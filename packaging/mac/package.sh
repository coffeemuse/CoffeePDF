#!/usr/bin/env bash
# Builds CoffeePDF for Apple silicon (spec §10): CoffeePDF.app, signed and, when the notary's key is given, notarised,
# in CoffeePDF-<version>.dmg.
#
#   packaging/mac/package.sh <version>        e.g. 0.1.0, or 0.1.0-rc1 (from the tag v0.1.0-rc1)
#
# Environment (all optional):
#   AVALONIA_LICENSE_KEY  the Avalonia licence key the PdfViewer checks. Unset, the app builds, but its PdfViewer throws
#                   as soon as a window creates it (the probe `What_the_viewer_does_without_a_licence`, spec §16),
#                   so it shows no PDF. With SIGN_IDENTITY set the script stops: a signed build is a release candidate.
#   SIGN_IDENTITY   the Developer ID Application identity to sign with, as `security find-identity` names it. Unset,
#                   the app gets only an ad-hoc signature, without the hardened runtime, so it runs here but Gatekeeper
#                   refuses it on any other Mac. Set but not in the keychains, the script stops: it never falls back to
#                   ad-hoc when a signature was asked for.
#   SIGN_KEYCHAIN   the keychain holding it (CI's temporary one); otherwise the usual search list.
#   NOTARY_KEY      the App Store Connect API key's .p8 file, with NOTARY_KEY_ID and NOTARY_ISSUER: with all three and
#                   a signing identity, the app and the disk image are notarised and stapled.
#   OUT_DIR         where the app and the disk image go; packaging/mac/out by default.
#
# The steps, as the Healdan Helper's: a self-contained, single-file .NET publish for osx-arm64 with no .pdb, since any
# file in Contents/MacOS that isn't native code breaks the bundle's signature; the bundle; signing inside-out (each
# native library, the binary, then the bundle), with the hardened runtime and a timestamp when there's an identity;
# codesign's strict check; notarising and stapling; the disk image, with a link to Applications, signed (and
# notarised) too. Any failure stops it.
set -euo pipefail

say() { printf '%s\n' "$*"; }
warn() { printf '\n*** %s ***\n\n' "$*" >&2; }
die() { printf 'package.sh: %s\n' "$*" >&2; exit 1; }

[ "$#" -eq 1 ] || die "usage: package.sh <version>, such as 0.1.0 or 0.1.0-rc1"
version=$1
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$ ]] || die "'$version' isn't a version like 1.2.3 or 1.2.3-rc1"
numeric=${version%%-*} # what CFBundleShortVersionString and CFBundleVersion take
[ "$(uname -s)" = Darwin ] || die "it builds only on macOS (codesign, hdiutil)"
for tool in dotnet codesign hdiutil plutil ditto lipo python3 file; do
  command -v "$tool" >/dev/null || die "$tool isn't installed"
done

here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
out=${OUT_DIR:-"$here/out"}
mkdir -p "$out"
out=$(cd "$out" && pwd)
app="$out/CoffeePDF.app"
dmg="$out/CoffeePDF-$version.dmg"
work=$(mktemp -d "${TMPDIR:-/tmp}/coffeepdf-package.XXXXXX")
trap 'rm -rf "$work"' EXIT
rm -rf "$app" "$dmg"

if [ -z "${AVALONIA_LICENSE_KEY:-}" ]; then
  # A signed build is a release candidate, and mustn't ship a PdfViewer that throws in the first window.
  [ -z "${SIGN_IDENTITY:-}" ] ||
    die "SIGN_IDENTITY asks for a signed build, but AVALONIA_LICENSE_KEY is unset, so the app's PdfViewer would throw as soon as a window creates it (unset SIGN_IDENTITY for a build without the key)"
  warn "WITHOUT the Avalonia licence key: AVALONIA_LICENSE_KEY is unset, so the PdfViewer throws as soon as a window creates it, and the app shows no PDF"
fi

# --- Who signs ------------------------------------------------------------------------------------------------------

keychain=()
if [ -n "${SIGN_KEYCHAIN:-}" ]; then
  keychain=(--keychain "$SIGN_KEYCHAIN")
fi
identity=${SIGN_IDENTITY:-}
if [ -n "$identity" ] && ! security find-identity -v -p codesigning ${SIGN_KEYCHAIN:+"$SIGN_KEYCHAIN"} | grep -qF "\"$identity\""; then
  die "the signing identity \"$identity\" isn't in the keychains (unset SIGN_IDENTITY to build without one)"
