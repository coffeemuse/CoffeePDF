# CoffeePDF Phase 0: Foundation Spike Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the CoffeePDF repository: a solution that builds and tests, a window that opens a local PDF in Avalonia's PdfViewer, a signed and notarised macOS package, and CI. Answer the spec's Phase 0 questions (§14) with tests and probes, and record the findings for the owner.

**Architecture:** The layout mirrors the Healdan Helper's (`clients/helper/` in Healdan-Project/healdan):
- `CoffeePdf.Core` has no Avalonia and holds the logic.
- `CoffeePdf` is the Avalonia 12 app.
- Two xUnit test projects; the app's runs Avalonia headless.

Tests that need the licensed PdfViewer run only when the build has the licence key. Probes print what the PdfViewer actually does, for the findings.

**Tech Stack:**
- .NET 10 and C#.
- Avalonia 12.1.4, with `Avalonia.Controls.PdfViewer` 12.0.1 (built on PDFium 157) and `AvaloniaUI.Licensing` 3.1.3.
- xUnit 2.9.3 and Avalonia.Headless. `System.Security.Cryptography.Pkcs` signs a test fixture.
- Bash for packaging, and GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` (§4.3, §8, §10–§14). Read it first.

## Global Constraints

- **SDK:** `global.json` pins it at `10.0.100` with `rollForward: latestFeature`. Projects target `net10.0`.
- **Package versions** are managed centrally in `Directory.Packages.props`. Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Headless and Avalonia.Skia are all `12.1.4`. Avalonia.Controls.PdfViewer is `12.0.1`, and AvaloniaUI.Licensing is `3.1.3`.
- **`CoffeePdf.Core` references no Avalonia assembly** (spec §4.3, §14 item 8). `ArchitectureTests` enforces it.
- **The Avalonia licence key** comes only from the `AVALONIA_LICENSE_KEY` environment variable, through `AvaloniaLicense.props`. It's never committed, logged, printed or written into any file in the repository.
- **Compiler settings:** `TreatWarningsAsErrors`, `Nullable` and `ImplicitUsings` are on everywhere, through `Directory.Build.props`.
- **Bundle identifiers:** the bundle id is `com.healdan.coffeepdf`, the app is `CoffeePDF.app`, the binary is `CoffeePDF`, and the disk image is `CoffeePDF-<version>.dmg`.
- **Packaging** is macOS on Apple silicon only (`osx-arm64`), with `LSMinimumSystemVersion` `26.0`, as the Helper has.
- **The PdfViewer's `AutoSave` stays off** (spec §6.4).
- **Tests wait for conditions, never for fixed times.** The one exception is the disk-write probe, which says why.
- **Tests needing the PdfViewer** use `[PdfViewerFact]` and `[Trait("Category", "PdfViewer")]`. Probes also carry `[Trait("Category", "Probe")]`.
- **Pushes go through the owner.** The repository is `coffeemuse/CoffeePDF`, which is private. Never push, tag or open a pull request without the owner's go-ahead.

---

## File Structure

| File | Responsibility |
|---|---|
| `global.json`, `nuget.config`, `Directory.Build.props`, `Directory.Packages.props` | Build settings, as the Helper's |
| `AvaloniaLicense.props` | The licence key from the environment, and `AVALONIA_LICENSED` when it's there; imported by the app and its tests |
| `CoffeePdf.sln` | The solution (`.sln` format, as the Helper's) |
| `CoffeePdf.Core/Files/PdfFile.cs` | Tells a PDF by its header |
| `CoffeePdf.Core/Documents/WindowChoice.cs` | Where an opened PDF goes: its window, an empty one, or a new one |
| `CoffeePdf.Core.Tests/…` | The core's tests, and the architecture test |
| `CoffeePdf/Program.cs`, `CoffeePdf/App.axaml(.cs)` | Start-up, themes, open-file events, the window list |
| `CoffeePdf/Windows/DocumentWindow.axaml(.cs)` | One PDF's window: the PdfViewer and the File menu |
| `CoffeePdf.Tests/Headless.cs`, `PdfViewerFactAttribute.cs`, `Viewers.cs`, `Probe.cs` | Test infrastructure |
| `CoffeePdf.Tests/Fixtures/MinimalPdf.cs`, `SignedPdf.cs` | PDFs written by the tests: plain, with another app's note, and signed |
| `CoffeePdf.Tests/Windows/DocumentWindowTests.cs` | The window shows a PDF |
| `CoffeePdf.Tests/Viewer/ViewerSaveTests.cs`, `ViewerProbes.cs` | Phase 0's checks of the PdfViewer (spec §14) |
| `packaging/mac/Info.plist.in`, `entitlements.plist`, `package.sh` | The app bundle and its disk image |
| `.github/workflows/ci.yml` | Tests on every push; packaging and release on a `v*` tag |
| `README.md`, `CLAUDE.md`, `LICENSE`, `docs/ROADMAP.md`, `.gitignore` | The repository's front matter (spec §11) |

---

### Task 1: Repository foundation and `PdfFile`

**Files:**
- Create: `global.json`, `nuget.config`, `Directory.Build.props`, `Directory.Packages.props`, `LICENSE`, `CLAUDE.md`, `docs/ROADMAP.md`
- Modify: `README.md`, `.gitignore`
- Create: `CoffeePdf.sln`, `CoffeePdf.Core/CoffeePdf.Core.csproj`, `CoffeePdf.Core/Files/PdfFile.cs`
- Test: `CoffeePdf.Core.Tests/CoffeePdf.Core.Tests.csproj`, `CoffeePdf.Core.Tests/Files/PdfFileTests.cs`, `CoffeePdf.Core.Tests/ArchitectureTests.cs`

**Interfaces:**
- Produces: `CoffeePdf.Core.Files.PdfFile.IsPdf(string path) : bool`, and `PdfFile.HeaderWindow` (`const int`, 1024).

- [ ] **Step 1: Write the build settings**

`global.json`:
```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

`nuget.config`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- One package source, so central package management doesn't warn (NU1507) and a developer's own feeds don't leak in. -->
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- The app's runtime packages (spec §4.3): the Healdan Helper's Avalonia, and the licensed PdfViewer. -->
    <PackageVersion Include="Avalonia" Version="12.1.4" />
    <PackageVersion Include="Avalonia.Desktop" Version="12.1.4" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="12.1.4" />
    <PackageVersion Include="Avalonia.Controls.PdfViewer" Version="12.0.1" />
    <PackageVersion Include="AvaloniaUI.Licensing" Version="3.1.3" />
  </ItemGroup>
  <ItemGroup>
    <!-- Test-only packages, at the Helper's versions. Pkcs signs the signed-PDF fixture. -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageVersion Include="Avalonia.Headless" Version="12.1.4" />
    <PackageVersion Include="Avalonia.Skia" Version="12.1.4" />
    <PackageVersion Include="System.Security.Cryptography.Pkcs" Version="10.0.14" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the front matter**

`LICENSE`:
```
Copyright (c) 2026 coffeemuse. All rights reserved.

CoffeePDF is proprietary software. No licence is granted to use, copy, modify or distribute it, in source or binary
form, without the copyright holder's written permission.

This notice is temporary. The intended licence is MIT, settled before the repository is made public (see
docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md, section 12).
```

`README.md` (replaces the one-line file):
````markdown
# CoffeePDF

An everyday PDF app for macOS, in the spirit of Preview, that works directly with documents stored and versioned in
[Healdan](https://github.com/Healdan-Project/healdan), through the Healdan Helper.

**Status:** pre-release, in Phase 0 (the foundation spike). See [the roadmap](docs/ROADMAP.md) and
[the v1 design](docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md).

## Building

You need the .NET 10 SDK (`global.json` pins it). The PdfViewer needs an Avalonia licence that covers it, whose key
the build reads from `AVALONIA_LICENSE_KEY`. Without the key everything builds, the PdfViewer's tests are skipped,
and the app can't show a PDF.

```bash
export AVALONIA_LICENSE_KEY=…   # from portal.avaloniaui.net; never commit it
dotnet test CoffeePdf.sln
dotnet run --project CoffeePdf -- path/to/file.pdf
packaging/mac/package.sh 0.0.1   # CoffeePDF.app and its .dmg, on a Mac
```

## Licence

Proprietary for now; see [LICENSE](LICENSE).
````

`CLAUDE.md`:
````markdown
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
````

`docs/ROADMAP.md`:
```markdown
# CoffeePDF roadmap

- **Status:** v1 designed on 2026-10-29 ([spec](superpowers/specs/2026-10-29-coffeepdf-v1-design.md)). Phase 0 is
  under way ([plan](superpowers/plans/2026-10-29-phase-0-foundation-spike.md)).

## v1: read and mark up

| Phase | What | Status |
|---|---|---|
| 0. Foundation spike | The solution, CI, the licence key, a window that shows a PDF, a signed and notarised package; the spec's §14 checks | in progress |
| 1. Standalone app | Viewing, markup, forms, printing, Preferences, Open Recent, the default PDF app | not started |
| 2. Healdan in CoffeePDF | The channel client, the window modes and the flows of spec §6, against a fake Helper | not started |
| 3. Healdan's milestone | Spec §4.4, through its own spec in Healdan's repository | not started |
| 4. Acceptance | Spec §2.5 on the owner's Mac against the owner's server, then v1.0 | not started |

## v2

Each gets its own design session first.

- Page operations: rotate, delete, reorder, insert, combine, split and extract.
- Signatures and true redaction.
- Legal tooling: Bates numbering, exhibit stamps, a doc # footer stamp, bundles from several Healdan documents.

## Backlog

- OCR and comparing versions, which may belong on Healdan's server.
- Linux, after the Helper's Linux phase. Windows later.

## Open decisions for the owner

- The licence, before the repository goes public (spec §12).
```

`.gitignore`: append at the end (the Visual Studio template's `[Oo]ut/` already covers `packaging/mac/out/`):
```
# macOS
.DS_Store
```

- [ ] **Step 3: Create the solution and the core's projects**

`CoffeePdf.Core/CoffeePdf.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Everything CoffeePDF decides that doesn't need a window (spec §4.3). It references no Avalonia assembly. -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>CoffeePdf.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

`CoffeePdf.Core.Tests/CoffeePdf.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <ProjectReference Include="../CoffeePdf.Core/CoffeePdf.Core.csproj" />
  </ItemGroup>
</Project>
```

Run:
```bash
dotnet new sln --name CoffeePdf --format sln
dotnet sln CoffeePdf.sln add CoffeePdf.Core/CoffeePdf.Core.csproj CoffeePdf.Core.Tests/CoffeePdf.Core.Tests.csproj
```
Expected: `CoffeePdf.sln` is created, and both projects are added.

- [ ] **Step 4: Write the failing tests**

`CoffeePdf.Core.Tests/Files/PdfFileTests.cs`:
```csharp
using System.Text;
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Tests.Files;

public sealed class PdfFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-core-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(content));
        return path;
    }

    [Fact]
    public void A_file_starting_with_the_header_is_a_pdf() =>
        Assert.True(PdfFile.IsPdf(Write("a.pdf", "%PDF-1.7\n%%EOF\n")));

    [Fact]
    public void Junk_before_the_header_is_allowed_within_the_window() =>
        Assert.True(PdfFile.IsPdf(Write("junk.pdf", new string('x', PdfFile.HeaderWindow - 5) + "%PDF-1.7\n")));

    [Fact]
    public void A_header_past_the_window_isnt_found() =>
        Assert.False(PdfFile.IsPdf(Write("late.pdf", new string('x', PdfFile.HeaderWindow - 4) + "%PDF-1.7\n")));

    [Fact]
    public void The_name_doesnt_count() =>
        Assert.False(PdfFile.IsPdf(Write("notes.pdf", "just some text")));

    [Fact]
    public void A_missing_file_isnt_a_pdf() =>
        Assert.False(PdfFile.IsPdf(Path.Combine(_dir, "missing.pdf")));

    [Fact]
    public void A_folder_isnt_a_pdf() =>
        Assert.False(PdfFile.IsPdf(_dir));
}
```

`CoffeePdf.Core.Tests/ArchitectureTests.cs`. This is a guard: it passes as soon as it compiles, and fails the day someone adds Avalonia to the core.
```csharp
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Tests;

