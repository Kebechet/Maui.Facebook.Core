# Repairing the iOS binding after a native SDK bump

You are running inside the `TryBumpIOS` workflow on a macOS runner. `scripts/bump-ios.sh`
has just replaced the three xcframeworks under `src/Maui.Facebook.Core.iOS/nativelib/`
(`FBSDKCoreKit`, `FBSDKCoreKit_Basics`, `FBAEMKit`), bumped `<Version>`, and the build step
failed. The build step is two things, and either can be the failure:

1. `dotnet build` of the binding project - a `CS####` error in `ApiDefinitions.cs` /
   `StructsAndEnums.cs`.
2. The **selector check** - `dotnet run scripts/facebook.cs -- check-selectors …` printed
   `MISSING <selector>` lines. The binding is hand-written (not Objective Sharpie output),
   bgen never reads the headers, so a selector Meta renamed or removed still compiles and
   would only throw `unrecognized selector` at runtime. The check is what fails instead.

## Goal

Make BOTH commands succeed, then stop:

```
dotnet build src/Maui.Facebook.Core.iOS/Maui.Facebook.Core.iOS.csproj -c Release
dotnet run scripts/facebook.cs -- check-selectors src/Maui.Facebook.Core.iOS/ApiDefinitions.cs src/Maui.Facebook.Core.iOS/nativelib/FBSDKCoreKit.xcframework/ios-arm64_arm64e/FBSDKCoreKit.framework/Headers
```

Start by reading `binding-build.log` in the repository root. The Objective-C truth is in
`src/Maui.Facebook.Core.iOS/nativelib/FBSDKCoreKit.xcframework/ios-arm64_arm64e/FBSDKCoreKit.framework/Headers/`:
`FBSDKAppEvents.h` for `FBSDKAppEvents`, and `FBSDKCoreKit-Swift.h` for the Swift-implemented
`FBSDKSettings` (`SWIFT_CLASS_NAMED("Settings")`) and `FBSDKApplicationDelegate`
(`SWIFT_CLASS_NAMED("ApplicationDelegate")`). Each bound type names its header in a comment.

## Rules

- Edit ONLY `src/Maui.Facebook.Core.iOS/ApiDefinitions.cs` and
  `src/Maui.Facebook.Core.iOS/StructsAndEnums.cs`. Every other change you make is
  discarded automatically before the verifying rebuild - the csproj, the xcframeworks,
  the wrapper, workflows and scripts included.
- Never change `<Version>`; never touch `nativelib/` or `src/Maui.Facebook.Core/`.
- Keep the surface the wrapper consumes bound and its **managed names unchanged** (the
  wrapper compiles against them):
  - `FBSDKAppEvents`: `Shared`, `UserID`, `AnonymousID`, all four `LogEvent` overloads,
    both `LogPurchase` overloads, `ActivateApp`, `Flush`, `ClearUserData`
  - `FBSDKSettings`: `SharedSettings`, `SdkVersion`, `IsAutoLogAppEventsEnabled`,
    `IsAdvertiserIDCollectionEnabled`, `IsAdvertiserTrackingEnabled`, `AppID`, `ClientToken`
  - `FBSDKApplicationDelegate`: `SharedInstance`, `InitializeSDK`, `FinishedLaunching`
  Deleting one of these to make the build or the check pass is not a fix. If Meta removed
  the underlying API outright, leave the file as it is and say so in `fix-summary.md` -
  that is a wrapper change for a human.
- When a selector was **renamed** upstream, change the `[Export("…")]` string to the new
  selector and keep the managed member name. When Meta moved a member between types, move
  the binding with it. Keep every other `[Export]` exactly as it is.
- Keep the file curated: bind only what the wrapper uses. Do not paste whole headers in,
  do not run Objective Sharpie.
- Do not run git; do not create branches or commits. The workflow commits for you.
- Minimal change: no reformatting, no unrelated cleanup. Keep the header-name comments.

## Known patterns

- **`MISSING logFoo:bar:`** from the selector check - open `FBSDKAppEvents.h`, find the
  method with the same purpose, update the `[Export]` (and parameter types if they changed).
  The check matches the *whole* declaration up to the `;`, so a selector that gained a
  trailing argument is reported missing - bind the new full selector.
- **Swift type no longer `@objc(FBSDK…)`** (`SWIFT_CLASS_NAMED` gone or renamed): update
  `Name = "…"` in `[BaseType]` to whatever `FBSDKCoreKit-Swift.h` now declares. Only use a
  `_TtC…` mangled name if the header shows the class is exported without an `@objc` name.
- **Property became readonly / method became a property**: adjust `{ get; set; }` /
  `[Export]` accordingly.
- **Enum values changed** in `StructsAndEnums.cs` (`FBSDKAdvertisingTrackingStatus`,
  `FBSDKAppEventsFlushBehavior`): mirror the header's `NS_ENUM` exactly.
- **Wrong nullability** - add or remove `[NullAllowed]` to match `_Nullable` / `nullable`
  in the header.
- **`NSString` parameters** - bind as `string`; `FBSDKAppEventName` /
  `FBSDKAppEventParameterName` are `typedef NSString *` and stay `string`.

## Loop

Edit -> run both commands -> read the new errors / MISSING lines -> repeat. Stop when
both pass, or when the failure is an upstream API removal that a binding edit cannot
express - in that case leave both files untouched.

## When done

Write `fix-summary.md` in the repository root (3-8 lines of markdown): which errors or
missing selectors you hit, what you changed and why, and anything a reviewer should
double-check against the headers. It is pasted into the pull request. If you changed
nothing, write one line saying so and why.
