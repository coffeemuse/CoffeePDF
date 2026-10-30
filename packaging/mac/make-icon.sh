#!/usr/bin/env bash
# Makes CoffeePDF.icns, CoffeePDF.app's icon, from the artwork in packaging/icon/CoffeePDF.png: a square picture at
# least 1024 pixels across, drawn to its edges. Linux and Windows will make their icons from the same artwork.
#
#   packaging/mac/make-icon.sh
#
# It's run by hand when the artwork changes, and the .icns it writes beside it is committed, so package.sh only copies
# it. make-icon.swift puts the artwork on Apple's grid for a macOS icon, a rounded square with continuous corners, at
# each size from 16 to 1024 pixels: macOS 26 shows an icon of that shape as it is, and puts any other inside a grey
# rounded square. iconutil packs the sizes into the .icns.
set -euo pipefail

die() { printf 'make-icon.sh: %s\n' "$*" >&2; exit 1; }

[ "$(uname -s)" = Darwin ] || die "it runs only on macOS (Core Graphics, iconutil)"
for tool in swift iconutil; do
  command -v "$tool" >/dev/null || die "$tool isn't installed (the Xcode command-line tools have both)"
done

here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
art="$repo/packaging/icon/CoffeePDF.png"
icns="$here/CoffeePDF.icns"
[ -f "$art" ] || die "there's no artwork at $art"
work=$(mktemp -d "${TMPDIR:-/tmp}/coffeepdf-icon.XXXXXX")
trap 'rm -rf "$work"' EXIT

swift "$here/make-icon.swift" "$art" "$work/CoffeePDF.iconset"
iconutil --convert icns --output "$icns" "$work/CoffeePDF.iconset"
printf 'Made %s\n' "$icns"
