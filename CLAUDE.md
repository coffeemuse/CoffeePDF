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
packaging/mac/make-icon.sh                                   # CoffeePDF.icns from packaging/icon/CoffeePDF.png; commit it
```

## Rules

- **The licence key** is never committed, logged or printed. `AvaloniaLicense.props` reads it from the environment.
- **Personal details stay out of the repository**, including PDFs' markup authors, which default to the macOS full name.
  - Run `git config core.hooksPath .githooks` once per clone. The hook rejects any PDF, and any change that holds the
    account's full name, account name or home folder.
  - Tests save with the made-up author `Test Reviewer`, and anything recording a name describes it instead of quoting it.
- **`CoffeePdf.Core` references no Avalonia assembly,** and `ArchitectureTests` fails if it does.
  - Logic goes in the core, with its tests in `CoffeePdf.Core.Tests`.
  - `CoffeePdf` is the Avalonia app. Its tests (`CoffeePdf.Tests`) run Avalonia headless.
- **Tests that need the PdfViewer** use `[PdfViewerFact]` and `[Trait("Category", "PdfViewer")]`. They skip in a build
  without the key.
- **Tests wait for conditions,** never for fixed times.
- **`TreatWarningsAsErrors` is on.**
- **The PdfViewer's `AutoSave` stays off** (spec §6.4).
- **Every save goes through `DocumentWindow.SaveToAsync`:** the PdfViewer saves to memory, `MarkupAuthor` writes the
  author, and `AtomicFile` replaces the file. The PdfViewer's `AllowDocumentSaving` stays off (spec §16 Decision 1).
- **CoffeePDF has its own copy of PDFium** (`libcoffeepdfium`, bound as `"coffeepdfium"`), separate from the
  PdfViewer's. The core's calls take `Pdfium.Gate`, and nothing calls `FPDF_DestroyLibrary`. `package.sh` names its
  architecture.
- **Tests never touch the real settings:** `Headless` sets `COFFEEPDF_SETTINGS_DIR`, and core tests pass a folder.
- **CoffeePDF reaches Healdan only through the Helper's channel** (spec §9). `ChannelRoutes` lists every route it
  calls. The client calls only those, and `FakeHelper` serves only those. A change to the channel in Healdan changes
  the list in the same release.
- **Tests never touch the real Helper:** `Headless` sets `HEALDAN_HELPER_DEV_ROOT` and `COFFEEPDF_LOG_DIR` to folders of
  the run's own, and the flows' tests give each window a `FakeHelper`'s link.
- **Nothing logs the channel's secret or a file's contents** (`CoffeeLog`). Log an exception by its type.
- **Packaging:** only native code may sit in the bundle's `Contents/MacOS`, which `package.sh` checks.
- **Pushes go through the owner.** Never push, tag or open a pull request without the owner's go-ahead.
