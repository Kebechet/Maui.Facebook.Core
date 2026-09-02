[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Maui.Facebook.Core.iOS
[![NuGet Version](https://img.shields.io/nuget/v/Kebechet.Maui.Facebook.Core.iOS)](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core.iOS/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kebechet.Maui.Facebook.Core.iOS)](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core.iOS/)
![Last updated (main)](https://img.shields.io/github/last-commit/Kebechet/Maui.Facebook.Core/main?path=src%2FMaui.Facebook.Core.iOS&label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

Bindings for the Facebook (Meta) iOS SDK core module
- https://github.com/facebook/facebook-ios-sdk
- frameworks: `FBSDKCoreKit`, plus its load-time dependencies `FBSDKCoreKit_Basics` and `FBAEMKit`
- changelog: https://github.com/facebook/facebook-ios-sdk/blob/main/CHANGELOG.md

Most consumers want the cross-platform wrapper, [`Kebechet.Maui.Facebook.Core`](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core/), rather than this binding directly.

## Versioning Scheme
The version is derived from the native package: `<native>.<binding-rev>`.

| Native lib version | Maui.Facebook.Core.iOS | Note |
|:--|:--|:--|
| 18.1.1 | 18.1.1.0 | First binding of 18.1.1 |
| 18.1.1 | 18.1.1.3 | Binding of 18.1.1 with 3 binding-only fixes |

# Binding creation

### A curated binding, not a Sharpie dump
`ApiDefinitions.cs` is written by hand against the headers shipped inside `nativelib/FBSDKCoreKit.xcframework`.
FBSDKCoreKit exposes 220 headers; the wrapper needs three types:

| Bound type | Source header | Notes |
|---|---|---|
| `FBSDKAppEvents` | `FBSDKAppEvents.h` | `shared`, `anonymousID`, `userID`, `logEvent:*`, `logPurchase:*`, `activateApp`, `flush` |
| `FBSDKSettings` | `FBSDKCoreKit-Swift.h` | Swift `Settings`, published as `@objc(FBSDKSettings)` - plain runtime name, no `_TtC…` mangling |
| `FBSDKApplicationDelegate` | `FBSDKCoreKit-Swift.h` | Swift `ApplicationDelegate`, same |

`FBSDKAppEventName` / `FBSDKAppEventParameterName` are `typedef NSString *` and bind as `string`.

Binding only what is used keeps the file reviewable, keeps it buildable on Windows (Objective Sharpie is
macOS-only) and turns a native bump into "swap the frameworks and rebuild".

### Getting the frameworks
Each release of `facebook-ios-sdk` publishes per-kit `*-Dynamic_XCFramework.zip` assets. Download three:
`FBSDKCoreKit`, `FBSDKCoreKit_Basics`, `FBAEMKit` (`scripts/bump-ios.sh` does this). An app that embeds
only `FBSDKCoreKit` crashes on launch with an image-not-found for the other two.

### ⚠️ Slimming the xcframeworks (177 MB → 21 MB)
From each `.xcframework` remove:
- the `ios-arm64_arm64e_x86_64-maccatalyst` slice - Catalyst is a no-op stub in the wrapper, and that slice uses the
  macOS bundle layout with symlinks that Windows cannot check out
- every `*.swiftmodule` directory
- every `*.dSYM` directory (debug symbols; also cause `NU5123` long-path warnings)
- `_CodeSignature` directories

then **remove the Catalyst entry from the xcframework's `Info.plist`** (`AvailableLibraries`), or the bundle no
longer matches its manifest and is rejected.

### Build
The binding builds and packs on Windows - `bgen` runs there and the frameworks ship as
`lib/net10.0-ios/*.resources.zip`. Only *running* it needs a Mac; CI builds the demo for the iOS simulator on macOS.

# License
This repository is licensed with the [MIT](../../LICENSE) license.
