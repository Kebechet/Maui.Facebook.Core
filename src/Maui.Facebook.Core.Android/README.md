[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Maui.Facebook.Core.Android
[![NuGet Version](https://img.shields.io/nuget/v/Kebechet.Maui.Facebook.Core.Android)](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core.Android/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kebechet.Maui.Facebook.Core.Android)](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core.Android/)
![Last updated (main)](https://img.shields.io/github/last-commit/Kebechet/Maui.Facebook.Core/main?path=src%2FMaui.Facebook.Core.Android&label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

Bindings for the Facebook (Meta) Android SDK core module
- https://github.com/facebook/facebook-android-sdk
- Maven: [`com.facebook.android:facebook-core`](https://central.sonatype.com/artifact/com.facebook.android/facebook-core)
- changelog: https://github.com/facebook/facebook-android-sdk/blob/main/CHANGELOG.md

Most consumers want the cross-platform wrapper, [`Kebechet.Maui.Facebook.Core`](https://www.nuget.org/packages/Kebechet.Maui.Facebook.Core/), rather than this binding directly.

## Versioning Scheme
The version is derived from the native package: `<native>.<binding-rev>`.

| Native lib version | Maui.Facebook.Core.Android | Note |
|:--|:--|:--|
| 18.3.0 | 18.3.0.0 | First binding of 18.3.0 |
| 18.3.0 | 18.3.0.3 | Binding of 18.3.0 with 3 binding-only fixes |

## What is bound
Only the `com.facebook` and `com.facebook.appevents` Java packages are surfaced in C# (`Transforms/Metadata.xml`);
`com.facebook.internal.*` and friends are embedded but not bound. Two Graph API classes whose erased Java
generics the generator cannot re-implement (`GraphRequestAsyncTask`, `GraphRequestBatch`) are removed.

## How the binding was created
- Reference the native library straight from Maven Central with
	[`AndroidMavenLibrary`](https://learn.microsoft.com/en-us/dotnet/android/binding-libs/binding-java-libs/binding-java-maven-library)
	instead of committing an `.aar`:
	```xml
	<AndroidMavenLibrary Include="com.facebook.android:facebook-core" Version="18.3.0" />
	<AndroidMavenLibrary Include="com.facebook.android:facebook-bolts" Version="18.3.0" Bind="false" />
	```
	Both AARs are downloaded when **this binding** is built and baked into the nupkg, so consumers need
	no Java, no Gradle and no build-time downloads. `facebook-bolts` is Meta's own utility library that
	`facebook-core` depends on; no NuGet binding for it exists, so it ships here unbound.
- The artifact's POM drives **Java dependency verification**: a missing dependency fails the build with
	`XA4241`/`XA4242` naming exactly what is missing, so the `PackageReference` list is cross-checked on every bump.
  - A package that does not advertise which Maven artifact it fulfils (no `artifact_versioned=` nuspec tag)
	says so via `JavaArtifact="group:id:version"` metadata on its `PackageReference`.
- ⚠️ The AndroidX / Kotlin `PackageReference`s are pinned to the wave the current `Microsoft.Maui.Core` resolves,
	not to the newest on nuget.org - a newer AndroidX drags a newer Lifecycle and consuming apps fail restore
	with `NU1107`.

# License
This repository is licensed with the [MIT](../../LICENSE) license.