/// <summary>Spec §4.3 and §14 item 8: the core stays free of Avalonia, so its logic never depends on the PdfViewer.</summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void The_core_references_no_Avalonia_assembly()
    {
        var references = typeof(PdfFile).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(references, name => name.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 5: Run the tests to check they fail**

Run: `dotnet test CoffeePdf.sln`
Expected: the build fails with `CS0103`/`CS0246` because `PdfFile` doesn't exist.

- [ ] **Step 6: Implement `PdfFile`**

`CoffeePdf.Core/Files/PdfFile.cs`:
```csharp
namespace CoffeePdf.Core.Files;

/// <summary>Tells a PDF from other files by its header, as PDF readers do, rather than by its name.</summary>
public static class PdfFile
{
    /// <summary>How far into a file the <c>%PDF-</c> header may start: readers accept junk before it.</summary>
    public const int HeaderWindow = 1024;

    private static ReadOnlySpan<byte> Header => "%PDF-"u8;

    /// <summary>Whether <paramref name="path"/> is a readable file with a PDF header in its first 1024 bytes.</summary>
    public static bool IsPdf(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[HeaderWindow];
            var read = stream.ReadAtLeast(buffer, HeaderWindow, throwOnEndOfStream: false);
            return buffer.AsSpan(0, read).IndexOf(Header) >= 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 7: Run the tests to check they pass**

Run: `dotnet test CoffeePdf.sln`
Expected: 7 passed, 0 failed.

- [ ] **Step 8: Commit**, including the spec and this plan, which aren't committed yet:

```bash
git add global.json nuget.config Directory.Build.props Directory.Packages.props LICENSE README.md CLAUDE.md .gitignore \
  docs/ CoffeePdf.sln CoffeePdf.Core CoffeePdf.Core.Tests
git commit -m "Add the solution, build settings, front matter and PdfFile"
```

---

### Task 2: `WindowChoice`

**Files:**
- Create: `CoffeePdf.Core/Documents/WindowChoice.cs`
- Test: `CoffeePdf.Core.Tests/Documents/WindowChoiceTests.cs`

**Interfaces:**
- Produces:
  - `CoffeePdf.Core.Documents.WindowAction`, an enum: `Activate`, `LoadInto`, `OpenNew`.
  - `CoffeePdf.Core.Documents.WindowChoice`, a `readonly record struct` with `(WindowAction Action, int Index)` and `static WindowChoice For(IReadOnlyList<string?> windowPaths, string path)`. `Index` is -1 for `OpenNew`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Documents/WindowChoiceTests.cs`:
```csharp
using CoffeePdf.Core.Documents;

namespace CoffeePdf.Core.Tests.Documents;

public sealed class WindowChoiceTests
{
    [Fact]
    public void With_no_windows_a_new_one_opens() =>
        Assert.Equal(new WindowChoice(WindowAction.OpenNew, -1), WindowChoice.For([], "/docs/a.pdf"));

    [Fact]
    public void The_window_showing_the_file_comes_forward() =>
        Assert.Equal(new WindowChoice(WindowAction.Activate, 1), WindowChoice.For(["/docs/b.pdf", "/docs/a.pdf"], "/docs/a.pdf"));

    [Fact]
    public void Another_spelling_of_the_same_path_is_the_same_file() =>
        Assert.Equal(new WindowChoice(WindowAction.Activate, 0), WindowChoice.For(["/docs/a.pdf"], "/docs/sub/../a.pdf"));

    [Fact]
    public void An_empty_window_takes_the_file() =>
        Assert.Equal(new WindowChoice(WindowAction.LoadInto, 1), WindowChoice.For(["/docs/b.pdf", null], "/docs/a.pdf"));

    [Fact]
    public void The_window_showing_the_file_wins_over_an_empty_one() =>
        Assert.Equal(new WindowChoice(WindowAction.Activate, 1), WindowChoice.For([null, "/docs/a.pdf"], "/docs/a.pdf"));

    [Fact]
    public void Windows_showing_other_files_are_left_alone() =>
        Assert.Equal(new WindowChoice(WindowAction.OpenNew, -1), WindowChoice.For(["/docs/b.pdf", "/docs/c.pdf"], "/docs/a.pdf"));
}
```

- [ ] **Step 2: Run the tests to check they fail**

Run: `dotnet test CoffeePdf.sln --filter FullyQualifiedName~WindowChoiceTests`
Expected: the build fails because `WindowChoice` doesn't exist.

- [ ] **Step 3: Implement `WindowChoice`**

`CoffeePdf.Core/Documents/WindowChoice.cs`:
```csharp
namespace CoffeePdf.Core.Documents;

/// <summary>What opening a PDF does to the open windows.</summary>
public enum WindowAction
{
    /// <summary>Bring forward the window that already shows the file.</summary>
    Activate,

    /// <summary>Load the file into an empty window, such as the one shown at launch.</summary>
    LoadInto,

    /// <summary>Open a new window for the file.</summary>
    OpenNew,
}

/// <summary>Where a PDF being opened goes (spec §2.1: one window per document).</summary>
/// <param name="Action">What to do.</param>
/// <param name="Index">The window's index in the list given to <see cref="For"/>, or -1 for <see cref="WindowAction.OpenNew"/>.</param>
public readonly record struct WindowChoice(WindowAction Action, int Index)
{
    /// <summary>
    /// Chooses for <paramref name="path"/>, given each open window's file in order (null for an empty window). Paths
    /// are compared in full, so <c>/docs/sub/../a.pdf</c> and <c>/docs/a.pdf</c> are the same file.
    /// </summary>
    public static WindowChoice For(IReadOnlyList<string?> windowPaths, string path)
    {
        var full = Path.GetFullPath(path);
        for (var i = 0; i < windowPaths.Count; i++)
            if (windowPaths[i] is { } open && Path.GetFullPath(open) == full)
                return new WindowChoice(WindowAction.Activate, i);
        for (var i = 0; i < windowPaths.Count; i++)
            if (windowPaths[i] is null)
                return new WindowChoice(WindowAction.LoadInto, i);
        return new WindowChoice(WindowAction.OpenNew, -1);
    }
}
```

- [ ] **Step 4: Run the tests to check they pass**

Run: `dotnet test CoffeePdf.sln`
Expected: 13 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Documents CoffeePdf.Core.Tests/Documents
git commit -m "Add WindowChoice: one window per document"
```

---

### Task 3: The app, the licence key and a window that shows a PDF

**Files:**
- Create: `AvaloniaLicense.props`
- Create: `CoffeePdf/CoffeePdf.csproj`, `CoffeePdf/Program.cs`, `CoffeePdf/App.axaml`, `CoffeePdf/App.axaml.cs`, `CoffeePdf/Windows/DocumentWindow.axaml`, `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Create: `CoffeePdf.Tests/CoffeePdf.Tests.csproj`, `CoffeePdf.Tests/Headless.cs`, `CoffeePdf.Tests/PdfViewerFactAttribute.cs`, `CoffeePdf.Tests/Viewers.cs`, `CoffeePdf.Tests/Fixtures/MinimalPdf.cs`
- Test: `CoffeePdf.Tests/Fixtures/MinimalPdfTests.cs`, `CoffeePdf.Tests/Windows/DocumentWindowTests.cs`
- Modify: `CoffeePdf.sln`

**Interfaces:**
- Consumes: `PdfFile.IsPdf`, `WindowChoice.For`, `WindowAction` (Tasks 1–2).
- Produces:
  - **`CoffeePdf.App`:** `public void OpenFile(string path)`.
  - **`CoffeePdf.Windows.DocumentWindow`:** `public string? FilePath`, `public void Load(string path)`, and the generated `internal PdfViewer Viewer` field.
  - **Test infrastructure:**
    - `Headless.RunAsync<T>(Func<Task<T>>) : Task<T>`
    - `PdfViewerFactAttribute`
    - `Viewers.OpenAsync(string path) : Task<PdfViewer>`
    - `Viewers.ShowAndLoadAsync(PdfViewer, Action load) : Task`
    - `MinimalPdf.Build(bool withForeignNote = false) : byte[]`
    - `MinimalPdf.Objects(bool withForeignNote, IReadOnlyList<string> extraAnnotations, string? acroForm) : List<string>`
    - `MinimalPdf.Write(IReadOnlyList<string> objects) : byte[]`
    - `MinimalPdf.ForeignNoteAuthor` and `MinimalPdf.ForeignNoteContents`

- [ ] **Step 1: Write the licence props**

`AvaloniaLicense.props`:
```xml
<Project>
  <!--
    The Avalonia Pro licence key the PdfViewer checks (spec §10), from the AVALONIA_LICENSE_KEY environment variable;
    never committed. Imported by the projects that run the PdfViewer: the app and its tests. With the key, the build
    also defines AVALONIA_LICENSED, which PdfViewerFact reads.
  -->
  <ItemGroup Condition="'$(AVALONIA_LICENSE_KEY)' != ''">
    <AvaloniaUILicenseKey Include="$(AVALONIA_LICENSE_KEY)" />
  </ItemGroup>
  <PropertyGroup Condition="'$(AVALONIA_LICENSE_KEY)' != ''">
    <DefineConstants>$(DefineConstants);AVALONIA_LICENSED</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AvaloniaUI.Licensing" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the test project, with the fixture builder's failing test**

`CoffeePdf.Tests/CoffeePdf.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- The app's tests, with Avalonia headless, and Phase 0's checks of the PdfViewer (spec §14). -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <Import Project="../AvaloniaLicense.props" />
  <ItemGroup>
    <PackageReference Include="Avalonia.Headless" />
    <PackageReference Include="Avalonia.Skia" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <ProjectReference Include="../CoffeePdf/CoffeePdf.csproj" />
  </ItemGroup>
</Project>
```

`CoffeePdf.Tests/Fixtures/MinimalPdfTests.cs`:
```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Tests.Fixtures;

public sealed class MinimalPdfTests
{
    [Fact]
    public void Every_cross_reference_entry_points_at_its_object()
    {
        var text = Encoding.ASCII.GetString(MinimalPdf.Build(withForeignNote: true));
        var xref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n").Groups[1].Value);
        Assert.StartsWith("xref\n", text[xref..]);
        var entries = Regex.Matches(text[xref..], @"(\d{10}) 00000 n\r\n");
        Assert.Equal(6, entries.Count);
        for (var i = 0; i < entries.Count; i++)
            Assert.StartsWith($"{i + 1} 0 obj\n", text[int.Parse(entries[i].Groups[1].Value)..]);
    }

    [Fact]
    public void The_foreign_note_is_on_the_page_only_when_asked_for()
    {
        Assert.DoesNotContain("/Annots", Encoding.ASCII.GetString(MinimalPdf.Build()));
        Assert.Contains("/Annots [6 0 R]", Encoding.ASCII.GetString(MinimalPdf.Build(withForeignNote: true)));
    }
}
```

- [ ] **Step 3: Create the app project so the test project resolves**

`CoffeePdf/CoffeePdf.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- The Avalonia app (spec §4.3): document windows hosting the PdfViewer. Everything testable is in CoffeePdf.Core. -->
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>CoffeePdf</RootNamespace>
    <!-- The binary the bundle starts (packaging/mac/Info.plist.in's CFBundleExecutable). -->
    <AssemblyName>CoffeePDF</AssemblyName>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    <BuiltInComInteropSupport>false</BuiltInComInteropSupport>
  </PropertyGroup>
  <Import Project="../AvaloniaLicense.props" />
  <ItemGroup>
    <PackageReference Include="Avalonia" />
    <PackageReference Include="Avalonia.Desktop" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="Avalonia.Controls.PdfViewer" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="CoffeePdf.Tests" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../CoffeePdf.Core/CoffeePdf.Core.csproj" />
  </ItemGroup>
</Project>
```

`CoffeePdf/Program.cs`:
```csharp
using Avalonia;
using Avalonia.Controls;

namespace CoffeePdf;

/// <summary>Starts CoffeePDF. On macOS one instance runs, and the system hands it every PDF opened (spec §4.3).</summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnLastWindowClose);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
```

`CoffeePdf/App.axaml`:
```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="CoffeePdf.App"
             Name="CoffeePDF"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
    <!-- Without its theme the PdfViewer draws nothing. -->
    <StyleInclude Source="avares://Avalonia.Controls.PdfViewer/Themes/Default.axaml" />
  </Application.Styles>
</Application>
```

`CoffeePdf/App.axaml.cs`:
```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Windows;

namespace CoffeePdf;

public partial class App : Application
{
    /// <summary>
    /// How long a launch waits for a file before showing an empty window: a launch from Finder delivers the file as
    /// an event just after the start.
    /// </summary>
    private static readonly TimeSpan EmptyWindowDelay = TimeSpan.FromMilliseconds(500);

    private readonly List<DocumentWindow> _windows = [];

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Subscribed before anything awaits, or the file Finder launched the app with is missed.
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
                activatable.Activated += OnActivated;
            foreach (var arg in desktop.Args ?? [])
                OpenFile(arg);
            DispatcherTimer.RunOnce(OpenEmptyWindowIfNone, EmptyWindowDelay);
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Shows <paramref name="path"/>: in its window if one shows it, else in an empty window, else in a new one.</summary>
    public void OpenFile(string path)
    {
        if (!PdfFile.IsPdf(path))
            return;
        var choice = WindowChoice.For(_windows.Select(w => w.FilePath).ToList(), path);
        switch (choice.Action)
        {
            case WindowAction.Activate:
                _windows[choice.Index].Activate();
                break;
            case WindowAction.LoadInto:
                _windows[choice.Index].Load(path);
                _windows[choice.Index].Activate();
                break;
            default:
                var window = NewWindow();
                window.Load(path);
                window.Show();
                break;
        }
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e is not FileActivatedEventArgs { Kind: ActivationKind.File } opened)
            return;
        foreach (var file in opened.Files)
            if (file.TryGetLocalPath() is { } path)
                OpenFile(path);
    }

    private void OpenEmptyWindowIfNone()
    {
        if (_windows.Count == 0)
            NewWindow().Show();
    }

    private DocumentWindow NewWindow()
    {
        var window = new DocumentWindow();
        window.Closed += (_, _) => _windows.Remove(window);
        _windows.Add(window);
        return window;
    }
}
```

`CoffeePdf/Windows/DocumentWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:pdf="using:Avalonia.Controls"
        x:Class="CoffeePdf.Windows.DocumentWindow"
        Title="CoffeePDF"
        Width="1000" Height="760">
  <NativeMenu.Menu>
    <NativeMenu>
      <NativeMenuItem Header="File">
        <NativeMenu>
          <NativeMenuItem Header="Open…" Gesture="Meta+O" Click="OnOpen" />
          <NativeMenuItem Header="Save" Gesture="Meta+S" Click="OnSave" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Close" Gesture="Meta+W" Click="OnClose" />
        </NativeMenu>
      </NativeMenuItem>
    </NativeMenu>
  </NativeMenu.Menu>
  <!-- AutoSave stays off (spec §6.4): each edit would otherwise become a save. -->
  <pdf:PdfViewer x:Name="Viewer"
                 ViewMode="Continuous"
                 SidebarMode="Thumbnails"
                 AllowDocumentSaving="True"
                 AutoSave="False" />
</Window>
```

`CoffeePdf/Windows/DocumentWindow.axaml.cs`:
```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CoffeePdf.Windows;

/// <summary>One PDF's window (spec §2.1), hosting the PdfViewer. It's empty until a file is loaded.</summary>
public partial class DocumentWindow : Window
{
    public DocumentWindow() => InitializeComponent();

    /// <summary>The file shown, or null while the window is empty.</summary>
    public string? FilePath => Viewer.Source;

    /// <summary>Shows <paramref name="path"/>. The viewer loads it, and its saves go back to it.</summary>
    public void Load(string path)
    {
        Viewer.Source = path;
        Title = Path.GetFileName(path);
    }

    private async void OnOpen(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.Pdf],
        });
        foreach (var file in files)
            if (file.TryGetLocalPath() is { } path)
                ((App)Application.Current!).OpenFile(path);
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        try
        {
            await Viewer.SaveAsync();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Phase 1 gives saving its own error handling. Until then the console says what went wrong.
            Console.Error.WriteLine($"CoffeePDF couldn't save {FilePath}: {error.Message}");
        }
    }

    private void OnClose(object? sender, EventArgs e) => Close();
}
```

Add the projects to the solution:
```bash
dotnet sln CoffeePdf.sln add CoffeePdf/CoffeePdf.csproj CoffeePdf.Tests/CoffeePdf.Tests.csproj
```

- [ ] **Step 4: Run the fixture test to check it fails**

Run: `dotnet test CoffeePdf.sln --filter FullyQualifiedName~MinimalPdfTests`
Expected: the build fails because `MinimalPdf` doesn't exist.

If the build fails for another reason, check these before going on:
- `PdfViewer.Source` isn't a `string?`. Adapt `FilePath` and `Load`, and record the type for the findings.
- The `pdf:` prefix doesn't resolve. Check `Avalonia.Controls.PdfViewer.xml` in the NuGet cache for the control's namespace.

- [ ] **Step 5: Write `MinimalPdf`**

`CoffeePdf.Tests/Fixtures/MinimalPdf.cs`:
```csharp
using System.Text;

