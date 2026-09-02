#!/usr/bin/env bash
#
# Bumps the Android Facebook SDK binding to a target Maven Central version.
#
# Usage: scripts/bump-android.sh <new-version>
#   e.g. scripts/bump-android.sh 18.4.0
#
# What it does:
#   1. Verifies the requested version exists on Maven Central - for facebook-core AND
#      facebook-bolts, because both AndroidMavenLibrary pins move together.
#   2. Points both <AndroidMavenLibrary> items at it - the .NET Android SDK downloads the
#      AARs (and POMs) when the binding is built; nothing is committed to the repo.
#   3. Bumps <Version> (<native>.<binding-rev>: the revision resets to .0 when the native
#      version changes and increments otherwise).
#   4. Compares the AAR's own minSdkVersion with <SupportedOSPlatformVersion> and raises
#      the csproj when the native library requires more.
#   5. Sets <PackageReleaseNotes> to a single entry for the version being published, with
#      Meta's CHANGELOG.md text for the native version when it is published.
#
# It deliberately does NOT touch the wrapper's <PackageReference>: the wrapper is only
# moved onto a binding that is already live on nuget.org, which is a separate step.
#
# Reading and rewriting files is delegated to scripts/facebook.cs, a .NET file-based app,
# so this stays shell glue and the repo keeps one language (see CLAUDE.md). `sed -i` here
# would strip CRLF line endings under Git Bash.

set -euo pipefail

NEW_VERSION="${1:?usage: bump-android.sh <new-version>}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ANDROID_CSPROJ="src/Maui.Facebook.Core.Android/Maui.Facebook.Core.Android.csproj"
MAVEN_BASE="https://repo1.maven.org/maven2/com/facebook/android"
POM_URL="${MAVEN_BASE}/facebook-core/${NEW_VERSION}/facebook-core-${NEW_VERSION}.pom"
BOLTS_POM_URL="${MAVEN_BASE}/facebook-bolts/${NEW_VERSION}/facebook-bolts-${NEW_VERSION}.pom"
AAR_URL="${MAVEN_BASE}/facebook-core/${NEW_VERSION}/facebook-core-${NEW_VERSION}.aar"
CHANGELOG_URL="https://github.com/facebook/facebook-android-sdk/blob/main/CHANGELOG.md"

facebook() { (cd "$REPO_ROOT" && dotnet run scripts/facebook.cs -- "$@"); }

echo "==> Bumping Android Facebook SDK to ${NEW_VERSION}"

# --- 1. Current versions from the csproj -----------------------------------------
CURRENT_NATIVE=$(facebook get-maven-pin "$ANDROID_CSPROJ")
CURRENT_BINDING_VERSION=$(facebook get-version "$ANDROID_CSPROJ")
echo "    current native version:  $CURRENT_NATIVE"
echo "    current binding version: $CURRENT_BINDING_VERSION"
echo "    target  native version:  $NEW_VERSION"

# --- 2. The target must exist on Maven Central -----------------------------------
# The AARs are only fetched at build time, so a typo here would otherwise surface as an
# opaque XA4234 much later. --retry (without --retry-all-errors) covers timeouts and
# 5xx but not the 404 we are actually testing for.
for url in "$POM_URL" "$BOLTS_POM_URL"; do
  echo "==> Verifying $url"
  if ! curl -fsSL --retry 3 --retry-delay 2 -o /dev/null "$url"; then
    echo "ERROR: ${url##*/} not found on Maven Central" >&2
    exit 1
  fi
done

# --- 3. New binding version --------------------------------------------------------
if [[ "$CURRENT_NATIVE" == "$NEW_VERSION" ]]; then
  NEW_BINDING_VERSION="${CURRENT_BINDING_VERSION%.*}.$(( ${CURRENT_BINDING_VERSION##*.} + 1 ))"
else
  NEW_BINDING_VERSION="${NEW_VERSION}.0"
fi
echo "    new     binding version: $NEW_BINDING_VERSION"

# --- 4. Minimum Android API the AAR itself requires ------------------------------------
# An AAR's manifest is plain XML, so this needs no aapt. A native SDK that raises its
# minSdk while the binding still claims a lower one compiles and packs cleanly and only
# breaks at deployment.
AAR_TMP="$(mktemp -d)"
trap 'rm -rf "$AAR_TMP"' EXIT
if ! curl -fsSL --retry 3 --retry-delay 2 -o "$AAR_TMP/facebook-core.aar" "$AAR_URL"; then
  echo "ERROR: could not download $AAR_URL to read its minSdkVersion" >&2
  exit 1
fi
NATIVE_MIN_OS=$(unzip -p "$AAR_TMP/facebook-core.aar" AndroidManifest.xml 2>/dev/null \
  | sed -n -E 's|.*android:minSdkVersion="([^"]+)".*|\1|p' | head -1)
