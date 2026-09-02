#!/usr/bin/env bash
#
# Bumps the iOS Facebook SDK binding to a target version.
#
# Usage: scripts/bump-ios.sh <new-version>
#   e.g. scripts/bump-ios.sh 18.2.0
#
# Prerequisites: macOS (plutil is used to read the framework's binary Info.plist). No
# Objective Sharpie: the binding is curated, not generated - see the iOS README.
#
# What it does:
#   1. Downloads the three per-kit `*-Dynamic_XCFramework.zip` release assets from
#      facebook/facebook-ios-sdk (FBSDKCoreKit + its load-time dependencies
#      FBSDKCoreKit_Basics and FBAEMKit) and replaces the committed xcframeworks.
#   2. Slims each xcframework the way the committed ones are: the maccatalyst slice,
#      .swiftmodule / dSYMs / _CodeSignature directories are removed and the slice's entry
#      is dropped from the xcframework Info.plist (177 MB -> 21 MB, and Windows can check
#      it out).
#   3. Checks every [Export] selector in the hand-written ApiDefinitions.cs is still
#      declared in the new headers - bgen never looks at headers, so a renamed selector
#      would otherwise compile and throw at runtime. A miss is reported (selectors_ok=false)
#      and the TryBumpIOS build step turns it into a binding-broken draft PR to fix.
#   4. Compares the framework's own MinimumOSVersion with <SupportedOSPlatformVersion>
#      and raises the csproj when the native library requires more.
#   5. Bumps <Version> (<native>.<binding-rev>) and sets <PackageReleaseNotes> to a single
#      entry for the version being published, with Meta's CHANGELOG.md text.
#
# It deliberately does NOT touch the wrapper's <PackageReference>: the wrapper is only
# moved onto a binding that is already live on nuget.org, which is a separate step.
#
# Reading and rewriting files is delegated to scripts/facebook.cs, a .NET file-based app,
# so this stays shell glue and the repo keeps one language (see CLAUDE.md).

set -euo pipefail

NEW_VERSION="${1:?usage: bump-ios.sh <new-version>}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IOS_DIR="src/Maui.Facebook.Core.iOS"
IOS_CSPROJ="$IOS_DIR/Maui.Facebook.Core.iOS.csproj"
KITS=(FBSDKCoreKit FBSDKCoreKit_Basics FBAEMKit)
RELEASE_BASE="https://github.com/facebook/facebook-ios-sdk/releases/download/v${NEW_VERSION}"
CHANGELOG_URL="https://github.com/facebook/facebook-ios-sdk/blob/main/CHANGELOG.md"
HEADERS="nativelib/FBSDKCoreKit.xcframework/ios-arm64_arm64e/FBSDKCoreKit.framework/Headers"
PLIST="nativelib/FBSDKCoreKit.xcframework/ios-arm64_arm64e/FBSDKCoreKit.framework/Info.plist"

# Paths passed to facebook.cs are repo-relative, so the subshell can cd to the root regardless
# of where the caller is: the iOS project directory contains a csproj, and `dotnet run`
# from there would try to run that project instead of the script.
facebook() { (cd "$REPO_ROOT" && dotnet run scripts/facebook.cs -- "$@"); }

echo "==> Bumping iOS Facebook SDK to ${NEW_VERSION}"

# --- 1. Current versions from the csproj -----------------------------------------
CURRENT_BINDING_VERSION=$(facebook get-version "$IOS_CSPROJ")
CURRENT_NATIVE="${CURRENT_BINDING_VERSION%.*}"
echo "    current native version:  $CURRENT_NATIVE"
echo "    current binding version: $CURRENT_BINDING_VERSION"
echo "    target  native version:  $NEW_VERSION"

if [[ "$CURRENT_NATIVE" == "$NEW_VERSION" ]]; then
  NEW_BINDING_VERSION="${CURRENT_BINDING_VERSION%.*}.$(( ${CURRENT_BINDING_VERSION##*.} + 1 ))"
else
  NEW_BINDING_VERSION="${NEW_VERSION}.0"
fi
echo "    new     binding version: $NEW_BINDING_VERSION"

# --- 2. Download and replace the three xcframeworks -------------------------------------
# All three are downloaded before anything is deleted, so a missing asset leaves the
# checkout untouched instead of half-replaced.
cd "$REPO_ROOT/$IOS_DIR"
DL_TMP="$(mktemp -d)"
trap 'rm -rf "$DL_TMP"' EXIT
for kit in "${KITS[@]}"; do
  ZIP_URL="${RELEASE_BASE}/${kit}-Dynamic_XCFramework.zip"
  echo "==> Downloading $ZIP_URL"
  HTTP_CODE=$(curl -sSL --retry 3 --retry-delay 2 -w "%{http_code}" -o "$DL_TMP/$kit.zip" "$ZIP_URL")
  if [[ "$HTTP_CODE" != "200" ]]; then
    echo "ERROR: HTTP $HTTP_CODE - ${kit}-Dynamic_XCFramework.zip not found on the v${NEW_VERSION} release" >&2
    exit 1
  fi
  unzip -tq "$DL_TMP/$kit.zip" > /dev/null
done