namespace CoffeePdf.Tests.Fixtures;

/// <summary>
/// Small, valid PDFs for the tests, written here rather than committed: one Letter page of text, optionally with a
/// sticky note another app wrote. Offsets are counted as the file is written, so the cross-reference table is exact.
/// </summary>
internal static class MinimalPdf
{
    public const string ForeignNoteAuthor = "Acrobat User";
    public const string ForeignNoteContents = "From another app";

    private const string Text = "BT /F1 24 Tf 72 700 Td (CoffeePDF fixture) Tj ET";

    /// <summary>The whole file.</summary>
    public static byte[] Build(bool withForeignNote = false) =>
        Write(Objects(withForeignNote, extraAnnotations: [], acroForm: null));

    /// <summary>
    /// The objects, numbered from 1: the catalog, the page tree, the page, its text, its font, and a sticky note by
    /// <see cref="ForeignNoteAuthor"/> (6), which is on the page only when <paramref name="withForeignNote"/>. Callers
    /// may append objects (7 on), and list them in <paramref name="extraAnnotations"/> and <paramref name="acroForm"/>.
    /// </summary>
    public static List<string> Objects(bool withForeignNote, IReadOnlyList<string> extraAnnotations, string? acroForm)
    {
        var annotations = new List<string>();
        if (withForeignNote)
            annotations.Add("6 0 R");
        annotations.AddRange(extraAnnotations);
        var annots = annotations.Count > 0 ? $" /Annots [{string.Join(' ', annotations)}]" : "";
        var form = acroForm is null ? "" : $" /AcroForm {acroForm}";
        return
        [
            $"<< /Type /Catalog /Pages 2 0 R{form} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R{annots} >>",
            $"<< /Length {Text.Length} >>\nstream\n{Text}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Type /Annot /Subtype /Text /Rect [72 600 96 624] /Contents ({ForeignNoteContents}) /T ({ForeignNoteAuthor}) /F 4 >>",
        ];
    }

