// Every read, edit and parse the bump pipeline needs, as one .NET 10 file-based app:
//
//   dotnet run scripts/facebook.cs -- <command> [args]
//
// The shell scripts stay thin glue (curl, gh, git, dotnet, unzip, plutil). This file is
// where XML, JSON and markdown handling lives, so the repository never grows a second
// scripting language - see CLAUDE.md.
//
// Every write preserves the file's BOM and line endings byte-for-byte: Git Bash's sed -i
// strips CRLF, which silently rewrote every line of a csproj once.
//
// Commands
//   get-version <csproj>                            print <Version>
//   get-maven-pin <csproj>                          print the com.facebook.android:facebook-core pin
//   get-min-os <csproj>                             print <SupportedOSPlatformVersion>
//   set-version <csproj> <version>                  rewrite <Version>
//   set-maven-pin <csproj> <version>                rewrite every com.facebook.android:* AndroidMavenLibrary pin
//   set-release-note <csproj> <note>                replace <PackageReleaseNotes> with one entry
//   set-package-version <csproj> <pkgId> <version>  rewrite one <PackageReference> version
//   check-min-os <csproj> <native-min>              raise the floor when the native lib needs more
//   compare-versions <a> <b>                        print -1, 0 or 1 (dotted numeric)
//   changelog-excerpt <android|ios> <version>       Meta's CHANGELOG.md entry for that version, one line
//   slim-xcframework-plist <Info.plist>             drop the maccatalyst entries from AvailableLibraries
//   check-selectors <ApiDefinitions.cs> <headers-dir>
//                                                   every [Export] selector must still be declared in the headers

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: dotnet run scripts/facebook.cs -- <command> [args]");
    return 2;
}

try
{
    return args[0] switch
    {
        "get-version" => Print(ReadElement(Arg(1), "Version")),
        "get-maven-pin" => Print(ReadMavenPin(Arg(1))),
        "get-min-os" => Print(ReadElement(Arg(1), "SupportedOSPlatformVersion")),
        "set-version" => SetElement(Arg(1), "Version", Arg(2)),
        "set-maven-pin" => SetMavenPin(Arg(1), Arg(2)),
        "set-release-note" => SetReleaseNote(Arg(1), Arg(2)),
        "set-package-version" => SetPackageVersion(Arg(1), Arg(2), Arg(3)),
        "check-min-os" => CheckMinOs(Arg(1), Arg(2)),
        "compare-versions" => Print(CompareVersions(Arg(1), Arg(2)).ToString(CultureInfo.InvariantCulture)),
        "changelog-excerpt" => await ChangelogExcerpt(Arg(1), Arg(2)),
        "slim-xcframework-plist" => SlimXcframeworkPlist(Arg(1)),
        "check-selectors" => CheckSelectors(Arg(1), Arg(2)),
        var other => Fail($"unknown command '{other}'"),
    };
}
catch (UsageException ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 2;
}

string Arg(int index) => index < args.Length
    ? args[index]
    : throw new UsageException($"'{args[0]}' is missing argument {index}");

int Print(string value)
{
    Console.WriteLine(value);
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"ERROR: {message}");
    return 1;
}

// --- file IO that leaves the encoding exactly as it found it ---------------------------

static (bool Bom, string Eol, string Text) ReadFile(string path)
{
    var bytes = File.ReadAllBytes(path);
    var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    var text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
    return (bom, text.Contains("\r\n") ? "\r\n" : "\n", text);
}

static void WriteFile(string path, bool bom, string text)
{
    var body = new UTF8Encoding(false).GetBytes(text);
    using var stream = File.Create(path);
    if (bom) stream.Write([0xEF, 0xBB, 0xBF]);
    stream.Write(body);
}

// --- csproj reads ----------------------------------------------------------------------

static string ReadElement(string csproj, string element)
{
    var match = Regex.Match(ReadFile(csproj).Text, $"<{element}>([^<]+)</{element}>");
    return match.Success
        ? match.Groups[1].Value.Trim()
        : throw new UsageException($"no <{element}> found in {csproj}");
}

