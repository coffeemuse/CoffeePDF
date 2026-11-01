#!/usr/bin/env bash
# TEMPORARY, until Avalonia releases a fix for AvaloniaUI/Avalonia#22285 (README.md says how to remove it).
#
# Builds libAvaloniaNative.dylib: Avalonia 12.1.4's macOS library with that issue's patch, which CoffeePDF ships in
# place of the Avalonia.Native package's (AvaloniaNative.targets). On macOS 27 the package's library leaves the menu bar
# showing only the CoffeePDF menu after a window switch (issue #9).
#
#   packaging/avalonia-native/build.sh
#
# It's run by hand, and the library it writes beside it is committed, as make-icon.sh's icon is. It needs git, the .NET
# SDK and the Xcode command-line tools, with network access; Xcode itself isn't needed. Avalonia builds the library with
# an Xcode project, native/Avalonia.Native/src/OSX/Avalonia.Native.OSX.xcodeproj; this compiles the same sources with
# that project's Release settings, and checks the result exports everything the package's library does.
set -euo pipefail

die() { printf 'build.sh: %s\n' "$*" >&2; exit 1; }

version=12.1.4
commit=2d79ba55fae1b1e71c69f93a668080c9029e6a31 # the 12.1.4 tag's commit

[ "$(uname -s)" = Darwin ] || die "it builds only on macOS"
for tool in git dotnet xcrun lipo nm strings shasum; do
  command -v "$tool" >/dev/null || die "$tool isn't installed"
done

here=$(cd "$(dirname "$0")" && pwd)
patch="$here/avalonia-menu-bar-native-fix.patch"
out="$here/libAvaloniaNative.dylib"
stock="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')avalonia.native/$version/runtimes/osx/native/libAvaloniaNative.dylib"
[ -f "$stock" ] || die "the Avalonia.Native $version package isn't restored: build CoffeePDF once first"
work=$(mktemp -d "${TMPDIR:-/tmp}/coffeepdf-avalonia-native.XXXXXX")
trap 'rm -rf "$work"' EXIT

# Avalonia's source at the tag: only the native code, the interface definition its header is made from, and the build
# files that make it.
src="$work/avalonia"
git clone --quiet --filter=blob:none --no-checkout --depth 1 --branch "$version" https://github.com/AvaloniaUI/Avalonia.git "$src"
git -C "$src" sparse-checkout set --no-cone /native/ /src/Avalonia.Native/avn.idl /build/ \
  /Directory.Build.props /Directory.Build.targets /Directory.Packages.props /global.json /NuGet.Config
git -C "$src" checkout --quiet
[ "$(git -C "$src" rev-parse HEAD)" = "$commit" ] || die "Avalonia's $version tag isn't commit $commit any more"
git -C "$src" apply "$patch"

# inc/avalonia-native.h, which Avalonia generates from avn.idl rather than committing.
dotnet build "$src/native/Avalonia.Native/Avalonia.Native.macOS.proj" -t:GenerateMicroComItems \
  -p:BuildAvaloniaNativeXcodeProject=false -p:Configuration=Release -nologo -verbosity:quiet
[ -f "$src/native/Avalonia.Native/inc/avalonia-native.h" ] || die "Avalonia's header generator made no avalonia-native.h"

# The Xcode project's Release settings: ARC except in noarc.mm, gnu++0x (C++11), libc++, -Os, NSAssert off, macOS 10.13
# and up, both architectures, warnings off as its build turns them off. The Clang modules it enables apply to C and
# Objective-C, not to these Objective-C++ files. The command-line tools' clang looks for libc++'s headers beside
# itself, where they no longer are, so they're named in the SDK. No debug information, and the sources' folder is
# renamed in what's compiled, so the library holds no path of this Mac's.
sdk=$(xcrun --show-sdk-path)
flags=(-x objective-c++ -arch arm64 -arch x86_64 -mmacosx-version-min=10.13 -Os -fobjc-arc -std=gnu++11
       -stdlib=libc++ -stdlib++-isystem "$sdk/usr/include/c++/v1" -DNS_BLOCK_ASSERTIONS=1
       -I"$src/native/Avalonia.Native/inc" -w -ffile-prefix-map="$src"=avalonia)
mkdir "$work/obj"
for file in "$src"/native/Avalonia.Native/src/OSX/*.mm; do
  name=$(basename "$file" .mm)
  arc=()
  [ "$name" = noarc ] && arc=(-fno-objc-arc)
  xcrun clang++ "${flags[@]}" ${arc[@]+"${arc[@]}"} -c "$file" -o "$work/obj/$name.o"
done
xcrun clang++ -dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min=10.13 -stdlib=libc++ \
  -install_name /usr/local/lib/libAvalonia.Native.OSX.dylib -compatibility_version 1 -current_version 1 \
  -framework AppKit -framework Carbon -framework CoreGraphics -framework IOSurface -framework Metal \
  -framework OpenGL -framework QuartzCore -weak_framework UniformTypeIdentifiers \
  "$work"/obj/*.o -o "$work/libAvaloniaNative.dylib"
built="$work/libAvaloniaNative.dylib"

# What Avalonia.Native calls must all be there, and the patch must be in. (The greps read all their input: with -q,
# pipefail would count the writer's broken pipe as a failure.)
for arch in arm64 x86_64; do
  lipo "$built" -verify_arch "$arch" || die "the library has no $arch half"
  missing=$(comm -23 <(nm -gU -arch "$arch" "$stock" | awk '{print $3}' | sort) \
                     <(nm -gU -arch "$arch" "$built" | awk '{print $3}' | sort))
  [ -z "$missing" ] || die "the $arch library doesn't export what the package's does: $missing"
  nm -gU -arch "$arch" "$built" | grep RenewMenuBarItems >/dev/null || die "the $arch library lacks the patch's RenewMenuBarItems"
done
# Nothing of this Mac's goes into the repository (CLAUDE.md). The hook checks too, but this says why.
if strings -a "$built" | grep -iF -e "$HOME" -e "/Users/" >/dev/null; then die "the library holds a path of this Mac's"; fi

mv "$built" "$out"
printf 'Made %s\nSHA-256 %s (README.md records it)\n' "$out" "$(shasum -a 256 "$out" | cut -d' ' -f1)"
