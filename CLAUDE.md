# CLAUDE.md

This file guides Claude Code in this repository.

## What this repo is

CoffeePDF is an everyday PDF app for macOS (Linux and Windows later). It works with Healdan
(`Healdan-Project/healdan`) through the Healdan Helper's local channel, and never holds a Healdan token or calls the
server itself.

- The design is `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`, and the order of work is `docs/ROADMAP.md`.
  Read both before feature work.
- Each phase gets a plan in `docs/superpowers/plans/` before it's built. v2 features get a design spec first.
- The repository is private and the code proprietary (`LICENSE`). MIT is the intent, settled before it goes public
  (spec §12).

## Build and test

You need the .NET 10 SDK (`global.json` pins it). The PdfViewer needs the Avalonia licence key in
`AVALONIA_LICENSE_KEY` at build time.

```bash
dotnet test CoffeePdf.sln                                    # everything; the PdfViewer's tests skip without the key
dotnet test CoffeePdf.sln --filter Category=Probe --logger "console;verbosity=detailed"   # the probes, which print what they find
dotnet run --project CoffeePdf -- <file.pdf>                 # the app
packaging/mac/package.sh 1.2.3                               # CoffeePDF.app and its .dmg, on a Mac
```

## Rules

- **The licence key** is never committed, logged or printed. `AvaloniaLicense.props` reads it from the environment.
- **`CoffeePdf.Core` references no Avalonia assembly,** and `ArchitectureTests` fails if it does.
  - Logic goes in the core, with its tests in `CoffeePdf.Core.Tests`.
  - `CoffeePdf` is the Avalonia app. Its tests (`CoffeePdf.Tests`) run Avalonia headless.
- **Tests that need the PdfViewer** use `[PdfViewerFact]` and `[Trait("Category", "PdfViewer")]`. They skip in a build
  without the key.
- **Tests wait for conditions,** never for fixed times.
- **`TreatWarningsAsErrors` is on.**
- **The PdfViewer's `AutoSave` stays off** (spec §6.4).
- **Packaging:** only native code may sit in the bundle's `Contents/MacOS`, which `package.sh` checks.
- **Pushes go through the owner.** Never push, tag or open a pull request without the owner's go-ahead.