    /// <summary>Writes <paramref name="objects"/> (object n is index n − 1) as a PDF with one cross-reference section.</summary>
    public static byte[] Write(IReadOnlyList<string> objects)
    {
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f\r\n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:D10} 00000 n\r\n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
```

- [ ] **Step 6: Run the fixture tests to check they pass**

Run: `dotnet test CoffeePdf.sln --filter FullyQualifiedName~MinimalPdfTests`
Expected: 2 passed.

- [ ] **Step 7: Build without the key, and keep a keyless build working**

Run: `env -u AVALONIA_LICENSE_KEY dotnet build CoffeePdf.sln 2>&1 | grep -iE 'warning|error' | sort -u`
Expected: either no output, or the licensing package's warning about the missing key.

If that warning fails the build (`TreatWarningsAsErrors`):
1. Note its code, the `XXXX0000:` before the message.
2. Add this to `AvaloniaLicense.props`, writing the code you saw in place of `XXXX0000`.
3. Run the build again. Expected: `Build succeeded`.

```xml
  <PropertyGroup Condition="'$(AVALONIA_LICENSE_KEY)' == ''">
    <!-- A build without the key (a contributor without a licence, CI without the secret) still builds and tests. -->
    <WarningsNotAsErrors>$(WarningsNotAsErrors);XXXX0000</WarningsNotAsErrors>
  </PropertyGroup>
```

Record the warning's exact text and code for the findings (Task 7).

- [ ] **Step 8: Write the test infrastructure and the window's failing test**

`CoffeePdf.Tests/Headless.cs`:
```csharp
using Avalonia;
using Avalonia.Headless;

namespace CoffeePdf.Tests;

/// <summary>
/// Avalonia without a display, for the tests: CoffeePDF's own App, whose themes the PdfViewer needs to draw, with
/// Skia's real drawing. One session serves the whole run, as Avalonia sets up once.
/// </summary>
public static class Headless
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(Headless)));

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Runs <paramref name="action"/> on the session's UI thread, and waits for what it returns.</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> action) => Session.Value.Dispatch(action, CancellationToken.None);
}
```

`CoffeePdf.Tests/PdfViewerFactAttribute.cs`:
```csharp
namespace CoffeePdf.Tests;

/// <summary>
/// A test that needs the PdfViewer, which runs only in a build with the Avalonia licence key (AvaloniaLicense.props
/// defines AVALONIA_LICENSED then). Without it the test is skipped, not failed, so a build without the key still tests
/// everything else (spec §8).
/// </summary>
public sealed class PdfViewerFactAttribute : FactAttribute
{
    public PdfViewerFactAttribute()
    {
#if !AVALONIA_LICENSED
        Skip = "Needs the Avalonia licence key: set AVALONIA_LICENSE_KEY and build again";
#endif
    }
}
```

`CoffeePdf.Tests/Viewers.cs`:
```csharp
using Avalonia.Controls;

namespace CoffeePdf.Tests;

/// <summary>A PdfViewer in a headless window, for the tests that check its behaviour. Call these on the UI thread.</summary>
internal static class Viewers
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Shows a viewer that may save, and loads <paramref name="path"/> through <c>Source</c>, as DocumentWindow does,
    /// so <c>SaveAsync</c> saves in place.
    /// </summary>
    public static async Task<PdfViewer> OpenAsync(string path)
    {
        var viewer = new PdfViewer { AllowDocumentSaving = true, AllowAnnotationEditing = true, AutoSave = false };
        await ShowAndLoadAsync(viewer, () => viewer.Source = path);
        return viewer;
    }

    /// <summary>
    /// Shows <paramref name="viewer"/> (in a window of its own unless it's in one already), runs
    /// <paramref name="load"/>, and waits until the document has loaded.
    /// </summary>
    public static async Task ShowAndLoadAsync(PdfViewer viewer, Action load)
    {
        var loaded = new TaskCompletionSource();
        viewer.DocumentLoaded += (_, _) => loaded.TrySetResult();
        viewer.LoadError += (_, e) => loaded.TrySetException(new IOException(e.Message, e.Exception));
        if (TopLevel.GetTopLevel(viewer) is null)
            new Window { Content = viewer, Width = 900, Height = 700 }.Show();
        load();
        await loaded.Task.WaitAsync(LoadTimeout);
    }
}
```

`CoffeePdf.Tests/Windows/DocumentWindowTests.cs`:
```csharp
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

[Trait("Category", "PdfViewer")]
public sealed class DocumentWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [PdfViewerFact]
    public async Task A_window_shows_the_pdf_it_loads()
    {
        var path = Path.Combine(_dir, "fixture.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());

        var (title, pages, shown) = await Headless.RunAsync(async () =>
        {
            var window = new DocumentWindow();
            window.Show();
            await Viewers.ShowAndLoadAsync(window.Viewer, () => window.Load(path));
            return (window.Title, window.Viewer.PageCount, window.FilePath);
        });

        Assert.Equal("fixture.pdf", title);
        Assert.Equal(1, pages);
        Assert.Equal(path, shown);
    }
}
```

- [ ] **Step 9: Run the tests without the key, then with it**

Run: `env -u AVALONIA_LICENSE_KEY dotnet test CoffeePdf.sln`
Expected: everything passes, and `A_window_shows_the_pdf_it_loads` is **skipped**.

Run, with the owner's key in the environment: `dotnet test CoffeePdf.sln`
Expected: everything passes, including `A_window_shows_the_pdf_it_loads`.

**If it fails with `AvaloniaLicensingException` although the key is set, stop.** The test host may not be an "application head" the licence covers.
1. Record the exception's message for the findings.
2. Tell the owner. The fallback is to run the PdfViewer's checks from the app itself, with a `--probe` switch, and that needs the owner's go-ahead because it changes this plan.

Record the outcome for the findings either way.

- [ ] **Step 10: See the app**, on the Mac with the key set:

```bash
printf 'CoffeePDF smoke test\n' > "$TMPDIR/coffeepdf-smoke.txt"
/usr/sbin/cupsfilter "$TMPDIR/coffeepdf-smoke.txt" > "$TMPDIR/coffeepdf-smoke.pdf" 2>/dev/null
dotnet run --project CoffeePdf -- "$TMPDIR/coffeepdf-smoke.pdf"
```

Expected:
- A window titled `coffeepdf-smoke.pdf` shows the page, with the PdfViewer's toolbar and thumbnails.
- The menu bar has CoffeePDF and File, and File has Open…, Save and Close with ⌘O, ⌘S and ⌘W.
- Adding a highlight and pressing ⌘S saves without an error on the console.

Quit with ⌘Q.

- [ ] **Step 11: Commit**

```bash
git add AvaloniaLicense.props CoffeePdf CoffeePdf.Tests CoffeePdf.sln
git commit -m "Add the app: a document window hosting the PdfViewer, and the licence key from the environment"
```

---

### Task 4: How the PdfViewer saves (spec §14 items 1–3)

**Files:**
- Create: `CoffeePdf.Tests/Fixtures/SignedPdf.cs`, `CoffeePdf.Tests/Probe.cs`
- Test: `CoffeePdf.Tests/Fixtures/SignedPdfTests.cs`, `CoffeePdf.Tests/Viewer/ViewerSaveTests.cs`, `CoffeePdf.Tests/Viewer/ViewerProbes.cs`
- Modify: `CoffeePdf.Tests/CoffeePdf.Tests.csproj`

**Interfaces:**
- Consumes: `MinimalPdf.Objects`, `MinimalPdf.Write`, `MinimalPdf.Build`, `Headless.RunAsync`, `Viewers.OpenAsync`, `PdfViewerFactAttribute` (Task 3).
- Produces:
  - `SignedPdf.Build() : byte[]`
  - `SignedPdf.VerifyFirstSignature(byte[] pdf)`, which throws `CryptographicException` when the first signature doesn't verify.
  - `Probe.Keep(string path, string name)`

- [ ] **Step 1: Add the CMS package**

In `CoffeePdf.Tests/CoffeePdf.Tests.csproj`, add to the `PackageReference` item group:
```xml
    <PackageReference Include="System.Security.Cryptography.Pkcs" />
```

- [ ] **Step 2: Write the signed fixture's failing test**

`CoffeePdf.Tests/Fixtures/SignedPdfTests.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Tests.Fixtures;

