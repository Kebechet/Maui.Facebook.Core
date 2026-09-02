[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Maui.Facebook.Core
[![NuGet Version](https://img.shields.io/nuget/v/Kebechet.Maui.Facebook.Core)](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kebechet.Maui.Facebook.Core)](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core/)
[![unit-tests](https://github.com/Kebechet/Maui.Facebook.Core/actions/workflows/unit-tests.yml/badge.svg)](https://github.com/Kebechet/Maui.Facebook.Core/actions/workflows/unit-tests.yml)
[![build-demo](https://github.com/Kebechet/Maui.Facebook.Core/actions/workflows/build-demo.yml/badge.svg)](https://github.com/Kebechet/Maui.Facebook.Core/actions/workflows/build-demo.yml)
![Last updated (main)](https://img.shields.io/github/last-commit/Kebechet/Maui.Facebook.Core/main?label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

.NET MAUI wrapper for the **Facebook (Meta) Core SDKs** - App Events, SDK settings and the anonymous
app-device id - on Android and iOS.

It exists because Meta's attribution partners (RevenueCat, Adjust, AppsFlyer, ...) need the Facebook
**anonymous id** (`anon_id` / `$fbAnonId`) before they will deliver an in-app event to Meta, and that id
only comes from Meta's own SDK. There is no maintained MAUI binding of that SDK; this is one.

| Package | What | Native SDK |
|---|---|---|
| [`Kebechet.Maui.Facebook.Core`](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core/) | the cross-platform wrapper you reference | - |
| [`Kebechet.Maui.Facebook.Core.Android`](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core.Android/) | binding | [`com.facebook.android:facebook-core`](https://central.sonatype.com/artifact/com.facebook.android/facebook-core) 18.3.0 |
| [`Kebechet.Maui.Facebook.Core.iOS`](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core.iOS/) | binding | [`FBSDKCoreKit`](https://github.com/facebook/facebook-ios-sdk) 18.1.1 (+ `FBSDKCoreKit_Basics`, `FBAEMKit`) |

## Usage

Register it in `MauiProgram.cs`:

```csharp
builder.Services.AddFacebookCore();
```

Initialize once at startup with the values from the Meta App Dashboard (App ID, and the Client Token
from *Settings → Advanced*):

```csharp
public partial class App : Application
{
    private readonly IFacebookCoreService _facebookCore;

    public App(IFacebookCoreService facebookCore)
    {
        InitializeComponent();
        _facebookCore = facebookCore;
    }

    protected override void OnStart()
    {
        _facebookCore.Initialize(appId: "<FacebookAppId>", clientToken: "<FacebookClientToken>");
        base.OnStart();
    }
}
```

No `Info.plist` keys and no `AndroidManifest.xml` meta-data are required: the wrapper configures the SDK
programmatically. (The Android SDK still logs an informational *"Failed to auto initialize the Facebook
SDK"* at process start because the manifest carries no app id - that is expected, the explicit
`Initialize` call is what configures it.)

### Hand the anonymous id to your attribution partner

```csharp
var anonymousId = _facebookCore.AnonymousId;   // "XZ" + UUID, persisted per install, works with ATT denied

// RevenueCat: the attribute its Meta Ads integration requires before it delivers events to Meta
_revenueCatBilling.SetAttributes(new Dictionary<string, string> { ["$fbAnonId"] = anonymousId! });
```

### Log events

```csharp
_facebookCore.UserId = accountId;                          // attached to every later event; null clears it
_facebookCore.LogEvent("fb_mobile_complete_registration");
_facebookCore.LogEvent("level_up", new Dictionary<string, object> { ["level"] = 3 }, valueToSum: 3);
_facebookCore.LogPurchase(9.99m, "EUR");
_facebookCore.Flush();
```

### Consent

```csharp
_facebookCore.SetAutoLogAppEventsEnabled(consented);
_facebookCore.SetAdvertiserIdCollectionEnabled(consented);
_facebookCore.SetAdvertiserTrackingEnabled(attAuthorized);   // iOS only; a no-op on Android
```

Full API with remarks per member: [`IFacebookCoreService`](src/Maui.Facebook.Core/Services/IFacebookCoreService.cs).

## Dummy classes

Windows, MacCatalyst and the plain `net10.0` target ship a no-op implementation, so you never have to
guard calls by platform. Their members return `true` for `bool` and `null` for nullable strings.

## Exception behavior

- The library throws only for developer mistakes.
- Every native failure is caught and reported through `ILogger<FacebookCoreService>` at `Error`, and the
  member returns the default of its type. Nothing native ever surfaces as an exception in your code.

## ⚠️ iOS Local debugging

Because of MAUI and VS bugs:
- https://github.com/xamarin/xamarin-macios/issues/19229
- https://developercommunity.visualstudio.com/t/MAUI---Cannot-create-native-types-when-d/10180586
- potential workaround: https://github.com/dotnet/maui/issues/10800#issuecomment-1301564278

it is not possible to run your app with hot-restart (direct local iOS deploy from VS for Windows).

## Demo and on-device harness

`demo/DemoApp` is a MAUI Blazor Hybrid app whose single page runs every wrapper member against the real
SDK and reports pass/fail per member. Because the wrapper never throws, the harness judges each check by
what the wrapper **logged**: a check that logs a warning or error fails.

It is scriptable, so a device run needs no tapping:

```bash
adb shell am start -n com.kebechet.demoapp/.MainActivity --es appId <id> --es clientToken <token> --ez autoRun true
adb logcat -d | grep "\[Harness\]"
```

⚠️ Do not pass an empty extra (`--es clientToken ""`) - the shell drops it and `am` misparses the rest.
Leave the flag out instead; the app defaults the token to empty.

## Automated SDK updates

Two independent workflows watch Meta's native SDKs and each turn a new release into **one standalone
pull request that is ready to merge when it opens**:

| Workflow | Watches | Runner |
|---|---|---|
| `TryBumpAndroid` (`.github/workflows/try-bump-android.yml`) | `com.facebook.android:facebook-core` on Maven Central | ubuntu |
| `TryBumpIOS` (`.github/workflows/try-bump-ios.yml`) | `facebook/facebook-ios-sdk` GitHub releases | macOS |

Both run daily and on demand (`workflow_dispatch` takes an optional explicit version). A run bumps
the **binding** csproj only. The wrapper csproj is asserted byte-identical before anything is
committed, so `main` never references a binding that is not on nuget.org yet.

The iOS binding is **curated by hand** (`src/Maui.Facebook.Core.iOS/ApiDefinitions.cs`), not generated:
FBSDKCoreKit exposes 220 headers and the wrapper needs three types. A bump therefore swaps and slims the
xcframeworks and rebuilds. Because `bgen` never reads the headers, a selector Meta renamed would still
compile - so the bump also runs `dotnet run scripts/facebook.cs -- check-selectors`, which verifies every
`[Export]` in `ApiDefinitions.cs` against the new headers and fails the build step when one is gone. Only
then does Claude Code repair the definitions, and the PR opens as a draft either way.

Secrets: `BUMP_BOT_APP_ID` / `BUMP_BOT_APP_PRIVATE_KEY` (the bot that pushes and opens PRs) and
`CLAUDE_CODE_OAUTH_TOKEN` (from `claude setup-token`; without it a broken binding simply lands as a
draft PR with the build log attached).

Merging and publishing the binding, and moving the wrapper onto it, are separate steps.

## Contributions
Feel free to create an issue or pull request. In case you would like to do massive changes in the
package please firstly discuss them in the issue because otherwise there is high chance that such big PR
would be rejected.

## License
This repository is licensed with the [MIT](LICENSE) license.