static string ReadMavenPin(string csproj)
{
    var match = Regex.Match(
        ReadFile(csproj).Text,
        @"<AndroidMavenLibrary\s+Include=""com\.facebook\.android:facebook-core""\s+Version=""([^""]+)""");
    return match.Success
        ? match.Groups[1].Value
        : throw new UsageException($"no <AndroidMavenLibrary Include=\"com.facebook.android:facebook-core\" .../> in {csproj}");
}

// --- csproj writes ---------------------------------------------------------------------

static int Replace(string path, string pattern, string replacement, string what)
{
    var (bom, _, text) = ReadFile(path);
    var updated = Regex.Replace(text, pattern, replacement);
    if (updated == text && !Regex.IsMatch(text, pattern))
        return Fail($"{what} not found in {path}");

    WriteFile(path, bom, updated);
    return 0;
}

static int SetElement(string csproj, string element, string value) =>
    Replace(csproj, $"<{element}>[^<]+</{element}>", $"<{element}>{value}</{element}>", $"<{element}>");

// facebook-core and facebook-bolts are released together under one version; the SDK's own
// POM pins bolts to the exact same number, so the two pins must move as one.
static int SetMavenPin(string csproj, string version) =>
    Replace(
        csproj,
        """(<AndroidMavenLibrary\s+Include="com\.facebook\.android:[a-z-]+"\s+Version=")[^"]+(")""",
        $"${{1}}{version}${{2}}",
        "the com.facebook.android AndroidMavenLibrary pins");

static int SetPackageVersion(string csproj, string packageId, string version) =>
    Replace(
        csproj,
        $"""(<PackageReference\s+Include="{Regex.Escape(packageId)}"\s+Version=")[^"]+(")""",
        $"${{1}}{version}${{2}}",
        $"a <PackageReference> for {packageId}");

// The notes are metadata for the version being published, not a changelog of past
// releases: nuget.org already shows every earlier version's own notes.
static int SetReleaseNote(string csproj, string note)
{
    if (note.AsSpan().IndexOfAny('<', '>', '&') >= 0)
        return Fail($"the note must not contain XML markup: {note}");

    return Replace(
        csproj,
        "(<PackageReleaseNotes>).*?(</PackageReleaseNotes>)",
        $"${{1}}{note.Replace("$", "$$")}${{2}}",
        "<PackageReleaseNotes>");
}

// --- minimum OS ------------------------------------------------------------------------

// Dotted numeric comparison: "16.0" > "14.2", "21" > "19", "3.9.1" > "3.9.0".
static int CompareVersions(string left, string right)
{
    var a = left.Split('.');
    var b = right.Split('.');
    for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
    {
        var x = i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pa) ? pa : 0;
        var y = i < b.Length && int.TryParse(b[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pb) ? pb : 0;
        if (x != y) return x < y ? -1 : 1;
    }
    return 0;
}

// A native SDK that raises its floor while the binding still claims a lower one compiles,
// packs and passes every build check - it only breaks at deployment. So the floor is read
// from the artifact and the binding is raised to match. Whether the WRAPPER follows is
// breaking for consumers and stays a human decision, which is why the caller turns a
// raise into a draft PR.
static int CheckMinOs(string csproj, string nativeMin)
{
    var current = ReadElement(csproj, "SupportedOSPlatformVersion");
    var raised = CompareVersions(nativeMin, current) > 0;
    var updated = raised ? nativeMin : current;

    if (raised)
    {
        var result = SetElement(csproj, "SupportedOSPlatformVersion", nativeMin);
        if (result != 0) return result;
        Console.Error.WriteLine($"==> minimum OS raised: {current} -> {nativeMin} (required by the native library)");
    }
    else
    {
        Console.Error.WriteLine($"==> minimum OS unchanged: csproj {current}, native library {nativeMin}");
    }

    Emit($"min_os_native={nativeMin}");
    Emit($"min_os_previous={current}");
    Emit($"min_os_current={updated}");
    Emit($"min_os_raised={(raised ? "true" : "false")}");
    return 0;

    static void Emit(string line)
    {
        Console.WriteLine(line);
        var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(output)) File.AppendAllText(output, line + Environment.NewLine);
    }
}