public sealed class SignedPdfTests
{
    [Fact]
    public void The_signature_covers_everything_but_its_own_value()
    {
        var pdf = SignedPdf.Build();
        var range = Regex.Match(Encoding.ASCII.GetString(pdf), @"/ByteRange \[(\d{10}) (\d{10}) (\d{10}) (\d{10})\]");
        var r = Enumerable.Range(1, 4).Select(i => int.Parse(range.Groups[i].Value)).ToArray();
        Assert.Equal(0, r[0]);
        Assert.Equal((byte)'<', pdf[r[1]]);
        Assert.Equal((byte)'>', pdf[r[2] - 1]);
        Assert.Equal(pdf.Length, r[2] + r[3]);
    }

    [Fact]
    public void The_signature_verifies() => SignedPdf.VerifyFirstSignature(SignedPdf.Build());

    [Fact]
    public void A_changed_byte_breaks_the_signature()
    {
        var pdf = SignedPdf.Build();
        var at = Encoding.ASCII.GetString(pdf).IndexOf("CoffeePDF fixture", StringComparison.Ordinal);
        pdf[at] = (byte)'K';
        Assert.ThrowsAny<CryptographicException>(() => SignedPdf.VerifyFirstSignature(pdf));
    }
}
```

- [ ] **Step 3: Run it to check it fails**

Run: `dotnet test CoffeePdf.sln --filter FullyQualifiedName~SignedPdfTests`
Expected: the build fails because `SignedPdf` doesn't exist.

- [ ] **Step 4: Write `SignedPdf`**

`CoffeePdf.Tests/Fixtures/SignedPdf.cs`:
```csharp
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Tests.Fixtures;

/// <summary>
/// The minimal PDF, signed as Acrobat signs: an approval signature (not a certification, so later annotations are
/// allowed), detached PKCS#7 over the whole file except the signature's own value, with a throwaway self-signed
/// certificate. For spec §14 item 1: a save must keep it.
/// </summary>
internal static class SignedPdf
{
    /// <summary>Room for the CMS blob in hex, about 3 KB of it here; the rest stays zeros, as signers leave it.</summary>
    private const int ContentsHexLength = 16384;

    private const string ByteRangePlaceholder = "[0000000000 0000000000 0000000000 0000000000]";

    public static byte[] Build()
    {
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: ["7 0 R"],
            acroForm: "<< /Fields [7 0 R] /SigFlags 3 >>");
        objects.Add("<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P 3 0 R /V 8 0 R >>");
        objects.Add($"<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange {ByteRangePlaceholder} /Contents <{new string('0', ContentsHexLength)}> /M (D:20261029000000Z) >>");
        var pdf = MinimalPdf.Write(objects);
        var text = Encoding.ASCII.GetString(pdf);

        var hexStart = text.IndexOf("/Contents <", StringComparison.Ordinal) + "/Contents ".Length; // at '<'
        var hexEnd = hexStart + 1 + ContentsHexLength + 1;                                          // just after '>'
        var byteRange = $"[{0:D10} {hexStart:D10} {hexEnd:D10} {pdf.Length - hexEnd:D10}]";
        Encoding.ASCII.GetBytes(byteRange).CopyTo(pdf, text.IndexOf(ByteRangePlaceholder, StringComparison.Ordinal));

        var signature = Sign([.. pdf[..hexStart], .. pdf[hexEnd..]]);
        Encoding.ASCII.GetBytes(Convert.ToHexString(signature)).CopyTo(pdf, hexStart + 1);
        return pdf;
    }

    /// <summary>
    /// Checks the first signature in <paramref name="pdf"/> against the bytes its /ByteRange names: an incremental
    /// update leaves that signature first, and its bytes alone. Throws <see cref="CryptographicException"/> if it fails.
    /// </summary>
    public static void VerifyFirstSignature(byte[] pdf)
    {
        var text = Encoding.ASCII.GetString(pdf);
        var range = Regex.Match(text, @"/ByteRange \[(\d+) (\d+) (\d+) (\d+)\]");
        var r = Enumerable.Range(1, 4).Select(i => int.Parse(range.Groups[i].Value)).ToArray();
        byte[] signed = [.. pdf[r[0]..(r[0] + r[1])], .. pdf[r[2]..(r[2] + r[3])]];
        var blob = Convert.FromHexString(text[(r[1] + 1)..(r[2] - 1)]);
        AsnDecoder.ReadEncodedValue(blob, AsnEncodingRules.BER, out _, out _, out var length);
        var cms = new SignedCms(new ContentInfo(signed), detached: true);
        cms.Decode(blob.AsSpan(0, length));
        cms.CheckSignature(verifySignatureOnly: true);
    }

    private static byte[] Sign(byte[] data)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=CoffeePDF test signer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var cms = new SignedCms(new ContentInfo(data), detached: true);
        cms.ComputeSignature(new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate));
        return cms.Encode();
    }
}
```

- [ ] **Step 5: Run it to check it passes**

Run: `dotnet test CoffeePdf.sln --filter FullyQualifiedName~SignedPdfTests`
Expected: 3 passed.

- [ ] **Step 6: Write the save tests and the probes**

`CoffeePdf.Tests/Probe.cs`:
```csharp
namespace CoffeePdf.Tests;

/// <summary>Phase 0's probes (spec §14): files kept for the owner to check by hand, in Acrobat or Preview.</summary>
internal static class Probe
{
    /// <summary>Copies <paramref name="path"/> into <c>COFFEEPDF_PROBE_OUT</c> as <paramref name="name"/>, when that's set.</summary>
    public static void Keep(string path, string name)
    {
        if (Environment.GetEnvironmentVariable("COFFEEPDF_PROBE_OUT") is not { Length: > 0 } outDir)
            return;
        Directory.CreateDirectory(outDir);
        File.Copy(path, Path.Combine(outDir, name), overwrite: true);
    }
}
```

`CoffeePdf.Tests/Viewer/ViewerSaveTests.cs`:
```csharp
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Tests.Viewer;

/// <summary>Spec §14 items 1 and 7: what a save keeps.</summary>
[Trait("Category", "PdfViewer")]
public sealed class ViewerSaveTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [PdfViewerFact]
    public async Task Saving_a_signed_pdf_appends_and_keeps_the_signature()
    {
        var original = SignedPdf.Build();
        var path = Write("signed.pdf", original);
        Probe.Keep(path, "signed-before.pdf");

        await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            await viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await viewer.SaveAsync();
            return true;
        });

        var saved = File.ReadAllBytes(path);
        Probe.Keep(path, "signed-after.pdf");
        Assert.True(saved.Length > original.Length, "The save added nothing to the file");
        Assert.Equal(original, saved[..original.Length]);
        SignedPdf.VerifyFirstSignature(saved);
    }

    [PdfViewerFact]
    public async Task A_save_keeps_another_apps_note()
    {
        var path = Write("noted.pdf", MinimalPdf.Build(withForeignNote: true));

        var annotations = await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            await viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await viewer.SaveAsync();
            viewer.CloseDocument();
            var reopened = await Viewers.OpenAsync(path);
            return await reopened.GetAnnotationsAsync(0, CancellationToken.None);
        });

        Assert.Contains(annotations, a => a.Author == MinimalPdf.ForeignNoteAuthor && a.Contents == MinimalPdf.ForeignNoteContents);
        Assert.Contains(annotations, a => a.Contents == "Reviewed");
    }

    [PdfViewerFact]
    public async Task A_save_clears_the_dirty_flag()
    {
        var path = Write("plain.pdf", MinimalPdf.Build());

        var (afterEdit, afterSave) = await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            await viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var edited = viewer.IsDirty;
            await viewer.SaveAsync();
            return (edited, viewer.IsDirty);
        });

        Assert.True(afterEdit);
        Assert.False(afterSave);
    }
}
```

`CoffeePdf.Tests/Viewer/ViewerProbes.cs`:
```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using CoffeePdf.Tests.Fixtures;
using Xunit.Abstractions;

namespace CoffeePdf.Tests.Viewer;

