# Repairing the Android binding after a native SDK bump

You are running inside the `TryBumpAndroid` workflow. `scripts/bump-android.sh` has just
moved `src/Maui.Facebook.Core.Android/Maui.Facebook.Core.Android.csproj` onto a new
`com.facebook.android:facebook-core` (and `facebook-bolts`) version and `dotnet build`
failed. The .NET Android SDK downloads those AARs from Maven Central and generates C# from
them at build time, so the only lever is the binding metadata under
`src/Maui.Facebook.Core.Android/Transforms/`.

## Goal

Make this command succeed, then stop:

```
dotnet build src/Maui.Facebook.Core.Android/Maui.Facebook.Core.Android.csproj -c Release
```

Start by reading `binding-build.log` in the repository root. The `BG####` / `CS####`
errors name the Java type or member at fault; `BG8605` / `BG8xxx` warnings about types in
`com.facebook.internal.*` or `com.facebook.appevents.internal.*` are pre-existing noise, not
the failure (those packages are not bound - see the first `remove-node` in `Metadata.xml`).

## Rules

- Edit ONLY files under `src/Maui.Facebook.Core.Android/Transforms/`
  (`Metadata.xml`, `EnumFields.xml`, `EnumMethods.xml`). Every other change you make is
  discarded automatically before the verifying rebuild - the csproj, the wrapper,
  workflows and scripts included.
- Never change `<Version>` or the `<AndroidMavenLibrary>` pins. Never touch
  `src/Maui.Facebook.Core/`.
- Keep the surface the wrapper consumes bound, in `com.facebook` and
  `com.facebook.appevents`:
  - `FacebookSdk`: `setApplicationId`, `setClientToken`, `sdkInitialize(Context)`,
    `fullyInitialize`, `setAutoLogAppEventsEnabled`, `setAdvertiserIDCollectionEnabled`,
    `getSdkVersion`
  - `AppEventsLogger`: `newLogger(Context)`, `activateApp(Application)`,
    `getAnonymousAppDeviceGUID(Context)`, `getUserID` / `setUserID` / `clearUserID`,
    every `logEvent` overload (`String`, `String,double`, `String,Bundle`,
    `String,double,Bundle`), `logPurchase(BigDecimal,Currency,Bundle)`, `flush`
  Removing a member the wrapper uses just to make the build pass is not a fix - prefer
  renaming (`managedName`) or removing the *conflicting* overload or internal member.
- A **new dependency** the POM demands (`XA4241` / `XA4242`: "Java dependency ... is not
  satisfied") is NOT fixable from `Transforms/`: it needs a `<PackageReference>` (or
  `JavaArtifact` metadata) in the csproj, which you must not edit. Leave the Transforms
  untouched and say so in `fix-summary.md` - the PR opens as a draft for a human.
- Do not run git; do not create branches or commits. The workflow commits for you.
- Minimal change: no reformatting, no unrelated cleanup. Keep the existing XML comments -
  they record why each transform exists - and add one above each transform you add,
  naming the error it resolves.

## Known patterns

- **Erased Java generics the generator cannot re-implement** (`CS0534` / `CS0115` on a
  class extending `AsyncTask<…>` / `AbstractList<…>` / `Comparable<…>`): `remove-node` the
  class - see the existing `GraphRequestAsyncTask` / `GraphRequestBatch` removals in
  `Metadata.xml`. Only do this for types outside the surface listed above.
- **Kotlin-generated companions / `DefaultImpls` breaking generation**: they live in the
  bound packages; `remove-node` the specific nested class, never the whole package.
- **Java overloads erasing to the same C# signature**: hide or rename one with
  `<attr path="..." name="managedName">NewName</attr>`.
- **Type name equal to its namespace (BG8403)**: `managedName` attr.
- **`int` parameters or constants that should be enums**: `EnumMethods.xml` /
  `EnumFields.xml`.
- **A new public package the wrapper does not need** starts tripping generation: extend
  the package filter in the first `remove-node` only if the wrapper's surface stays in
  `com.facebook` and `com.facebook.appevents`.

## Loop

Edit -> run the build command -> read the new errors -> repeat. Stop when the build
passes, or when you are convinced the failure is a generator bug that metadata cannot
fix - in that case leave the Transforms untouched.

## When done

Write `fix-summary.md` in the repository root (3-8 lines of markdown): which errors you
hit, what you changed and why, and anything a reviewer should double-check. It is pasted
into the pull request. If you changed nothing, write one line saying so and why.
