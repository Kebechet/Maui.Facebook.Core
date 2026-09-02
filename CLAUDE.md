# Maui.Facebook.Core — project notes

## Public API surface & DI

The cross-platform contract is the interface `IFacebookCoreService`
(`src/Maui.Facebook.Core/Services/IFacebookCoreService.cs`).
The concrete `FacebookCoreService` is a `partial class` split per platform (Android / iOS) plus one
shared stub (`PlatformsStandard/`) compiled for every target without a native SDK: plain `net10.0`,
MacCatalyst and Windows. `AddFacebookCore()` registers the interface mapping as a singleton:

```csharp
services.AddSingleton<IFacebookCoreService, FacebookCoreService>();
```

**Consumers MUST depend on `IFacebookCoreService`, not the concrete class.**
The concrete type is not registered standalone — resolving it directly will fail.

## Where XML documentation lives

All public API XML docs live on `IFacebookCoreService`. The concrete partial members use
`/// <inheritdoc/>` so IntelliSense works regardless of whether the consumer holds an interface or
concrete reference. When adding new public surface, the workflow is:

1. Add the member to the interface with a `<summary>` (and `<param>` / `<returns>` / `<remarks>`).
2. Add the matching partial declaration to `FacebookCoreService.cs` with `/// <inheritdoc/>`.
3. Implement it in `Platforms/Android/…`, `Platforms/iOS/…` **and** `PlatformsStandard/…` (the stub).
   The private `…Method()` partials carry an accessibility modifier, so C# forces every part to
   implement them — a platform you forget fails the build rather than the consumer.

Documentation wording should be **adapted from Meta's official docs** to stay authoritative:
- Android: https://developers.facebook.com/docs/app-events/getting-started-app-events-android
- iOS:     https://developers.facebook.com/docs/app-events/getting-started-app-events-ios

If platform behavior diverges, call it out in the interface's `<remarks>` — the interface is the single
source of truth consumers read. Two divergences already recorded there: `SetAdvertiserTrackingEnabled` is
iOS-only (no-op on Android), and the Android `UserId` write is asynchronous (~80 ms before a read sees it).

## Behavioral contract the harness depends on

- Native failures are **never thrown**. They are caught, logged at `Error` through
  `ILogger<FacebookCoreService>`, and the member returns its type's default.
- Reading state (`IsInitialized`, `SdkVersion`, `AnonymousId`, `UserId`) before `Initialize` returns
  `null`/`false` **silently** — no log entry. The Android SDK throws `FacebookSdkNotInitializedException`
  on `userID` before init; the wrapper guards it. This matters because the demo page reads state during
  render, and a logging read re-renders via `HarnessLog.Changed`, which looped the UI once.
- Mutating before `Initialize` logs a `Warning` and drops the call.

The demo harness (`demo/DemoApp/DemoApp.Harness`) judges every check by that contract: it captures the
wrapper's `ILogger` output and fails any check that logged `Warning` or above.

## The iOS binding is curated, not generated

`src/Maui.Facebook.Core.iOS/ApiDefinitions.cs` is written by hand against the headers inside
`nativelib/FBSDKCoreKit.xcframework`. It binds exactly `FBSDKAppEvents`, `FBSDKSettings` and
`FBSDKApplicationDelegate` — the three types the wrapper uses — out of FBSDKCoreKit's 220 headers. Each
type names its source header in a comment so a bump can be checked against it.

Consequences:
- **Do not run Objective Sharpie over it.** A regenerated dump would replace a reviewable 150-line file
  with thousands of lines of unused, fragile surface.
- The Swift-implemented types (`Settings`, `ApplicationDelegate`) are published to Objective-C via
  `SWIFT_CLASS_NAMED`, i.e. `@objc(FBSDKSettings)`, so their runtime names are the plain `FBSDK*` ones
  and need **no** `_TtC…` mangling in `[BaseType(Name = …)]`.
- `FBSDKAppEventName` / `FBSDKAppEventParameterName` are `typedef NSString *`, bound as `string`.
- The binding builds and packs on **Windows** (`bgen` runs there); only *running* iOS needs a Mac. CI's
  `build-demo.yml` builds the demo for the iOS simulator on `macos-15` as the iOS smoke test.

Three xcframeworks ship in one package because `FBSDKCoreKit` links `FBSDKCoreKit_Basics` and `FBAEMKit`
at load time. They are the `*-Dynamic_XCFramework.zip` release assets with the **maccatalyst slice,
`.swiftmodule` and `.dSYM` directories and `_CodeSignature` removed** (177 MB → 21 MB). Removing a slice
also means removing its entry from the xcframework's `Info.plist`, or the bundle is invalid.

## The Android binding

`AndroidMavenLibrary` pulls `facebook-core` **and** `facebook-bolts` (Meta's own utility library, no
NuGet exists) from Maven Central at build time. `Transforms/Metadata.xml` binds only the `com.facebook`
and `com.facebook.appevents` packages and removes `GraphRequestAsyncTask` / `GraphRequestBatch`, whose
erased Java generics the generator cannot re-implement.