fi
if [ -n "$identity" ]; then
  signer=$identity
  timestamp=--timestamp
  runtime=(--options runtime)
  say "Signing with $identity"
else
  signer=-
  timestamp=--timestamp=none
  # The hardened runtime's library validation refuses an ad-hoc build's own libraries, so with it the app can't
  # launch (spec §16 row 7, Decision 4). Notarisation, which needs the hardened runtime, needs an identity anyway.
  runtime=()
  warn "NOT SIGNED with a Developer ID: SIGN_IDENTITY is unset. The app gets an ad-hoc signature only, without the hardened runtime, so it runs on this Mac, but Gatekeeper refuses it on any other, and it isn't notarised."
fi
notarise=false
if [ -n "${NOTARY_KEY:-}" ] && [ -n "${NOTARY_KEY_ID:-}" ] && [ -n "${NOTARY_ISSUER:-}" ]; then
  if [ -z "$identity" ]; then
    warn "NOT NOTARISED: the notary's key is set, but there's no signing identity"
  else
    [ -f "$NOTARY_KEY" ] || die "NOTARY_KEY ($NOTARY_KEY) isn't a file"
    notarise=true
  fi
elif [ -n "$identity" ]; then
  warn "NOT NOTARISED: NOTARY_KEY, NOTARY_KEY_ID and NOTARY_ISSUER aren't all set, so Gatekeeper refuses the app on other Macs"
fi

# --- The app --------------------------------------------------------------------------------------------------------

# CoffeePdfiumRid names the platform of CoffeePDF's own PDFium (CoffeePdf.Core.csproj). The core never sees --runtime,
# and without this it would take the building Mac's, which on an Intel Mac isn't the app's.
say "Publishing CoffeePDF $version for osx-arm64"
dotnet publish "$repo/CoffeePdf/CoffeePdf.csproj" \
  --configuration Release --runtime osx-arm64 --self-contained true --output "$work/publish" \
  -p:CoffeePdfiumRid=osx-arm64 -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -p:Version="$numeric" -p:InformationalVersion="$version" -p:IncludeSourceRevisionInInformationalVersion=false \
  -nologo