// --- xcframework slimming --------------------------------------------------------------

// The Catalyst slice is deleted from every shipped xcframework (the wrapper is a no-op stub
// on MacCatalyst, and that slice uses the macOS bundle layout with symlinks that Windows
// cannot check out). An xcframework whose Info.plist still lists a slice that is gone is
// rejected as invalid, so the manifest has to lose the entry too.
static int SlimXcframeworkPlist(string plistPath)
{
    var (bom, _, text) = ReadFile(plistPath);
    if (!text.Contains("<key>AvailableLibraries</key>", StringComparison.Ordinal))
        return Fail($"no AvailableLibraries in {plistPath}");

    // One <dict>…</dict> entry per slice; a slice's dict nests <array> but never another
    // <dict>, so "from a <dict> to the next </dict> with no <dict> in between" is exactly one
    // entry; the outer <dict> is excluded by that rule when the Catalyst slice comes first.
    // Text surgery
    // rather than an XML round-trip, which reformats the DOCTYPE and whitespace.
    var updated = Regex.Replace(
        text,
        @"\r?\n[ \t]*<dict>(?:(?!</?dict>).)*?<key>LibraryIdentifier</key>\s*<string>[^<]*maccatalyst[^<]*</string>(?:(?!</?dict>).)*?</dict>",
        string.Empty,
        RegexOptions.Singleline);

    var removed = Regex.Matches(text, "<dict>").Count - Regex.Matches(updated, "<dict>").Count;
    if (removed == 0)
    {
        Console.Error.WriteLine($"==> {plistPath}: no maccatalyst entry, nothing to do");
        return 0;
    }

    WriteFile(plistPath, bom, updated);
    Console.Error.WriteLine($"==> {plistPath}: removed {removed} maccatalyst entr{(removed == 1 ? "y" : "ies")}");
    return 0;
}

// --- curated binding vs. new headers ---------------------------------------------------

// ApiDefinitions.cs is hand-written, so nothing regenerates it on a bump - and bgen never
// looks at the headers, so a selector Meta renamed or removed would compile, pack and pass
// every build check, then throw "unrecognized selector" on the first call in production.
// This is the check that turns that into a red binding build: every [Export("...")] must
// still be declared in the framework's headers, in Objective-C declaration form.
static int CheckSelectors(string apiDefinitions, string headersDir)
{
    if (!Directory.Exists(headersDir)) return Fail($"headers directory not found: {headersDir}");

    var headers = string.Join('\n', Directory.EnumerateFiles(headersDir, "*.h", SearchOption.AllDirectories).Select(File.ReadAllText));
    var selectors = Regex.Matches(ReadFile(apiDefinitions).Text, @"\[(?:[A-Za-z]+,\s*)*Export\s*\(\s*""([^""]+)""")
        .Select(m => m.Groups[1].Value)
        .Distinct()
        .ToList();
    if (selectors.Count == 0) return Fail($"no [Export] attributes found in {apiDefinitions}");

    var missing = selectors.Where(selector => !Regex.IsMatch(headers, SelectorPattern(selector))).ToList();
    Console.Error.WriteLine($"==> {selectors.Count} selectors checked against {headersDir}, {missing.Count} missing");
    if (missing.Count == 0) return 0;

    foreach (var selector in missing) Console.WriteLine($"MISSING {selector}");
    return 1;

    // "logEvent:valueToSum:" must match `- (void)logEvent:(FBSDKAppEventName)eventName valueToSum:(double)valueToSum;`
    // and nothing else - in particular not the longer `logEvent:valueToSum:parameters:` overload,
    // which is why the pattern runs through to the `;`. Trailing macros such as
    // `NS_SWIFT_NAME(logPurchase(amount:currency:))` are allowed in front of it. "anonymousID"
    // (a property) must appear as a whole identifier.
    static string SelectorPattern(string selector)
    {
        if (!selector.Contains(':')) return $@"\b{Regex.Escape(selector)}\b";
        var segments = selector.TrimEnd(':').Split(':');
        var argument = @"\s*\([^()]*(?:\([^()]*\)[^()]*)*\)\s*\w+";
        var trailingMacros = @"(?:\s*\w+(?:\([^;]*?\))?)*";
        return @"[-+]\s*\([^;]*?\)\s*" + string.Join(@"\s+", segments.Select(s => Regex.Escape(s) + @"\s*:" + argument)) + trailingMacros + @"\s*;";
    }
}