/// <summary>
/// Spec §14 items 2 and 3: questions with no right answer to assert, so these print what the PdfViewer does, for the
/// findings. Run with: dotnet test --filter Category=Probe --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "PdfViewer")]
[Trait("Category", "Probe")]
public sealed class ViewerProbes(ITestOutputHelper output) : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-probes-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [PdfViewerFact]
    public async Task What_author_a_new_note_gets()
    {
        var path = Write("author.pdf", MinimalPdf.Build());

        var authors = await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            await viewer.AddStickyNoteAsync(0, 300, 400, "Probe");
            await viewer.SaveAsync();
            viewer.CloseDocument();
            var reopened = await Viewers.OpenAsync(path);
            return (await reopened.GetAnnotationsAsync(0, CancellationToken.None))
                .Where(a => a.Contents == "Probe").Select(a => a.Author).ToList();
        });

        output.WriteLine($"A new note's author: {(authors.Count == 0 ? "(note not found)" : authors[0] ?? "(none)")}");
        output.WriteLine($"For comparison, this account's user name is {Environment.UserName}");
    }

    [PdfViewerFact]
    public async Task How_a_save_reaches_the_disk()
    {
        foreach (var (name, bytes) in new[] { ("plain.pdf", MinimalPdf.Build()), ("signed.pdf", SignedPdf.Build()) })
        {
            var path = Write(name, bytes);
            var inodeBefore = Inode(path);
            var events = new ConcurrentQueue<string>();
            using var watcher = new FileSystemWatcher(_dir);
            watcher.Changed += (_, e) => events.Enqueue($"Changed {e.Name}");
            watcher.Created += (_, e) => events.Enqueue($"Created {e.Name}");
            watcher.Deleted += (_, e) => events.Enqueue($"Deleted {e.Name}");
            watcher.Renamed += (_, e) => events.Enqueue($"Renamed {e.OldName} -> {e.Name}");
            watcher.EnableRaisingEvents = true;

            var took = await Headless.RunAsync(async () =>
            {
                var viewer = await Viewers.OpenAsync(path);
                await viewer.AddStickyNoteAsync(0, 300, 400, "Probe");
                var clock = Stopwatch.StartNew();
                await viewer.SaveAsync();
                return clock.Elapsed;
            });
            // A probe's one fixed wait: FSEvents delivers in batches, and there's no condition to wait for, since
            // which events come is the question.
            await Task.Delay(TimeSpan.FromSeconds(2));

            output.WriteLine($"{name}: SaveAsync took {took.TotalMilliseconds:F0} ms; size {bytes.Length} -> {new FileInfo(path).Length}");
            output.WriteLine($"{name}: inode {inodeBefore} -> {Inode(path)} ({(inodeBefore == Inode(path) ? "written in place" : "replaced")})");
            output.WriteLine($"{name}: events: {string.Join("; ", events)}");
        }
    }

    private static string Inode(string path)
    {
        var start = new ProcessStartInfo("stat") { RedirectStandardOutput = true };
        foreach (var arg in OperatingSystem.IsMacOS() ? new[] { "-f", "%i" } : new[] { "-c", "%i" })
            start.ArgumentList.Add(arg);
        start.ArgumentList.Add(path);
        using var stat = Process.Start(start)!;
        var inode = stat.StandardOutput.ReadToEnd().Trim();
        stat.WaitForExit();
        return inode;
    }
}
```

- [ ] **Step 7: Run them with the key**

Run: `dotnet test CoffeePdf.sln --filter "Category=PdfViewer" --logger "console;verbosity=detailed"`

Expected:
- **`Saving_a_signed_pdf_appends_and_keeps_the_signature` passes.** The docs say signed files are saved as incremental updates.
- **`A_save_keeps_another_apps_note` passes.**
- **`A_save_clears_the_dirty_flag` passes.**
- **The two probes pass,** and print their findings.

Copy the probes' output into a scratch note for Task 7.

A failure here is a finding, not a bug to fix in this task:
- Record which assertion failed and what was seen.
- Mark the test `[PdfViewerFact(Skip = "Phase 0 finding: <what was seen>")]`. `Skip` is settable on `FactAttribute`, so this overrides the constructor's value.
- Go on. Task 7 puts the finding to the owner.

If `AddStickyNoteAsync` itself fails in a headless window (it returns -1, or `AnnotationError` fires), record that too. It decides whether Phase 1 can test markup headless.

- [ ] **Step 8: Keep the signed files for the owner**

Run:
```bash
COFFEEPDF_PROBE_OUT="$PWD/probe-out" dotnet test CoffeePdf.sln --filter "FullyQualifiedName~Saving_a_signed_pdf"
```
Expected: `probe-out/signed-before.pdf` and `probe-out/signed-after.pdf`. `probe-out/` isn't committed: the template's `[Oo]ut/` doesn't match it, so check with `git status` and delete it after Task 7.

- [ ] **Step 9: Commit**

```bash
git add CoffeePdf.Tests
git commit -m "Check how the PdfViewer saves: signatures, other apps' notes, the dirty flag; probe authors and disk writes"
```

---

### Task 5: The macOS package

**Files:**
- Create: `packaging/mac/Info.plist.in`, `packaging/mac/entitlements.plist`, `packaging/mac/package.sh`

**Interfaces:**
- Consumes: the `CoffeePDF` binary from `CoffeePdf/CoffeePdf.csproj` (Task 3).
- Produces: `packaging/mac/package.sh <version>`, which writes `CoffeePDF.app` and `CoffeePDF-<version>.dmg` into `OUT_DIR` (`packaging/mac/out` by default). It reads `SIGN_IDENTITY`, `SIGN_KEYCHAIN`, `NOTARY_KEY`, `NOTARY_KEY_ID` and `NOTARY_ISSUER`, as the Helper's script does, and `AVALONIA_LICENSE_KEY`.

- [ ] **Step 1: Write the bundle's files**

`packaging/mac/Info.plist.in`:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<!--
  CoffeePDF.app's Info.plist (spec §4.3, §10). package.sh replaces @VERSION@ with the numeric part of the version it
  builds (1.2.3 from v1.2.3-rc1). CFBundleDocumentTypes declares PDFs, at the Alternate rank, so CoffeePDF is offered
  in Open With and can be made the default without taking over from Preview on install. The bundle identifier is
  what the Healdan Helper launches CoffeePDF by (spec §4.4), so it never changes.
-->
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>CoffeePDF</string>
  <key>CFBundleExecutable</key>
  <string>CoffeePDF</string>
  <key>CFBundleIdentifier</key>
  <string>com.healdan.coffeepdf</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>CoffeePDF</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>@VERSION@</string>
  <key>CFBundleVersion</key>
  <string>@VERSION@</string>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key>
      <string>PDF Document</string>
      <key>CFBundleTypeRole</key>
      <string>Editor</string>
      <key>LSHandlerRank</key>
      <string>Alternate</string>
      <key>LSItemContentTypes</key>
      <array>
        <string>com.adobe.pdf</string>
      </array>
    </dict>
  </array>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.productivity</string>
  <key>LSMinimumSystemVersion</key>
  <string>26.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSHumanReadableCopyright</key>
  <string>Copyright © 2026 coffeemuse</string>
</dict>
</plist>
```

`packaging/mac/entitlements.plist`:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<!--
  The hardened runtime's one exception, as the Healdan Helper's (spec §10): .NET's JIT writes and runs code in MAP_JIT
  memory (allow-jit). package.sh signs the bundle's native libraries, PDFium's included, with CoffeePDF's own identity,
  so library validation accepts them. No sandbox: CoffeePDF opens files wherever they are, and the Helper's copies.
-->
<plist version="1.0">
<dict>
  <key>com.apple.security.cs.allow-jit</key>
  <true/>
</dict>
</plist>
```

- [ ] **Step 2: Write `package.sh`**

`packaging/mac/package.sh`. This is the Helper's script (`clients/helper/packaging/mac/package.sh` in Healdan) made CoffeePDF's: no LibreOffice extension, no icon yet, and a warning without the licence key.
```bash
#!/usr/bin/env bash
# Builds CoffeePDF for Apple silicon (spec §10): CoffeePDF.app, signed and, when the notary's key is given, notarised,
# in CoffeePDF-<version>.dmg.
#
#   packaging/mac/package.sh <version>        e.g. 0.1.0, or 0.1.0-rc1 (from the tag v0.1.0-rc1)
#
# Environment (all optional):
#   AVALONIA_LICENSE_KEY  the Avalonia licence key the PdfViewer checks. Unset, the app builds, but shows no PDF.
#   SIGN_IDENTITY   the Developer ID Application identity to sign with, as `security find-identity` names it. Unset,
#                   the app gets only an ad-hoc signature, which Gatekeeper refuses on any other Mac. Set but not in
#                   the keychains, the script stops: it never falls back to ad-hoc when a signature was asked for.
#   SIGN_KEYCHAIN   the keychain holding it (CI's temporary one); otherwise the usual search list.
#   NOTARY_KEY      the App Store Connect API key's .p8 file, with NOTARY_KEY_ID and NOTARY_ISSUER: with all three and
#                   a signing identity, the app and the disk image are notarised and stapled.
#   OUT_DIR         where the app and the disk image go; packaging/mac/out by default.
#
# The steps, as the Healdan Helper's: a self-contained, single-file .NET publish for osx-arm64 with no .pdb, since any
# file in Contents/MacOS that isn't native code breaks the bundle's signature; the bundle; signing inside-out (each
# native library, the binary, then the bundle) with the hardened runtime and a timestamp; codesign's strict check;
# notarising and stapling; the disk image, with a link to Applications, signed (and notarised) too. Any failure stops it.
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

[ -n "${AVALONIA_LICENSE_KEY:-}" ] ||
  warn "WITHOUT the Avalonia licence key: AVALONIA_LICENSE_KEY is unset, so the app's PdfViewer refuses to show a PDF"

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
  say "Signing with $identity"
else
  signer=-
  timestamp=--timestamp=none
  warn "NOT SIGNED with a Developer ID: SIGN_IDENTITY is unset. The app gets an ad-hoc signature only, so Gatekeeper refuses it on any other Mac, and it isn't notarised."
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

say "Publishing CoffeePDF $version for osx-arm64"
dotnet publish "$repo/CoffeePdf/CoffeePdf.csproj" \
  --configuration Release --runtime osx-arm64 --self-contained true --output "$work/publish" \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -p:Version="$numeric" -p:InformationalVersion="$version" -p:IncludeSourceRevisionInInformationalVersion=false \
  -nologo