# Only native code may sit in Contents/MacOS: a managed .dll, a .pdb or a .json there breaks the bundle's signature.
for file in "$work/publish"/*; do
  file -b "$file" | grep -q '^Mach-O' || die "the publish left $(basename "$file"), which isn't native code, beside the binary"
done
[ -f "$work/publish/CoffeePDF" ] || die "the publish made no CoffeePDF binary"
# CoffeePDF's own copy of PDFium (CoffeePdf.Core.csproj), beside the PdfViewer's libpdfium: the publish above names its
# platform, and this stops a package that lost it or got the wrong one.
[ -f "$work/publish/libcoffeepdfium.dylib" ] || die "the publish made no libcoffeepdfium.dylib, CoffeePDF's own copy of PDFium"
lipo "$work/publish/libcoffeepdfium.dylib" -verify_arch arm64 2>/dev/null ||
  die "libcoffeepdfium.dylib isn't built for Apple silicon, so CoffeePdfiumRid didn't reach CoffeePdf.Core"

say "Building $app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp "$work/publish"/* "$app/Contents/MacOS/"
for library in "$app/Contents/MacOS"/*.dylib; do
  [ -e "$library" ] || continue
  # NuGet's native libraries are often universal: Apple silicon needs only its own half.
  if lipo "$library" -verify_arch arm64 2>/dev/null && [ "$(lipo -archs "$library")" != arm64 ]; then
    lipo "$library" -thin arm64 -output "$library.arm64"
    mv "$library.arm64" "$library"
  fi
done
chmod 755 "$app/Contents/MacOS"/*
sed -e "s/@VERSION@/$numeric/g" "$here/Info.plist.in" > "$app/Contents/Info.plist"
! grep -q '@[A-Z_]*@' "$app/Contents/Info.plist" || die "Info.plist.in has a placeholder package.sh doesn't fill"
plutil -lint "$app/Contents/Info.plist" >/dev/null
printf 'APPL????' > "$app/Contents/PkgInfo"
chmod 644 "$app/Contents/Info.plist" "$app/Contents/PkgInfo"
xattr -cr "$app" 2>/dev/null || true # Finder info or a resource fork would make codesign refuse it

# --- Signing, inside out --------------------------------------------------------------------------------------------

sign() {
  codesign --force ${runtime[@]+"${runtime[@]}"} "$timestamp" --sign "$signer" ${keychain[@]+"${keychain[@]}"} "$@"
}
for library in "$app/Contents/MacOS"/*.dylib; do
  [ -e "$library" ] || continue
  sign "$library"
done
sign --entitlements "$here/entitlements.plist" "$app/Contents/MacOS/CoffeePDF"
sign --entitlements "$here/entitlements.plist" "$app"
codesign --verify --strict --deep --verbose=2 "$app"
codesign --display --verbose=2 "$app" 2>&1 | grep -E '^(Identifier|Format|TeamIdentifier|Authority|Timestamp|Runtime Version|CodeDirectory)' || true

if [ "$notarise" = true ]; then
  field() { # <json> <name>: the answer's field, or nothing when the answer isn't JSON
    python3 -I -c '
import json, sys
try:
    value = json.loads(sys.argv[1]).get(sys.argv[2], "")
except (ValueError, AttributeError):
    value = ""
print(value if isinstance(value, str) else "")' "$1" "$2" || true
  }
  notarise() { # <file> <what>
    local result status id
    say "Notarising $2; Apple usually answers within a few minutes"
    result=$(xcrun notarytool submit "$1" --key "$NOTARY_KEY" --key-id "$NOTARY_KEY_ID" --issuer "$NOTARY_ISSUER" \
      --wait --timeout 30m --output-format json) || true
    status=$(field "$result" status)
    id=$(field "$result" id)
    if [ "$status" != Accepted ]; then
      if [ -n "$id" ]; then
        xcrun notarytool log "$id" --key "$NOTARY_KEY" --key-id "$NOTARY_KEY_ID" --issuer "$NOTARY_ISSUER" >&2 || true
      else
        printf 'notarytool answered:\n%s\n' "$result" >&2
      fi
      [ -n "$status" ] || status="an answer that isn't notarytool's JSON"
      die "notarising $2 ended with: $status"
    fi
    say "Notarised $2 ($id)"
  }
  # Stapling and validating fetch the ticket from Apple over the network, which a CI runner sometimes loses part-way
  # (NSURLErrorDomain -1005 and exit 68, on v0.0.1-spike4's first run), so they're tried three times.
  staple() { # <file>
    local try
    for try in 1 2 3; do
      if xcrun stapler staple "$1" && xcrun stapler validate "$1"; then return 0; fi
      [ "$try" -lt 3 ] || break
      warn "Stapling $(basename "$1") failed (try $try of 3), so it's tried again in $((try * 20)) seconds"
      sleep $((try * 20))
    done
    die "stapling $(basename "$1") failed three times"
  }
  ditto -c -k --keepParent "$app" "$work/app.zip"
  notarise "$work/app.zip" "the app"
  staple "$app"
  spctl --assess --type execute --verbose=2 "$app"
elif [ -n "$identity" ]; then
  # Before notarisation Gatekeeper may say "rejected": shown, not fatal.
  spctl --assess --type execute --verbose=2 "$app" || true
fi

# --- The disk image -------------------------------------------------------------------------------------------------

say "Building $dmg"
mkdir "$work/image"
ditto "$app" "$work/image/CoffeePDF.app"
ln -s /Applications "$work/image/Applications"
made=false
for attempt in 1 2 3; do # hdiutil is sometimes "busy" on CI's runners
  if hdiutil create -volname "CoffeePDF" -srcfolder "$work/image" -fs APFS -format ULFO -ov "$dmg" >/dev/null; then
    made=true
    break
  fi
  say "hdiutil failed (attempt $attempt); trying again"
  sleep 5
done
[ "$made" = true ] || die "hdiutil couldn't make the disk image"
codesign --force "$timestamp" --sign "$signer" ${keychain[@]+"${keychain[@]}"} "$dmg"
codesign --verify --strict --verbose=2 "$dmg"
if [ "$notarise" = true ]; then
  notarise "$dmg" "the disk image"
  staple "$dmg"
  spctl --assess --type open --context context:primary-signature --verbose=2 "$dmg"
fi

say ""
say "Built $dmg"
say "  sha256 $(shasum -a 256 "$dmg" | cut -d' ' -f1)"
if [ -z "$identity" ]; then
  warn "NOT SIGNED with a Developer ID (ad-hoc only), and not notarised"
elif [ "$notarise" = false ]; then
  warn "Signed with $identity, but NOT NOTARISED"
else
  say "  signed with $identity, notarised and stapled"
fi