// --- changelog -------------------------------------------------------------------------

// Both SDKs keep a Keep-a-Changelog style CHANGELOG.md at the repository root, and it is
// the only place Meta writes per-version notes: GitHub releases on facebook-ios-sdk carry
// the same text and facebook-android-sdk releases carry none. Never throws: a missing
// changelog must not block a bump.
static async Task<int> ChangelogExcerpt(string platform, string version)
{
    var repo = platform switch
    {
        "android" => "facebook/facebook-android-sdk",
        "ios" => "facebook/facebook-ios-sdk",
        _ => null,
    };
    if (repo is null) return Fail($"unknown platform '{platform}'");

    string markdown;
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Kebechet.Maui.Facebook.Core-bump/1.0");
        using var response = await http.GetAsync($"https://raw.githubusercontent.com/{repo}/main/CHANGELOG.md");
        if (!response.IsSuccessStatusCode) return 0;
        markdown = await response.Content.ReadAsStringAsync();
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return 0;
    }

    var excerpt = ExcerptFromChangelog(markdown, version);
    if (!string.IsNullOrWhiteSpace(excerpt)) Console.WriteLine(Sanitize(excerpt));
    return 0;
}

// The result is written into <PackageReleaseNotes>, which set-release-note refuses to fill
// with markup rather than corrupt the csproj.
static string Sanitize(string value) =>
    Regex.Replace(value.Replace("&", "and").Replace("<", string.Empty).Replace(">", string.Empty), @"\s+", " ").Trim();

// Walks the "## 18.1.1" (iOS) or "## [18.3.0]" (Android) block up to the next "## " and
// folds it into one line: "Changed: a. b. Fixed: c." Wrapped bullet continuation lines are
// joined onto their bullet; the "[Full Changelog]" trailer is dropped.
static string? ExcerptFromChangelog(string markdown, string version)
{
    var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r'));
    var block = lines
        .SkipWhile(l => !Regex.IsMatch(l, $@"^##\s+\[?{Regex.Escape(version)}\]?\s*$"))
        .Skip(1)
        .TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal))
        .ToList();
    if (block.Count == 0) return null;

    var parts = new List<string>();
    var bullet = new StringBuilder();
    void Flush()
    {
        if (bullet.Length == 0) return;
        var item = Regex.Replace(bullet.ToString(), @"\s+", " ").Trim();
        item = Regex.Replace(item, @"\[([^\]]+)\]\([^)]*\)", "$1").Replace("**", string.Empty).Replace("`", string.Empty);
        if (!string.IsNullOrWhiteSpace(item)) parts.Add(item.EndsWith('.') ? item : item + ".");
        bullet.Clear();
    }

    foreach (var line in block)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("[Full Changelog]", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(trimmed, @"^\[\d{4}-\d{2}-\d{2}\]\(")) { Flush(); continue; }
        if (trimmed.StartsWith("### ", StringComparison.Ordinal)) { Flush(); parts.Add(trimmed[4..].Trim() + ":"); continue; }
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
        {
            Flush();
            bullet.Append(trimmed[2..]);
        }
        else if (bullet.Length > 0)
        {
            bullet.Append(' ').Append(trimmed);
        }
    }
    Flush();

    // A heading with nothing under it ("Fixed:" and then the next release) says nothing.
    var result = string.Join(" ", parts.Where((p, i) => !p.EndsWith(':') || (i + 1 < parts.Count && !parts[i + 1].EndsWith(':'))));
    return result.Length > 0 ? result : null;
}

file sealed class UsageException(string message) : Exception(message);
