# A patched Avalonia macOS library: TEMPORARY

**This folder is a stopgap. Remove it once an Avalonia release fixes
[AvaloniaUI/Avalonia#22285](https://github.com/AvaloniaUI/Avalonia/issues/22285).** The steps are under
[Removing it](#removing-it). Every file that takes part mentions `22285`, so `git grep -n 22285` finds them all.

CoffeePDF ships `libAvaloniaNative.dylib` from this folder in place of the one in the Avalonia.Native 12.1.4 package.
It's Avalonia 12.1.4's own macOS library with one patch, the one attached to that issue. Nothing else in Avalonia is
changed. The owner chose this on 2026-10-31, over a runtime workaround in CoffeePDF's code and over waiting for
Avalonia.

## Why

CoffeePDF's [issue #9](https://github.com/coffeemuse/CoffeePDF/issues/9). On macOS 27, after a switch between
CoffeePDF's windows, the menu bar shows only the CoffeePDF menu, with no File, Edit, View or the rest:

- between two documents, for 2–3 seconds;
- from Settings back to a document, until the pointer reaches the menu bar.

Seen on macOS 27.0.1 with Avalonia 12.1.4. Avalonia#22285 reports it as not seen on macOS 26.

The cause is Avalonia's, not CoffeePDF's. On every change of key window, Avalonia gives `NSApp` a different `NSMenu`
twice, about 6 ms apart. The window losing key puts up the application menu alone (the bar's CoffeePDF menu), and the
window gaining key puts up its own. macOS 27 draws the first and holds it. The same issue reports a second defect with
the same cause. When a keyboard shortcut is what changes the window, such as ⌘, opening Settings, the top-level menu
holding that shortcut can stop opening for the rest of the run.

## What the patch does

The patch is `avalonia-menu-bar-native-fix.patch`, exactly as attached to Avalonia#22285. It was written against
Avalonia's `main` at `334536066a` and applies cleanly to the 12.1.4 tag. It changes four files in
`native/Avalonia.Native/src/OSX/`:

1. **A window losing key no longer touches the menu bar.** The window gaining key installs its own. When the last
   window closes, the application menu goes up one turn of the main loop later, and only if no other window has taken
   the bar.
2. **A window without a menu (Settings) gets the application menu one turn later**, in one install.
3. **Each installed bar gets fresh top-level `NSMenuItem`s.** The issue found that a damaged title can only be revived
   by a new item.
4. **The application menu moves between bars through a stand-in item**, so the bar on screen never loses its first
   title.

The issue's author measured it on macOS 27: no blank bar, and every menu keeps opening.

## How it's wired in

- **`AvaloniaNative.targets`**, imported by `CoffeePdf/CoffeePdf.csproj`, replaces the package's library with this one.
  - It covers a build, which puts the library in `runtimes/osx/native/`, and `package.sh`'s publish for `osx-arm64`,
    which puts it beside the binary.
  - **The build stops if Avalonia.Native isn't 12.1.4.** The library and Avalonia's .NET side talk through interfaces
    that can change between versions, so a 12.1.4 library under a newer Avalonia could crash. An upgrade therefore
    stops at an error that says to remove the patch or rebuild it.
  - It also stops if the library is missing.
- **`packaging/mac/package.sh`** stops if the published library isn't byte-for-byte this one.
- **The tests are unaffected.** Avalonia headless doesn't load the native library, and `CoffeePdf.Tests` takes the
  package's copy.

## Rebuilding it

```bash
packaging/avalonia-native/build.sh
```

`build.sh` needs git, the .NET SDK and the Xcode command-line tools, with network access. It doesn't need Xcode. It:

1. fetches Avalonia's source at the 12.1.4 tag, and checks the tag is still commit
   `2d79ba55fae1b1e71c69f93a668080c9029e6a31`;
2. applies the patch;
3. generates `avalonia-native.h` from `avn.idl`, with Avalonia's own MSBuild target;
4. compiles the 34 `.mm` files with the Release settings of Avalonia's Xcode project, for arm64 and x86_64;
5. checks the result: it must export everything the package's library does, carry the patch's `RenewMenuBarItems`, and
   hold no path of the building Mac's.

Commit the library it writes, and record its SHA-256 below. Another toolchain may give different bytes; that's
expected.

To rebuild for another Avalonia version, change `version` and `commit` in `build.sh` and
`PatchedAvaloniaNativeVersion` in `AvaloniaNative.targets`. The patch must still apply, and the Mac checks below must
pass again.

### The library committed

| | |
|---|---|
| Built | 2026-10-31, by `build.sh` |
| Avalonia | 12.1.4, commit `2d79ba55fae1b1e71c69f93a668080c9029e6a31` |
| Patch SHA-256 | `3e990a47ad05ea7372792662f8dd9eac37002344a83c7ebf1889e164d963a06f` |
| Toolchain | Apple clang 21.0.0 (clang-2100.3.34.2), Command Line Tools 27.0, macOS 27.0 SDK |
| `libAvaloniaNative.dylib` SHA-256 | `7e90415e5102764bf300c4bf478fc62819f2d0be3580f405b6b9497fec12b5e0` |

Compared with the package's library, it has the same architectures (arm64 and x86_64), links the same system libraries
and has the same minimum macOS. It exports everything the package's does, plus the patch's functions and two of
Avalonia's C++ callbacks that the package's library doesn't export. Avalonia's .NET side uses neither.

## Removing it

Remove it once an Avalonia release fixes Avalonia#22285. Watch the issue and Avalonia's release notes.

1. In `Directory.Packages.props`, move `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Headless`
   and `Avalonia.Skia` to that release.
2. Delete this folder.
3. Delete the `Import` of `AvaloniaNative.targets`, and its comment, from `CoffeePdf/CoffeePdf.csproj`.
4. Delete the `libAvaloniaNative.dylib` check from `packaging/mac/package.sh`.
5. Delete the rule in `CLAUDE.md` and the line in `docs/ROADMAP.md`'s backlog. Record the removal in the spec: §4.3's
   `packaging/avalonia-native/` line goes, and §16's entry for issue #9 says which release fixed it.
6. `git grep -n 22285` should then find only that record.
7. Check on the Mac, with `dotnet run --project CoffeePdf -- <file.pdf>`, that the upstream fix works. Avalonia may not
   have taken this patch as it is.
   - Open Settings with ⌘, close it, and click a document: the whole menu bar shows.
   - With two documents open, click from one to the other: the whole bar shows straight away.
   - After ⌘, the CoffeePDF menu still drops down. After ⌘O (then Cancel), or ⌘W closing one of two documents, the
     File menu still does.