if [[ -z "$NATIVE_MIN_OS" ]]; then
  echo "ERROR: could not read android:minSdkVersion from the AAR manifest" >&2
  exit 1
fi

echo "==> Native AAR requires API ${NATIVE_MIN_OS}"
MIN_OS_OUTPUT=$(facebook check-min-os "$ANDROID_CSPROJ" "$NATIVE_MIN_OS")
MIN_OS_RAISED=$(printf '%s\n' "$MIN_OS_OUTPUT" | sed -n -E 's|^min_os_raised=(.*)$|\1|p')
MIN_OS_PREVIOUS=$(printf '%s\n' "$MIN_OS_OUTPUT" | sed -n -E 's|^min_os_previous=(.*)$|\1|p')

# --- 5. Release note -----------------------------------------------------------------
EXCERPT=$(facebook changelog-excerpt android "$NEW_VERSION" || true)
if [[ "$CURRENT_NATIVE" == "$NEW_VERSION" ]]; then
  NOTE="${NEW_BINDING_VERSION}: rebuilt the binding for native Facebook Android SDK ${NEW_VERSION} (binding revision only, no native change)."
else
  NOTE="${NEW_BINDING_VERSION}: bumped native Facebook Android SDK from ${CURRENT_NATIVE} to ${NEW_VERSION}."
fi
if [[ -n "$EXCERPT" ]]; then
  NOTE+=" Upstream: ${EXCERPT}"
fi
if [[ "$MIN_OS_RAISED" == "true" ]]; then
  NOTE+=" BREAKING: the minimum supported Android API level is now ${NATIVE_MIN_OS} (was ${MIN_OS_PREVIOUS}), as required by the native SDK."
fi
NOTE+=" Changelog: ${CHANGELOG_URL}"
echo "    release note: $NOTE"

# Meta follows semver, so a new major is the upstream's own statement that something
# breaks - and its changelog marks removals under "### Removed". Neither is necessarily
# visible in the artifact (the binding may still build), so a release like that is never
# auto-merged.
UPSTREAM_BREAKING=false
if [[ "${CURRENT_NATIVE%%.*}" != "${NEW_VERSION%%.*}" ]] || printf '%s' "$EXCERPT" | grep -qiE 'Removed:|breaking'; then
  UPSTREAM_BREAKING=true
  echo "==> upstream marks this release as breaking (new major, or a Removed/breaking note)"
fi

# --- 6. Edit the csproj ----------------------------------------------------------------
facebook set-maven-pin "$ANDROID_CSPROJ" "$NEW_VERSION"
facebook set-version "$ANDROID_CSPROJ" "$NEW_BINDING_VERSION"
facebook set-release-note "$ANDROID_CSPROJ" "$NOTE"

# --- 7. Read everything back: a silently missed edit would ship "18.4.0.0" containing
#        facebook-core 18.3.0, which is worse than failing here. ---------------------------
WRITTEN_NATIVE=$(facebook get-maven-pin "$ANDROID_CSPROJ")
if [[ "$WRITTEN_NATIVE" != "$NEW_VERSION" ]]; then
  echo "ERROR: failed to update the <AndroidMavenLibrary> pin (still '${WRITTEN_NATIVE}')" >&2
  exit 1
fi
if grep -E 'Include="com\.facebook\.android:[a-z-]+"' "$REPO_ROOT/$ANDROID_CSPROJ" | grep -vqF "Version=\"${NEW_VERSION}\""; then
  echo "ERROR: not every com.facebook.android AndroidMavenLibrary pin moved to ${NEW_VERSION}" >&2
  grep -E 'Include="com\.facebook\.android:' "$REPO_ROOT/$ANDROID_CSPROJ" >&2
  exit 1
fi
WRITTEN_BINDING=$(facebook get-version "$ANDROID_CSPROJ")
if [[ "$WRITTEN_BINDING" != "$NEW_BINDING_VERSION" ]]; then
  echo "ERROR: failed to update <Version> (still '${WRITTEN_BINDING}')" >&2
  exit 1
fi
if ! grep -qF "<PackageReleaseNotes>${NOTE}</PackageReleaseNotes>" "$REPO_ROOT/$ANDROID_CSPROJ"; then
  echo "ERROR: failed to write the <PackageReleaseNotes> entry" >&2
  exit 1
fi

echo "==> Done"
echo "    binding version: $NEW_BINDING_VERSION"
echo "    files changed:"
echo "      - $ANDROID_CSPROJ"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "native_version=${NEW_VERSION}"
    echo "binding_version=${NEW_BINDING_VERSION}"
    echo "previous_native_version=${CURRENT_NATIVE}"
    echo "changelog_excerpt=${EXCERPT}"
    echo "release_note=${NOTE}"
    echo "upstream_breaking=${UPSTREAM_BREAKING}"
  } >> "$GITHUB_OUTPUT"
fi