# Only native code may sit in Contents/MacOS: a managed .dll, a .pdb or a .json there breaks the bundle's signature.
for file in "$work/publish"/*; do
  file -b "$file" | grep -q '^Mach-O' || die "the publish left $(basename "$file"), which isn't native code, beside the binary"
done
[ -f "$work/publish/CoffeePDF" ] || die "the publish made no CoffeePDF binary"

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
  codesign --force --options runtime "$timestamp" --sign "$signer" ${keychain[@]+"${keychain[@]}"} "$@"
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
  ditto -c -k --keepParent "$app" "$work/app.zip"
  notarise "$work/app.zip" "the app"
  xcrun stapler staple "$app"
  xcrun stapler validate "$app"
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
  xcrun stapler staple "$dmg"
  xcrun stapler validate "$dmg"
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
```

Run: `chmod +x packaging/mac/package.sh && bash -n packaging/mac/package.sh && (command -v shellcheck >/dev/null && shellcheck packaging/mac/package.sh || true)`
Expected: no syntax errors. Fix anything shellcheck flags as an error.

- [ ] **Step 3: Build an unsigned package, with the key**

Run: `packaging/mac/package.sh 0.0.1-spike1`
Expected: `Built …/CoffeePDF-0.0.1-spike1.dmg`, with the NOT SIGNED warning.

If it stops with `the publish left X, which isn't native code`, that's a finding: the PdfViewer ships a file a single-file publish leaves outside the binary.
- Record X.
- If X is the PdfViewer's `.xml` documentation, add `-p:GenerateDocumentationFile=false -p:AllowedReferenceRelatedFileExtensions=none` to the publish line and build again.
- Otherwise, stop and record it for the owner.

Then check the bundle:
```bash
ls -l packaging/mac/out/CoffeePDF.app/Contents/MacOS
plutil -p packaging/mac/out/CoffeePDF.app/Contents/Info.plist | grep -E 'CFBundleIdentifier|LSItemContentTypes' -A1
open -a packaging/mac/out/CoffeePDF.app "$TMPDIR/coffeepdf-smoke.pdf"
```
Expected:
- `Contents/MacOS` holds `CoffeePDF`, `libpdfium.dylib` and Avalonia's native libraries (`libAvaloniaNative.dylib`, `libSkiaSharp.dylib`, `libHarfBuzzSharp.dylib`).
- The plist shows `com.healdan.coffeepdf` and `com.adobe.pdf`.
- The app opens and shows the smoke PDF.

Record the list of libraries for the findings.

- [ ] **Step 4: Optionally, a signed build on the owner's Mac**

If the owner wants to check signing before CI exists, they run it themselves, since it uses their Developer ID:
```bash
SIGN_IDENTITY="Developer ID Application: <the owner's name> (<team id>)" packaging/mac/package.sh 0.0.1-spike1
```
Expected: `codesign --verify --strict --deep` passes, and the script ends with "Signed with …, but NOT NOTARISED" unless the `NOTARY_*` variables are set.

- [ ] **Step 5: Commit**

```bash
git add packaging/mac
git commit -m "Add the macOS package: CoffeePDF.app for PDFs, signed and notarised in a disk image"
```

---

### Task 6: CI

**Files:**
- Create: `.github/workflows/ci.yml`
- Modify: `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` (§10, where the licence key is kept)

**Interfaces:**
- Consumes: `CoffeePdf.sln` (Tasks 1–4) and `packaging/mac/package.sh <version>` (Task 5).
- Produces:
  - The `Tests` check, for the owner to require on `main`.
  - Packages for `v*` tags.
  - Secrets the owner sets: the repository secret `AVALONIA_LICENSE_KEY`, and the `release` environment's `APPLE_DEVELOPER_ID_P12`, `APPLE_DEVELOPER_ID_PASSWORD`, `APPLE_TEAM_ID`, `NOTARY_KEY`, `NOTARY_KEY_ID` and `NOTARY_ISSUER`.

- [ ] **Step 1: Write the workflow**

`.github/workflows/ci.yml`. Its signing steps are the Healdan Helper's (`.github/workflows/clients.yml` in Healdan), renamed.
```yaml
name: CI

# Tests CoffeePDF on every push and pull request, on macOS and Ubuntu. A v* tag, or a run by hand, also packages the
# macOS app (spec §10); a pushed v* tag attaches the notarised disk image to its release.
#
# AVALONIA_LICENSE_KEY is a repository secret, so every run's tests use the PdfViewer. Without it, as for a pull
# request from a fork, the PdfViewer's tests skip and everything else runs. The signing secrets are the release
# environment's, which only v* tags may use.

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]
  pull_request:
    branches: [ main ]
  workflow_dispatch:
    inputs:
      version:
        description: The version to package, such as 0.1.0-test1 (by default 0.0.0-dev.<run number>)
        required: false
        default: ''

permissions:
  contents: read

# A new push to a pull request cancels its outdated run. Pushes get a group per ref and commit, so a tag's run doesn't
# queue behind its branch's.
concurrency:
  group: ${{ github.workflow }}-${{ github.event_name == 'pull_request' && github.ref || format('{0}-{1}', github.ref, github.sha) }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

jobs:
  test:
    name: Tests (${{ matrix.runner }})
    strategy:
      fail-fast: false
      matrix:
        runner: [ macos-15, ubuntu-24.04 ]
    runs-on: ${{ matrix.runner }}
    timeout-minutes: 20

    steps:
      - name: Checkout source
        uses: actions/checkout@v7

      - name: Set up .NET
        uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - name: Build and test
        env:
          AVALONIA_LICENSE_KEY: ${{ secrets.AVALONIA_LICENSE_KEY }}
        run: dotnet test CoffeePdf.sln --configuration Release

  # The check to require on main: it passes only when both runners' tests passed.
  tests:
    name: Tests
    needs: [ test ]
    if: always()
    runs-on: ubuntu-24.04
    steps:
      - name: Check the jobs' results
        env:
          TEST: ${{ needs.test.result }}
        run: |
          echo "test: $TEST"
          if [ "$TEST" = success ]; then exit 0; fi
          echo "::error::The tests didn't pass"
          exit 1

  package-mac:
    name: Package (macos-15)
    needs: [ test ]
    if: github.event_name == 'workflow_dispatch' || startsWith(github.ref, 'refs/tags/v')
    # No environment for a branch: the environment would refuse it, failing the job instead of building unsigned.
    environment: ${{ startsWith(github.ref, 'refs/tags/v') && 'release' || '' }}
    runs-on: macos-15
    timeout-minutes: 60
    outputs:
      version: ${{ steps.version.outputs.version }}
      notarised: ${{ steps.stapled.outputs.notarised }}

    steps:
      - name: Checkout source
        uses: actions/checkout@v7

      - name: Set up .NET
        uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - name: Work out the version
        id: version
        env:
          EVENT: ${{ github.event_name }}
          REF_NAME: ${{ github.ref_name }}
          REQUESTED: ${{ inputs.version }}
        run: |
          if [ "$EVENT" = workflow_dispatch ]; then
            version=${REQUESTED:-0.0.0-dev.$GITHUB_RUN_NUMBER}
          else
            version=${REF_NAME#v}
          fi
          if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$ ]]; then
            echo "::error::'$version' isn't a version like 1.2.3 or 1.2.3-rc1 (tags are v1.2.3)"
            exit 1
          fi
          echo "version=$version" >> "$GITHUB_OUTPUT"
          echo "Packaging CoffeePDF $version"

      # Secrets can't be tested in an if:, so this step says which are set, without printing them.
      - name: Check for the signing secrets
        id: secrets
        env:
          P12: ${{ secrets.APPLE_DEVELOPER_ID_P12 }}
          P12_PASSWORD: ${{ secrets.APPLE_DEVELOPER_ID_PASSWORD }}
          TEAM_ID: ${{ secrets.APPLE_TEAM_ID }}
          NOTARY_KEY_B64: ${{ secrets.NOTARY_KEY }}
          NOTARY_KEY_ID: ${{ secrets.NOTARY_KEY_ID }}
          NOTARY_ISSUER: ${{ secrets.NOTARY_ISSUER }}
        run: |
          signing=false
          notary=false
          if [ -n "$P12" ] && [ -n "$P12_PASSWORD" ] && [ -n "$TEAM_ID" ]; then signing=true; fi
          if [ -n "$NOTARY_KEY_B64" ] && [ -n "$NOTARY_KEY_ID" ] && [ -n "$NOTARY_ISSUER" ]; then notary=true; fi
          echo "signing=$signing" >> "$GITHUB_OUTPUT"
          echo "notary=$notary" >> "$GITHUB_OUTPUT"
          if [ "$signing" = false ]; then
            echo "::warning::The APPLE_DEVELOPER_ID_* and APPLE_TEAM_ID secrets aren't available (they're the release environment's, for v* tags), so the package is built unsigned"
          elif [ "$notary" = false ]; then
            echo "::warning::The NOTARY_* secrets aren't set, so the package is signed but not notarised"
          fi

      # A keychain of its own, with a random password, holding only the Developer ID certificate.
      - name: Import the Developer ID certificate
        if: steps.secrets.outputs.signing == 'true'
        env:
          P12: ${{ secrets.APPLE_DEVELOPER_ID_P12 }}
          P12_PASSWORD: ${{ secrets.APPLE_DEVELOPER_ID_PASSWORD }}
          TEAM_ID: ${{ secrets.APPLE_TEAM_ID }}
        run: |
          keychain="$RUNNER_TEMP/coffeepdf-signing.keychain-db"
          password=$(openssl rand -hex 24)
          echo "::add-mask::$password"
          umask 077
          printf '%s' "$P12" | base64 --decode > "$RUNNER_TEMP/developer-id.p12"
          security create-keychain -p "$password" "$keychain"
          security set-keychain-settings -lut 21600 "$keychain"
          security unlock-keychain -p "$password" "$keychain"
          security import "$RUNNER_TEMP/developer-id.p12" -k "$keychain" -f pkcs12 -P "$P12_PASSWORD" -T /usr/bin/codesign
          rm -f "$RUNNER_TEMP/developer-id.p12"
          security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$password" "$keychain" > /dev/null
          # Searched first, before the runner's own keychains (listed one to a line, in quotes).
          keychains=("$keychain")
          while IFS= read -r line; do
            line=${line#*\"}
            line=${line%\"*}
            if [ -n "$line" ]; then keychains+=("$line"); fi
          done < <(security list-keychains -d user)
          security list-keychains -d user -s "${keychains[@]}"
          identity=$(security find-identity -v -p codesigning "$keychain" | sed -n 's/.*"\(Developer ID Application: .*\)"$/\1/p' | grep -F "($TEAM_ID)" | head -n 1)
          if [ -z "$identity" ]; then
            echo "::error::APPLE_DEVELOPER_ID_P12 holds no Developer ID Application identity for APPLE_TEAM_ID"
            exit 1
          fi
          echo "SIGN_IDENTITY=$identity" >> "$GITHUB_ENV"
          echo "SIGN_KEYCHAIN=$keychain" >> "$GITHUB_ENV"

      - name: Decode the notary's key
        if: steps.secrets.outputs.signing == 'true' && steps.secrets.outputs.notary == 'true'
        env:
          NOTARY_KEY_B64: ${{ secrets.NOTARY_KEY }}
        run: |
          umask 077
          printf '%s' "$NOTARY_KEY_B64" | base64 --decode > "$RUNNER_TEMP/notary-key.p8"
          echo "NOTARY_KEY=$RUNNER_TEMP/notary-key.p8" >> "$GITHUB_ENV"

      - name: Build the package
        env:
          VERSION: ${{ steps.version.outputs.version }}
          AVALONIA_LICENSE_KEY: ${{ secrets.AVALONIA_LICENSE_KEY }}
          NOTARY_KEY_ID: ${{ steps.secrets.outputs.notary == 'true' && secrets.NOTARY_KEY_ID || '' }}
          NOTARY_ISSUER: ${{ steps.secrets.outputs.notary == 'true' && secrets.NOTARY_ISSUER || '' }}
          OUT_DIR: ${{ runner.temp }}/package
        run: packaging/mac/package.sh "$VERSION"

      # What release-mac trusts: the disk image's own stapled ticket, not which secrets were set.
      - name: Check the disk image is notarised
        id: stapled
        env:
          DMG: ${{ runner.temp }}/package/CoffeePDF-${{ steps.version.outputs.version }}.dmg
        run: |
          if xcrun stapler validate "$DMG"; then
            echo "notarised=true" >> "$GITHUB_OUTPUT"
          else
            echo "::warning::The disk image isn't notarised, so it isn't attached to any release"
            echo "notarised=false" >> "$GITHUB_OUTPUT"
          fi

      - name: Upload the disk image
        uses: actions/upload-artifact@v7
        with:
          name: CoffeePDF-${{ steps.version.outputs.version }}-macos
          path: ${{ runner.temp }}/package/CoffeePDF-${{ steps.version.outputs.version }}.dmg
          if-no-files-found: error
          retention-days: 30

      # The keychain and the decoded files go whatever happened.
      - name: Remove the keychain and keys
        if: always()
        run: |
          keychain="$RUNNER_TEMP/coffeepdf-signing.keychain-db"
          if [ -f "$keychain" ]; then security delete-keychain "$keychain" || true; fi
          rm -f "$RUNNER_TEMP/developer-id.p12" "$RUNNER_TEMP/notary-key.p8"

  # Attaches the notarised disk image to the tag's GitHub release, making the release if there's none yet, only for a
  # pushed v* tag. Kept apart from package-mac so only this short job, which builds nothing and sees no signing secret,
  # can write to the repository.
  release-mac:
    name: Release (macOS)
    needs: package-mac
    if: github.event_name == 'push' && startsWith(github.ref, 'refs/tags/v') && needs.package-mac.outputs.notarised == 'true'
    runs-on: ubuntu-24.04
    timeout-minutes: 10
    permissions:
      contents: write

    steps:
      - name: Download the disk image
        uses: actions/download-artifact@v8
        with:
          name: CoffeePDF-${{ needs.package-mac.outputs.version }}-macos
          path: ${{ runner.temp }}/package

      - name: Attach it to the release
        env:
          GH_TOKEN: ${{ github.token }}
          TAG: ${{ github.ref_name }}
          VERSION: ${{ needs.package-mac.outputs.version }}
        run: |
          dmg="$RUNNER_TEMP/package/CoffeePDF-$VERSION.dmg"
          if ! gh release view "$TAG" --repo "$GITHUB_REPOSITORY" > /dev/null 2>&1; then
            prerelease=()
            if [[ "$VERSION" == *-* ]]; then prerelease=(--prerelease); fi
            gh release create "$TAG" --repo "$GITHUB_REPOSITORY" --verify-tag --title "CoffeePDF $VERSION" \
              --notes "CoffeePDF $VERSION for macOS on Apple silicon: open the disk image and drag CoffeePDF to Applications." \
              "${prerelease[@]}"
          fi
          gh release upload "$TAG" "$dmg" --repo "$GITHUB_REPOSITORY" --clobber
```

Run: `command -v actionlint >/dev/null && actionlint .github/workflows/ci.yml || echo "actionlint not installed; checked by reading"`
Expected: no errors.

- [ ] **Step 2: Bring the spec in line with where the key lives**

In `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` §10, replace:
```
  - It uses a `release` environment holding the Apple signing and notarising secrets (as the Helper's `helper-release` does) and the Avalonia licence key.
```
with:
```
  - It uses a `release` environment holding the Apple signing and notarising secrets, as the Helper's `helper-release` does. The Avalonia licence key is a repository secret instead, since every run's tests need it (changed in Phase 0).
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md
git commit -m "Add CI: tests on macOS and Ubuntu, and the signed, notarised package for v* tags"
```

- [ ] **Step 4: Hand over to the owner** to push and set the secrets. Don't push yourself. Tell the owner:
1. **Repository secret** (Settings › Secrets and variables › Actions): `AVALONIA_LICENSE_KEY`.
2. **A `release` environment**, limited to tags matching `v*`, with:
   - `APPLE_DEVELOPER_ID_P12`: the Developer ID Application certificate and key, as base64 of a .p12;
   - `APPLE_DEVELOPER_ID_PASSWORD`;
   - `APPLE_TEAM_ID`;
   - `NOTARY_KEY` (base64 of the App Store Connect API key's .p8), `NOTARY_KEY_ID` and `NOTARY_ISSUER`.

   These are the same values the Helper's `helper-release` environment holds.
3. **The cost:** the repository is private, so macOS runner minutes count at 10× against the account's Actions minutes.
4. **Then push the branch and open a pull request.** Expected: `Tests` passes, with the PdfViewer's tests run, not skipped. Check the macOS job's log for "Skipped".
5. **A pre-release tag after merging,** such as `v0.0.1-spike1`. Expected: `Package (macos-15)` builds a notarised disk image, and `Release (macOS)` attaches it to a pre-release.

---

### Task 7: The owner's checks on the Mac, and the findings

**Files:**
- Modify: `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` (add §16), `docs/ROADMAP.md`

**Interfaces:**
- Consumes: the outcomes recorded in Tasks 3–6 and the probes' output (Task 4 Step 7).
- Produces: spec §16, "Phase 0 findings", and the decisions put to the owner.

- [ ] **Step 1: The owner's checks.** Each needs a person at the Mac. Give the owner this list with the notarised build from Task 6 (or the unsigned one from Task 5), and record each answer.

1. **Gatekeeper.** Open the notarised `.dmg` from the release, drag CoffeePDF to Applications, and open it. Expected: no Gatekeeper warning, and an empty window.
2. **Open With, at a cold start.** Quit CoffeePDF. In Finder, right-click a PDF, then Open With › CoffeePDF. Expected: it opens in a CoffeePDF window, with no empty window beside it.
3. **Open With while running.** With CoffeePDF running, Open With another PDF. Expected: a second window. Do it again with the first PDF, and expected: its window comes forward, with no third window.
4. **The default PDF app.** Finder › Get Info on a PDF › Open with: CoffeePDF › Change All…, then double-click a PDF. Expected: it opens in CoffeePDF. Put Preview back afterwards if the owner prefers.
5. **The menu bar.** File shows Open… ⌘O, Save ⌘S and Close ⌘W, and each works. The CoffeePDF menu has Quit ⌘Q.
6. **Text layout.** Compare the PdfViewer's toolbar and sidebar labels with Preview's. Look for text clipped or overlapping, the San Francisco measuring bug the Helper fixed in its `Platform/SystemFont.cs` (spec §14 item 6). Record yes or no, with a screenshot if yes.
7. **The signature, in Acrobat Reader or Preview.**
   - Open `probe-out/signed-after.pdf` from Task 4 Step 8. Expected: the signature panel says the document hasn't been modified since it was signed, or that only allowed changes (annotations) were made after. Its certificate shows as untrusted, which is expected for a throwaway one.
   - Compare with `probe-out/signed-before.pdf`.
8. **Acrobat's own notes.** Open a PDF that has comments made in Acrobat (from the owner's own files), add a highlight in CoffeePDF, save, and reopen in Acrobat. Expected: Acrobat's comments are all still there. Note that replies to a comment lose their threading if CoffeePDF edits that comment (the PdfViewer's docs say so).

- [ ] **Step 2: Write the findings into the spec.** Append to `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`, filling each row's Result from what Tasks 3–6 and Step 1 recorded:

```markdown
## 16. Phase 0 findings (2026-MM-DD)

| §14 item | Question | Result | Consequence |
|---|---|---|---|
| 1 | Does a save keep a signature, and other apps' annotations? | | |
| 2 | How does a save reach the disk (in place or replaced, events, time)? | | |
| 3 | Can the app set the author of new annotations? | | |
| 4 | How does the licence key reach the build and the tests? | | |
| 5 | macOS: open-file events (cold and warm), menus, the default PDF app | | |
| 6 | Text layout with San Francisco | | |
| 7 | Notarisation with PDFium under the hardened runtime | | |
| 8 | The core free of Avalonia | ArchitectureTests guards it | none |

### Decisions for the owner
```

Two facts known before Phase 0 started go in, whatever the tests show:
- **Item 1:** the PdfViewer's docs say signed documents are saved as incremental updates and unsigned ones are fully rewritten.
- **Item 3:** the PdfViewer's public API (12.0.1) can read an annotation's author (`PdfAnnotationInfo.Author`) but has no way to set the author of new ones.

So unless the probe shows the author can already be set, the decisions section lists item 3's options:
- (a) Ask Avalonia, under the Pro licence, for an author setting on the viewer, and keep the Markup author preference waiting on it.
- (b) After each save, write the author into the new annotations through PDFium directly, as a second, incremental write. That adds complexity, and a second save for the Helper to see.
- (c) Ship v1 with the control's default author, and move the preference to when (a) lands.

Add any other decision a failed check forces.

- [ ] **Step 3: Update the roadmap**

In `docs/ROADMAP.md`:
- Change Phase 0's status to `done YYYY-MM-DD, findings in the spec's §16`.
- Change the Status line to say Phase 1's plan waits on the owner's decisions in §16.

- [ ] **Step 4: Clean up and commit**

```bash
rm -rf probe-out
git add docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md docs/ROADMAP.md
git commit -m "Record Phase 0's findings and the decisions they leave for the owner"
```

- [ ] **Step 5: Put the decisions to the owner.** Phase 1's plan isn't written until they're answered (spec §13).