mkdir -p nativelib
for kit in "${KITS[@]}"; do
  rm -rf "nativelib/$kit.xcframework"
  unzip -q "$DL_TMP/$kit.zip" -d nativelib/
  if [[ ! -d "nativelib/$kit.xcframework" ]]; then
    echo "ERROR: ${kit}-Dynamic_XCFramework.zip did not contain $kit.xcframework at its root" >&2
    exit 1
  fi

  echo "==> Slimming nativelib/$kit.xcframework"
  find "nativelib/$kit.xcframework" -mindepth 1 -maxdepth 1 -type d -name "*-maccatalyst" -prune -exec rm -rf {} +
  find "nativelib/$kit.xcframework" -type d \( -name "*.swiftmodule" -o -name "dSYMs" -o -name "*.dSYM" -o -name "_CodeSignature" \) -prune -exec rm -rf {} +
  facebook slim-xcframework-plist "$IOS_DIR/nativelib/$kit.xcframework/Info.plist"
done
# The zip contains the xcframework and nothing else, but guard against a stray top-level
# file (a LICENSE or README) landing in nativelib.
find nativelib -mindepth 1 -maxdepth 1 ! -name "*.xcframework" -exec rm -rf {} +

# --- 3. The curated binding must still match the headers --------------------------------
# Reported, not fatal: the frameworks and versions are still written so the TryBumpIOS
# workflow's build step (which re-runs this check) can fail, hand ApiDefinitions.cs to
# the repair step and open the PR as binding-broken - the same path a compile error takes.
echo "==> Checking ApiDefinitions.cs selectors against $HEADERS"
SELECTORS_OK=true
if ! facebook check-selectors "$IOS_DIR/ApiDefinitions.cs" "$IOS_DIR/$HEADERS"; then
  SELECTORS_OK=false
  echo "WARNING: ApiDefinitions.cs binds selectors the ${NEW_VERSION} headers no longer declare (listed above); the binding needs a manual fix" >&2
fi

# --- 4. Minimum iOS version the framework itself requires ------------------------------
NATIVE_MIN_OS=$(plutil -extract MinimumOSVersion raw -o - "$PLIST" 2>/dev/null || true)
if [[ -z "$NATIVE_MIN_OS" ]]; then
  NATIVE_MIN_OS=$(/usr/libexec/PlistBuddy -c "Print :MinimumOSVersion" "$PLIST" 2>/dev/null || true)
fi
if [[ -z "$NATIVE_MIN_OS" ]]; then
  echo "ERROR: could not read MinimumOSVersion from $IOS_DIR/$PLIST" >&2
  exit 1
fi

cd "$REPO_ROOT"

echo "==> Native framework requires iOS ${NATIVE_MIN_OS}"
MIN_OS_OUTPUT=$(facebook check-min-os "$IOS_CSPROJ" "$NATIVE_MIN_OS")
MIN_OS_RAISED=$(printf '%s\n' "$MIN_OS_OUTPUT" | sed -n -E 's|^min_os_raised=(.*)$|\1|p')
MIN_OS_PREVIOUS=$(printf '%s\n' "$MIN_OS_OUTPUT" | sed -n -E 's|^min_os_previous=(.*)$|\1|p')

# --- 5. Release note -----------------------------------------------------------------
EXCERPT=$(facebook changelog-excerpt ios "$NEW_VERSION" || true)
if [[ "$CURRENT_NATIVE" == "$NEW_VERSION" ]]; then
  NOTE="${NEW_BINDING_VERSION}: rebuilt the binding for native Facebook iOS SDK ${NEW_VERSION} (binding revision only, no native change)."
else
  NOTE="${NEW_BINDING_VERSION}: bumped native Facebook iOS SDK from ${CURRENT_NATIVE} to ${NEW_VERSION}."
fi
if [[ -n "$EXCERPT" ]]; then
  NOTE+=" Upstream: ${EXCERPT}"
fi
if [[ "$MIN_OS_RAISED" == "true" ]]; then
  NOTE+=" BREAKING: the minimum supported iOS version is now ${NATIVE_MIN_OS} (was ${MIN_OS_PREVIOUS}), as required by the native SDK."
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
facebook set-version "$IOS_CSPROJ" "$NEW_BINDING_VERSION"
facebook set-release-note "$IOS_CSPROJ" "$NOTE"

WRITTEN_BINDING=$(facebook get-version "$IOS_CSPROJ")
if [[ "$WRITTEN_BINDING" != "$NEW_BINDING_VERSION" ]]; then
  echo "ERROR: failed to update <Version> (still '${WRITTEN_BINDING}')" >&2
  exit 1
fi
if ! grep -qF "<PackageReleaseNotes>${NOTE}</PackageReleaseNotes>" "$IOS_CSPROJ"; then
  echo "ERROR: failed to write the <PackageReleaseNotes> entry" >&2
  exit 1
fi

echo "==> Done"
echo "    binding version: $NEW_BINDING_VERSION"
echo "    files changed:"
echo "      - $IOS_CSPROJ"
for kit in "${KITS[@]}"; do
  echo "      - $IOS_DIR/nativelib/$kit.xcframework"
done

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "native_version=${NEW_VERSION}"
    echo "binding_version=${NEW_BINDING_VERSION}"
    echo "previous_native_version=${CURRENT_NATIVE}"
    echo "changelog_excerpt=${EXCERPT}"
    echo "release_note=${NOTE}"
    echo "upstream_breaking=${UPSTREAM_BREAKING}"
    echo "selectors_ok=${SELECTORS_OK}"
  } >> "$GITHUB_OUTPUT"
fi