⚠️ **AndroidX and Kotlin PackageReferences are pinned to the wave `Microsoft.Maui.Core` resolves**
(Lifecycle 2.9.2.1 for MAUI 10.0.1), not to "latest". A newer AndroidX drags a newer Lifecycle and the
consuming MAUI app fails restore with NU1107. Bump them together with the MAUI baseline.

## Local NuGet feed for binding development

`nuget.config` at repo root defines a `local` source mapped to `./local-nuget` (committed empty, contents
ignored). The `Kebechet.Maui.Facebook.Core.*` family is mapped to BOTH `local` and `nuget.org` — source
mapping searches only the sources sharing the most specific pattern, so mapping the family to `local`
alone would break every machine with an empty `local-nuget`, i.e. every CI runner.

To verify a binding change end-to-end (consumed as a real package, not a `ProjectReference`):

1. Bump the binding `<Version>` (`<native>.<binding-rev>`), or clear
   `~/.nuget/packages/kebechet.maui.facebook.core.<platform>/<version>` to re-pack the same version.
2. `dotnet pack src/Maui.Facebook.Core.Android/Maui.Facebook.Core.Android.csproj -c Release -o local-nuget`
   (same for iOS).
3. Point the wrapper's `PackageReference` at it and rebuild the demo.

## Running the demo against a device

Debug APKs rely on Fast Deployment, so a plain `adb install` gives an APK with no managed assemblies
(`No assemblies found … Assuming this is part of Fast Deployment. Exiting`). Either deploy with
`dotnet build -t:Install`, or build self-contained:

```bash
dotnet build demo/DemoApp/DemoApp/DemoApp.csproj -f net10.0-android -p:RuntimeIdentifier=android-x64 -p:EmbedAssembliesIntoApk=true
adb install -r demo/DemoApp/DemoApp/bin/Debug/net10.0-android/android-x64/com.kebechet.demoapp-Signed.apk
adb shell am start -n com.kebechet.demoapp/.MainActivity --es appId <id> --ez autoRun true
adb logcat -d | grep "\[Harness\]"
```

Match the RID to the device (`adb shell getprop ro.product.cpu.abilist`; a 32-bit phone needs
`android-arm`). ⚠️ MIUI phones refuse `adb install` until *Install via USB* is enabled on the phone.
⚠️ Never pass an empty extra (`--es clientToken ""`): the shell drops it and `am` treats the next token
as the package name, so no extra arrives.

## Scripts and workflows

**No second scripting language.** Anything beyond thin shell glue is a **.NET 10 file-based app** —
`scripts/facebook.cs`, run as `dotnet run scripts/facebook.cs -- <command>`. Do NOT reach for Perl,
Python, Ruby or Node to parse XML/JSON/HTML; `facebook.cs` grows new subcommands instead.

`scripts/*.sh` stay thin: `curl`, `gh`, `git`, `dotnet`, `unzip`, `plutil`, control flow. They run on
ubuntu, macOS and Windows Git Bash — no `grep -P`, no `find -quit`. Never rewrite a file in place with
`sed -i`: Git Bash's sed strips CRLF line endings. File rewriting goes through `facebook.cs`, which
preserves BOM and line endings byte-for-byte.

The automated SDK bumps live in `.github/workflows/try-bump-android.yml` and `try-bump-ios.yml`; the
prompts Claude Code follows when a bumped binding does not build are
`.github/prompts/fix-<platform>-binding.md`. A bump PR touches only the binding project — never the
wrapper's binding `PackageReference`.

Two `facebook.cs` commands exist because the iOS binding is curated rather than generated:
- `slim-xcframework-plist <Info.plist>` drops the maccatalyst entries from `AvailableLibraries` after
  `bump-ios.sh` deletes that slice; it reproduces the committed plists byte-for-byte.
- `check-selectors <ApiDefinitions.cs> <headers-dir>` verifies every `[Export("…")]` is still declared
  in the new headers (whole declaration up to the `;`, trailing `NS_SWIFT_NAME(...)` macros allowed).
  `bgen` never reads headers, so without this a renamed selector compiles and throws at runtime. The
  TryBumpIOS build step runs it after `dotnet build`, so a miss lands as `binding-broken`.

## Versioning

The wrapper uses release-please. The `<Version>` line in `src/Maui.Facebook.Core/Maui.Facebook.Core.csproj`
is marked `<!-- x-release-please-version -->` and is updated automatically — never bump it in a feature
PR. Use conventional commit prefixes (`feat:`, `fix:`, `feat!:` for breaking); release-please derives
the version and changelog from them.

The two binding packages stay **out** of release-please: their versions are `<native>.<binding-rev>`
(`18.3.0.0`, `18.1.1.0`), bumped by the TryBump workflows and published separately.
