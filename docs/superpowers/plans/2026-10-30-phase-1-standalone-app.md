# CoffeePDF Phase 1: Standalone App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make CoffeePDF a PDF app the owner can use on its own, in place of Preview. It writes the Markup author into
every markup, saves safely, has Save As, Open Recent and the close and quit prompts, keeps running with no window, has
Settings with the default PDF app, and has a full menu bar with printing.

**Architecture:** Logic goes in `CoffeePdf.Core`, and its tests run on macOS and Linux. That covers:
- CoffeePDF's own PDFium calls, which write the author;
- the atomic file write;
- the settings and Open Recent.

The Avalonia app (`CoffeePdf`) wires them to the PdfViewer, the windows and the menus.

Every save goes through one path, `DocumentWindow.SaveToAsync`:
1. The PdfViewer saves to memory.
2. `MarkupAuthor.Stamp` writes the author into the new annotations, as an incremental update.
3. `AtomicFile.Write` renames the result over the file.

The PdfViewer's own saving is off, so nothing can save around that path.

**Tech Stack:**
- .NET 10 and C#.
- Avalonia 12.1.4, with `Avalonia.Controls.PdfViewer` 12.0.1.
- PDFium 157 through `bblanchon.PDFium.macOS` and `bblanchon.PDFium.Linux` 157.0.8086, the PdfViewer's own PDFium.
- macOS Launch Services and Core Foundation, for the default PDF app.
- xUnit 2.9.3 and Avalonia.Headless.

**Spec:** `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`. Read §2.1, §5, §6.4, §6.8, §7, §8, §13, and all of
§16 (Decision 1 and the planning probe) first.

## Global Constraints

- **SDK and versions:** the SDK is `10.0.100` with `rollForward: latestFeature`, and projects target `net10.0`. Package
  versions live in `Directory.Packages.props`:
  - Avalonia packages are `12.1.4`.
  - Avalonia.Controls.PdfViewer is `12.0.1`, and AvaloniaUI.Licensing is `3.1.3`.
  - `bblanchon.PDFium.macOS` and `bblanchon.PDFium.Linux` are `157.0.8086`, the version the PdfViewer 12.0.1 depends on.
- **`CoffeePdf.Core` references no Avalonia assembly** (`ArchitectureTests`). It may reference the PDFium packages.
- **The Avalonia licence key** comes only from `AVALONIA_LICENSE_KEY`, and is never committed, logged or printed.
- **Compiler settings:** `TreatWarningsAsErrors`, `Nullable` and `ImplicitUsings` are on everywhere.
- **Every save goes through `DocumentWindow.SaveToAsync`.** The PdfViewer's `AllowDocumentSaving` and `AutoSave` stay
  `False`.
- **PDFium is shared with the PdfViewer.** CoffeePDF's calls take `Pdfium.Gate`, and nothing ever calls
  `FPDF_DestroyLibrary`.
- **Settings in tests:**
  - Tests never touch the real settings folder. `Headless` points `COFFEEPDF_SETTINGS_DIR` at a temporary folder before
    Avalonia starts.
  - Core tests pass a folder of their own.
- **Personal details:** no test, probe or log prints the account's full name or user name, so tests assert on it
  without writing it out. Test names are made up, such as `Test Reviewer`.
- **Tests wait for conditions, never for fixed times.**
- **Tests that need the PdfViewer** use `[PdfViewerFact]` and `[Trait("Category", "PdfViewer")]`. A test that creates a
  `DocumentWindow` needs it, since the window creates a PdfViewer. Tests of other windows use `[Fact]`, and so run
  without the key.
- **macOS names:** the menu item for the spec's Preferences is `Settings…` (⌘,), as on macOS 13 and later. Menu text
  uses `…` (U+2026) and curly quotes.
- **Pushes go through the owner.** Never push, tag or open a pull request without the owner's go-ahead.

## What the planning probe settled

This is spec §16's planning probe, in short. Each fact is observed, and the tasks rely on it:

- **Saving to a stream:** `PdfViewer.SaveDocumentAsync(Stream)`
  - clears `IsDirty`, and keeps `Source` and the undo history;
  - works with `AllowDocumentSaving="False"`, which makes the PdfViewer's own `SaveAsync` do nothing.
- **Signed files:** their stream saves are incremental updates on the original bytes, every time. A save after the file
  was replaced with an earlier save's bytes still starts with the original, and the signature verifies.
- **New annotations:** `AnnotationAdded` fires for every annotation made, with its `PageIndex`.
  - New notes, stamps and highlights get a `/NM` (as `av-<32 hex>`) and no `/T`.
  - Another app's note without a `/NM` keeps none after a save.
- **`IsDirty` and undo:** `IsDirty` stays `false` after an undo past a save.
- **PDFium:** `FPDF_InitLibrary` does nothing once PDFium is initialised, as PDFium's source shows. In the app the
  PdfViewer initialises it first, with its own configuration.
- **The prototype:** stamping `/T` through PDFium 157 and saving with `FPDF_SaveAsCopy(…, FPDF_INCREMENTAL)` keeps the
  original bytes as the file's start, and reads back correctly.

---

## File Structure

| File | Responsibility |
|---|---|
| `CoffeePdf.Core/Pdf/Pdfium.cs` | The PDFium functions CoffeePDF calls, its lock, and `PdfiumDocument` (a document, its pages' annotations, an incremental save) |
| `CoffeePdf.Core/Pdf/AnnotationSubtype.cs` | PDFium's annotation subtypes, and which are markup |
| `CoffeePdf.Core/Pdf/PdfAnnotations.cs` | Reads a page's annotations: subtype, name and author |
| `CoffeePdf.Core/Pdf/MarkupAuthor.cs` | Writes the author into the annotations made in CoffeePDF |
| `CoffeePdf.Core/Files/AtomicFile.cs` | Writes a file through a temporary file renamed over it |
| `CoffeePdf.Core/Settings/SettingsLocation.cs` | Where `settings.json` and `recent.json` live |
| `CoffeePdf.Core/Settings/SettingsStore.cs` | `settings.json`: Healdan support and the Markup author |
| `CoffeePdf.Core/Settings/UserNames.cs` | The account's full name, the author's default |
| `CoffeePdf.Core/Settings/RecentDocuments.cs` | Open Recent, in `recent.json` |
| `CoffeePdf.Core/Documents/DocumentTitle.cs` | A window's title |
| `CoffeePdf/App.axaml.cs`, `CoffeePdf/Program.cs` | Settings, recents, the app menu, Reopen, the quit prompts |
| `CoffeePdf/Windows/DocumentWindow.axaml(.cs)` | The save path, Save As, Open Recent, the close prompt, the menus, printing |
| `CoffeePdf/Windows/ActionCommand.cs` | A command for menu items built in code |
| `CoffeePdf/Dialogs/MessageDialog.axaml(.cs)`, `SaveChoice.cs` | The error message and the save-changes prompt |
| `CoffeePdf/Windows/SettingsWindow.axaml(.cs)` | Settings: the Markup author and the default PDF app |
| `CoffeePdf/Platform/DefaultPdfApp.cs` | `IDefaultPdfApp`, and its macOS Launch Services version |
| `CoffeePdf.Core.Tests/Fixtures/MinimalPdf.cs`, `AnnotatedPdf.cs` | Fixtures: `MinimalPdf` moves here from the app's tests, which link it |
| `CoffeePdf.Core.Tests/Pdf/…`, `Files/…`, `Settings/…`, `Documents/…` | The core's tests |
| `CoffeePdf.Tests/Windows/…`, `Dialogs/…` | The app's tests |

---

### Task 1: PDFium in the core

**Files:**
- Modify: `Directory.Packages.props`, `CoffeePdf.Core/CoffeePdf.Core.csproj`, `CoffeePdf.Tests/CoffeePdf.Tests.csproj`
- Move: `CoffeePdf.Tests/Fixtures/MinimalPdf.cs` → `CoffeePdf.Core.Tests/Fixtures/MinimalPdf.cs`
- Move: `CoffeePdf.Tests/Fixtures/MinimalPdfTests.cs` → `CoffeePdf.Core.Tests/Fixtures/MinimalPdfTests.cs`
- Create: `CoffeePdf.Core/Pdf/Pdfium.cs`, `CoffeePdf.Core/Pdf/AnnotationSubtype.cs`, `CoffeePdf.Core/Pdf/PdfAnnotations.cs`
- Test: `CoffeePdf.Core.Tests/Fixtures/AnnotatedPdf.cs`, `CoffeePdf.Core.Tests/Pdf/PdfAnnotationsTests.cs`

**Interfaces:**
- Produces:
  - `enum CoffeePdf.Core.Pdf.AnnotationSubtype`, with PDFium's `FPDF_ANNOT_*` values; `Text` is 1 and `Widget` is 20.
  - `AnnotationSubtypes.IsMarkup(this AnnotationSubtype) : bool`.
  - `record AnnotationFacts(AnnotationSubtype Subtype, string? Name, string? Author)`.
  - `PdfAnnotations.Read(byte[] pdf, int pageIndex) : IReadOnlyList<AnnotationFacts>`, which throws
    `InvalidDataException` when PDFium can't open the PDF.
  - `internal static class Pdfium`: `Gate`, `HasKey`, `GetString` and `SetString`.
  - `internal sealed class PdfiumDocument : IDisposable`: `Open(byte[], string?)`, `OpenFile(string, string?)`,
    `PageCount`, `ForEachAnnotation(int, Action<nint>)` and `SaveIncremental()`.
  - Test fixtures: `AnnotatedPdf.Build(params string[] annotations) : byte[]`,
    `AnnotatedPdf.Note(string? name, string? author) : string`, and `AnnotatedPdf.Widget(string name) : string`.
- `MinimalPdf` keeps its namespace, `CoffeePdf.Tests.Fixtures`, since both test projects compile it.

- [ ] **Step 1: Add the PDFium packages and move the fixture**

In `Directory.Packages.props`, add to the first `ItemGroup`, after the PdfViewer:
```xml
    <!-- PDFium for CoffeePDF's own calls (spec §16 Decision 1): the PdfViewer 12.0.1's own version, so one libpdfium. -->
    <PackageVersion Include="bblanchon.PDFium.macOS" Version="157.0.8086" />
    <PackageVersion Include="bblanchon.PDFium.Linux" Version="157.0.8086" />
```

Replace `CoffeePdf.Core/CoffeePdf.Core.csproj` with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Everything CoffeePDF decides that doesn't need a window (spec §4.3). It references no Avalonia assembly. -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>CoffeePdf.Core</RootNamespace>
    <!-- Pdf/Pdfium.cs passes pointers to PDFium. -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <!-- The PdfViewer's PDFium, for the core's own calls and for its tests on macOS and Linux. -->
    <PackageReference Include="bblanchon.PDFium.macOS" />
    <PackageReference Include="bblanchon.PDFium.Linux" />
  </ItemGroup>
</Project>
```

Move the fixture and its tests into the core's tests:
```bash
mkdir -p CoffeePdf.Core.Tests/Fixtures
git mv CoffeePdf.Tests/Fixtures/MinimalPdf.cs CoffeePdf.Core.Tests/Fixtures/MinimalPdf.cs
git mv CoffeePdf.Tests/Fixtures/MinimalPdfTests.cs CoffeePdf.Core.Tests/Fixtures/MinimalPdfTests.cs
```

In `CoffeePdf.Tests/CoffeePdf.Tests.csproj`, add an `ItemGroup` that compiles the moved fixture too. `SignedPdf` and the
PdfViewer tests use it:
```xml
  <ItemGroup>
    <!-- MinimalPdf lives with the core's tests, which need it without the app; the app's tests compile it too. -->
    <Compile Include="../CoffeePdf.Core.Tests/Fixtures/MinimalPdf.cs" Link="Fixtures/MinimalPdf.cs" />
  </ItemGroup>
```

`MinimalPdf.cs` and `MinimalPdfTests.cs` keep `namespace CoffeePdf.Tests.Fixtures;`. Add this comment above the
namespace line in `MinimalPdf.cs`:
```csharp
// Compiled by both test projects (CoffeePdf.Tests links it), so its namespace stays the app tests' own.
```

- [ ] **Step 2: Write the fixture for annotated PDFs**

`CoffeePdf.Core.Tests/Fixtures/AnnotatedPdf.cs`:
```csharp
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Fixtures;

/// <summary>One page of <see cref="MinimalPdf"/> carrying the annotations given, as objects 7 on.</summary>
internal static class AnnotatedPdf
{
    public static byte[] Build(params string[] annotations)
    {
        var references = annotations.Select((_, i) => $"{7 + i} 0 R").ToList();
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: references, acroForm: null);
        objects.AddRange(annotations);
        return MinimalPdf.Write(objects);
    }

    /// <summary>A sticky note, with a name (<c>/NM</c>) and an author (<c>/T</c>) when they're given.</summary>
    public static string Note(string? name, string? author) =>
        "<< /Type /Annot /Subtype /Text /Rect [72 600 96 624] /Contents (Note)" +
        (name is null ? "" : $" /NM ({name})") + (author is null ? "" : $" /T ({author})") + " /F 4 >>";

    /// <summary>A text field's widget, with a name and no field name (<c>/T</c>), as a form's kid can have.</summary>
    public static string Widget(string name) =>
        $"<< /Type /Annot /Subtype /Widget /FT /Tx /Rect [72 500 272 520] /NM ({name}) /F 4 >>";
}
```

- [ ] **Step 3: Write the failing test**

`CoffeePdf.Core.Tests/Pdf/PdfAnnotationsTests.cs`:
```csharp
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Pdf;

public sealed class PdfAnnotationsTests
{
    [Fact]
    public void It_reads_each_annotations_subtype_name_and_author()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("n-1", "Zoë Q. Reviewer"), AnnotatedPdf.Note(null, null), AnnotatedPdf.Widget("w-1"));

        var annotations = PdfAnnotations.Read(pdf, 0);

        Assert.Equal(
            new[]
            {
                new AnnotationFacts(AnnotationSubtype.Text, "n-1", "Zoë Q. Reviewer"),
                new AnnotationFacts(AnnotationSubtype.Text, null, null),
                new AnnotationFacts(AnnotationSubtype.Widget, "w-1", null),
            },
            annotations);
    }

    [Fact]
    public void A_page_without_annotations_has_none() =>
        Assert.Empty(PdfAnnotations.Read(AnnotatedPdf.Build(), 0));

    [Fact]
    public void Bytes_that_arent_a_pdf_are_refused() =>
        Assert.Throws<InvalidDataException>(() => PdfAnnotations.Read("%PDF-1.7 not really"u8.ToArray(), 0));

    [Fact]
    public void Only_text_markup_shapes_ink_stamps_and_the_like_are_markup()
    {
        Assert.True(AnnotationSubtype.Text.IsMarkup());
        Assert.True(AnnotationSubtype.Highlight.IsMarkup());
        Assert.True(AnnotationSubtype.Ink.IsMarkup());
        Assert.True(AnnotationSubtype.Stamp.IsMarkup());
        Assert.False(AnnotationSubtype.Widget.IsMarkup());
        Assert.False(AnnotationSubtype.Link.IsMarkup());
        Assert.False(AnnotationSubtype.Popup.IsMarkup());
    }
}
```

- [ ] **Step 4: Run it to see it fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~PdfAnnotationsTests`
Expected: the build fails. `CoffeePdf.Core.Pdf` doesn't exist yet.

- [ ] **Step 5: Write the PDFium layer**

`CoffeePdf.Core/Pdf/AnnotationSubtype.cs`:
```csharp
namespace CoffeePdf.Core.Pdf;

/// <summary>An annotation's subtype, as PDFium numbers them (<c>FPDF_ANNOT_*</c> in <c>fpdf_annot.h</c>).</summary>
public enum AnnotationSubtype
{
    Unknown = 0, Text, Link, FreeText, Line, Square, Circle, Polygon, PolyLine, Highlight, Underline, Squiggly,
    StrikeOut, Stamp, Caret, Ink, Popup, FileAttachment, Sound, Movie, Widget, Screen, PrinterMark, TrapNet, Watermark,
    ThreeD, RichMedia, XfaWidget, Redact,
}

public static class AnnotationSubtypes
{
    /// <summary>
    /// Whether annotations of <paramref name="subtype"/> are markup, which carry an author (<c>/T</c>): ISO 32000-1
    /// §12.5.6.2. A widget's <c>/T</c> is its field's name instead, and links and popups have none.
    /// </summary>
    public static bool IsMarkup(this AnnotationSubtype subtype) => subtype is
        AnnotationSubtype.Text or AnnotationSubtype.FreeText or AnnotationSubtype.Line or AnnotationSubtype.Square or
        AnnotationSubtype.Circle or AnnotationSubtype.Polygon or AnnotationSubtype.PolyLine or
        AnnotationSubtype.Highlight or AnnotationSubtype.Underline or AnnotationSubtype.Squiggly or
        AnnotationSubtype.StrikeOut or AnnotationSubtype.Stamp or AnnotationSubtype.Caret or AnnotationSubtype.Ink or
        AnnotationSubtype.FileAttachment or AnnotationSubtype.Sound or AnnotationSubtype.Redact;
}
```

`CoffeePdf.Core/Pdf/Pdfium.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace CoffeePdf.Core.Pdf;

/// <summary>
/// The PDFium functions CoffeePDF calls itself, for what the PdfViewer doesn't do (spec §4.3, §16 Decision 1). They
/// bind to the <c>libpdfium</c> the PdfViewer ships (bblanchon.PDFium 157), so the process holds one copy.
/// </summary>
/// <remarks>
/// PDFium isn't thread-safe. <see cref="Gate"/> serialises CoffeePDF's own calls. The PdfViewer serialises its own per
/// document, and nothing joins the two, so CoffeePDF keeps its calls few: a save's, on the pages it needs. PDFium
/// ignores <c>FPDF_InitLibrary</c> once it's initialised, and in the app the PdfViewer initialises it first, with its
/// own configuration. Nothing here calls <c>FPDF_DestroyLibrary</c>, which would pull PDFium from under the PdfViewer.
/// </remarks>
internal static unsafe partial class Pdfium
{
    private const string Library = "pdfium";

    /// <summary><c>FPDF_INCREMENTAL</c>: a save that appends to the original bytes, so a signature over them holds.</summary>
    private const uint Incremental = 1;

    public static readonly Lock Gate = new();

    private static bool s_initialised;

    /// <summary>Initialises PDFium, which does nothing if the PdfViewer has. Call under <see cref="Gate"/>.</summary>
    public static void Initialise()
    {
        if (s_initialised)
            return;
        FPDF_InitLibrary();
        s_initialised = true;
    }

    /// <summary>Whether <paramref name="annotation"/>'s dictionary has <paramref name="key"/> (null-terminated ASCII).</summary>
    public static bool HasKey(nint annotation, ReadOnlySpan<byte> key)
    {
        fixed (byte* k = key)
            return FPDFAnnot_HasKey(annotation, k) != 0;
    }

    /// <summary><paramref name="annotation"/>'s string for <paramref name="key"/> (null-terminated ASCII), or null if it has none or it's empty.</summary>
    public static string? GetString(nint annotation, ReadOnlySpan<byte> key)
    {
        fixed (byte* k = key)
        {
            // The length is in bytes of UTF-16, with its two-byte terminator: 2 is empty or missing.
            var length = (int)FPDFAnnot_GetStringValue(annotation, k, null, new CULong(0)).Value;
            if (length <= 2)
                return null;
            var buffer = new char[length / 2];
            fixed (char* b = buffer)
                FPDFAnnot_GetStringValue(annotation, k, b, new CULong((nuint)length));
            return new string(buffer, 0, buffer.Length - 1);
        }
    }

    /// <summary>Sets <paramref name="annotation"/>'s string for <paramref name="key"/> (null-terminated ASCII).</summary>
    public static bool SetString(nint annotation, ReadOnlySpan<byte> key, string value)
    {
        // A string's characters are followed by a null in memory, which PDFium's wide string needs.
        fixed (byte* k = key)
        fixed (char* v = value)
            return FPDFAnnot_SetStringValue(annotation, k, v) != 0;
    }

    /// <summary>Writes <paramref name="document"/> as an incremental update.</summary>
    public static byte[] SaveIncremental(nint document)
    {
        using var output = new MemoryStream();
        var target = GCHandle.Alloc(output);
        try
        {
            var writer = new FileWrite { Version = 1, WriteBlock = &WriteBlock, Target = GCHandle.ToIntPtr(target) };
            if (FPDF_SaveAsCopy(document, &writer, new CULong(Incremental)) == 0)
                throw new IOException($"PDFium couldn't write the document (error {FPDF_GetLastError().Value})");
            return output.ToArray();
        }
        finally
        {
            target.Free();
        }
    }

    /// <summary><c>FPDF_FILEWRITE</c>, and after PDFium's two fields the stream it writes to, found through <c>pThis</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct FileWrite
    {
        public int Version;
        public delegate* unmanaged<FileWrite*, void*, CULong, int> WriteBlock;
        public nint Target;
    }

    [UnmanagedCallersOnly]
    private static int WriteBlock(FileWrite* self, void* data, CULong size)
    {
        try
        {
            var output = (MemoryStream)GCHandle.FromIntPtr(self->Target).Target!;
            output.Write(new ReadOnlySpan<byte>(data, checked((int)size.Value)));
            return 1;
        }
        catch (Exception) // nothing may unwind into PDFium: a 0 fails the save instead
        {
            return 0;
        }
    }

    /// <summary><paramref name="text"/> as null-terminated UTF-8, or null for null.</summary>
    public static byte[]? Utf8(string? text) => text is null ? null : Encoding.UTF8.GetBytes(text + '\0');

    [LibraryImport(Library)] private static partial void FPDF_InitLibrary();
    [LibraryImport(Library)] public static partial nint FPDF_LoadMemDocument64(void* data, nuint size, byte* password);
    [LibraryImport(Library)] public static partial nint FPDF_LoadDocument(byte* path, byte* password);
    [LibraryImport(Library)] public static partial void FPDF_CloseDocument(nint document);
    [LibraryImport(Library)] public static partial CULong FPDF_GetLastError();
    [LibraryImport(Library)] public static partial int FPDF_GetPageCount(nint document);
    [LibraryImport(Library)] public static partial nint FPDF_LoadPage(nint document, int index);
    [LibraryImport(Library)] public static partial void FPDF_ClosePage(nint page);
    [LibraryImport(Library)] public static partial int FPDFPage_GetAnnotCount(nint page);
    [LibraryImport(Library)] public static partial nint FPDFPage_GetAnnot(nint page, int index);
    [LibraryImport(Library)] public static partial void FPDFPage_CloseAnnot(nint annotation);
    [LibraryImport(Library)] public static partial int FPDFAnnot_GetSubtype(nint annotation);
    [LibraryImport(Library)] private static partial int FPDFAnnot_HasKey(nint annotation, byte* key);
    [LibraryImport(Library)] private static partial CULong FPDFAnnot_GetStringValue(nint annotation, byte* key, char* buffer, CULong length);
    [LibraryImport(Library)] private static partial int FPDFAnnot_SetStringValue(nint annotation, byte* key, char* value);
    [LibraryImport(Library)] private static partial int FPDF_SaveAsCopy(nint document, FileWrite* writer, CULong flags);
}

/// <summary>A PDFium document. Bytes it's opened on stay pinned until it's disposed, since PDFium reads them lazily. Use under <see cref="Pdfium.Gate"/>.</summary>
internal sealed unsafe class PdfiumDocument : IDisposable
{
    private GCHandle _pin;

    private PdfiumDocument(nint handle, GCHandle pin)
    {
        Handle = handle;
        _pin = pin;
    }

    public nint Handle { get; }

    public int PageCount => Pdfium.FPDF_GetPageCount(Handle);

    /// <summary>Opens <paramref name="pdf"/>, or returns null if PDFium can't (it isn't a PDF, or needs a password it wasn't given).</summary>
    public static PdfiumDocument? Open(byte[] pdf, string? password)
    {
        Pdfium.Initialise();
        var pin = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        nint handle;
        fixed (byte* p = Pdfium.Utf8(password))
            handle = Pdfium.FPDF_LoadMemDocument64((void*)pin.AddrOfPinnedObject(), (nuint)pdf.Length, p);
        if (handle != 0)
            return new PdfiumDocument(handle, pin);
        pin.Free();
        return null;
    }

    /// <summary>Opens the file at <paramref name="path"/>, or returns null if PDFium can't.</summary>
    public static PdfiumDocument? OpenFile(string path, string? password)
    {
        Pdfium.Initialise();
        nint handle;
        fixed (byte* f = Pdfium.Utf8(path))
        fixed (byte* p = Pdfium.Utf8(password))
            handle = Pdfium.FPDF_LoadDocument(f, p);
        return handle == 0 ? null : new PdfiumDocument(handle, default);
    }

    /// <summary>Calls <paramref name="visit"/> with each annotation on page <paramref name="pageIndex"/>, which is open meanwhile.</summary>
    public void ForEachAnnotation(int pageIndex, Action<nint> visit)
    {
        var page = Pdfium.FPDF_LoadPage(Handle, pageIndex);
        if (page == 0)
            return;
        try
        {
            var count = Pdfium.FPDFPage_GetAnnotCount(page);
            for (var i = 0; i < count; i++)
            {
                var annotation = Pdfium.FPDFPage_GetAnnot(page, i);
                if (annotation == 0)
                    continue;
                try
                {
                    visit(annotation);
                }
                finally
                {
                    Pdfium.FPDFPage_CloseAnnot(annotation);
                }
            }
        }
        finally
        {
            Pdfium.FPDF_ClosePage(page);
        }
    }

    public byte[] SaveIncremental() => Pdfium.SaveIncremental(Handle);

    public void Dispose()
    {
        Pdfium.FPDF_CloseDocument(Handle);
        if (_pin.IsAllocated)
            _pin.Free();
    }
}
```

`CoffeePdf.Core/Pdf/PdfAnnotations.cs`:
```csharp
namespace CoffeePdf.Core.Pdf;

/// <summary>What an annotation is, what it's called (<c>/NM</c>) and who wrote it (<c>/T</c>).</summary>
public sealed record AnnotationFacts(AnnotationSubtype Subtype, string? Name, string? Author);

/// <summary>Reads annotations through PDFium, for the tests and for anything that needs more than the PdfViewer shows.</summary>
public static class PdfAnnotations
{
    /// <summary>The annotations on page <paramref name="pageIndex"/> (0-based) of <paramref name="pdf"/>, in order.</summary>
    public static IReadOnlyList<AnnotationFacts> Read(byte[] pdf, int pageIndex)
    {
        lock (Pdfium.Gate)
        {
            using var document = PdfiumDocument.Open(pdf, password: null)
                ?? throw new InvalidDataException("PDFium can't open these bytes as a PDF");
            var facts = new List<AnnotationFacts>();
            document.ForEachAnnotation(pageIndex, annotation => facts.Add(new AnnotationFacts(
                (AnnotationSubtype)Pdfium.FPDFAnnot_GetSubtype(annotation),
                Pdfium.GetString(annotation, "NM\0"u8),
                Pdfium.GetString(annotation, "T\0"u8))));
            return facts;
        }
    }
}
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests`
Expected: everything passes, including `PdfAnnotationsTests` and the moved `MinimalPdfTests`.

Then run the whole solution: `dotnet test CoffeePdf.sln`.
Expected: everything passes. The PdfViewer's tests run with the key, and skip without it.

- [ ] **Step 7: Commit**

```bash
git add -A Directory.Packages.props CoffeePdf.Core CoffeePdf.Core.Tests CoffeePdf.Tests
git commit -m "Call PDFium from the core, and read annotations' names and authors"
```

---

### Task 2: Writing the Markup author

**Files:**
- Create: `CoffeePdf.Core/Pdf/MarkupAuthor.cs`
- Test: `CoffeePdf.Core.Tests/Pdf/MarkupAuthorTests.cs`

**Interfaces:**
- Consumes (Task 1): `Pdfium`, `PdfiumDocument`, `AnnotationSubtype.IsMarkup()`, `PdfAnnotations.Read`, and `AnnotatedPdf`.
- Produces: `MarkupAuthor.Stamp(byte[] pdf, string? previousFile, IReadOnlyCollection<int> pages, string author, string? password = null) : byte[]`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Pdf/MarkupAuthorTests.cs`:
```csharp
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>Spec §7 and §16 Decision 1: the author goes into the markups made in CoffeePDF, and nowhere else.</summary>
public sealed class MarkupAuthorTests : IDisposable
{
    private const string Author = "Zoë Q. Reviewer";

    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Previous(byte[] pdf)
    {
        var path = Path.Combine(_dir, "previous.pdf");
        File.WriteAllBytes(path, pdf);
        return path;
    }

    [Fact]
    public void A_new_note_gets_the_author_in_an_incremental_update()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        var stamped = MarkupAuthor.Stamp(pdf, previousFile: null, [0], Author);

        Assert.Equal(Author, Assert.Single(PdfAnnotations.Read(stamped, 0)).Author);
        Assert.True(stamped.Length > pdf.Length);
        Assert.Equal(pdf, stamped[..pdf.Length]);
    }

    [Fact]
    public void A_note_with_an_author_keeps_it()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", "Someone Else"));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }

    [Fact]
    public void An_authorless_note_already_in_the_file_stays_authorless()
    {
        var previous = Previous(AnnotatedPdf.Build(AnnotatedPdf.Note("x-1", null)));
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("x-1", null), AnnotatedPdf.Note("av-2", null));

        var annotations = PdfAnnotations.Read(MarkupAuthor.Stamp(pdf, previous, [0], Author), 0);

        Assert.Null(annotations.Single(a => a.Name == "x-1").Author);
        Assert.Equal(Author, annotations.Single(a => a.Name == "av-2").Author);
    }

    [Fact]
    public void A_note_stamped_at_the_last_save_is_stamped_again()
    {
        // The PdfViewer's own copy of the document never has the author, so each save writes it again.
        var previous = Previous(AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", Author)));
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Equal(Author, Assert.Single(PdfAnnotations.Read(MarkupAuthor.Stamp(pdf, previous, [0], Author), 0)).Author);
    }

    [Fact]
    public void A_note_without_a_name_isnt_ours_and_stays_authorless()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note(null, null));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }

    [Fact]
    public void Form_fields_are_left_alone()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Widget("av-1"));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }

    [Fact]
    public void Only_the_pages_given_are_touched()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [], Author));
        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [1], Author));
    }

    [Fact]
    public void A_blank_author_writes_nothing()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], "  "));
    }

    [Fact]
    public void A_pdf_pdfium_cant_open_is_returned_as_it_is()
    {
        var pdf = "%PDF-1.7 not really"u8.ToArray();

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~MarkupAuthorTests`
Expected: the build fails. `MarkupAuthor` doesn't exist yet.

- [ ] **Step 3: Write `MarkupAuthor`**

`CoffeePdf.Core/Pdf/MarkupAuthor.cs`:
```csharp
namespace CoffeePdf.Core.Pdf;

/// <summary>
/// Writes the Markup author (spec §5, §7) into the annotations made in CoffeePDF, since the PdfViewer can't set one
/// (§16 Decision 1). Every save runs it on the bytes the PdfViewer saved, before they're written to the file.
/// </summary>
public static class MarkupAuthor
{
    /// <summary>
    /// Returns <paramref name="pdf"/> with <paramref name="author"/> as the author (<c>/T</c>) of each new markup
    /// annotation on <paramref name="pages"/>, in an incremental update, so the original bytes and any signature
    /// over them are kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A new annotation is a markup annotation that has a name (<c>/NM</c>) and no author, where no authorless
    /// annotation in <paramref name="previousFile"/>, the file the save replaces, has that name. The PdfViewer names
    /// every annotation it makes. So another app's authorless annotations stay authorless, and those made in this
    /// window get the author again at every save, since the PdfViewer's own copy never has it.
    /// </para>
    /// <para>
    /// Returns <paramref name="pdf"/> itself when there's nothing to write: no author, no pages, nothing new, or a
    /// PDF that PDFium can't open, such as an encrypted one without its <paramref name="password"/>. A save never
    /// fails for want of an author.
    /// </para>
    /// </remarks>
    public static byte[] Stamp(byte[] pdf, string? previousFile, IReadOnlyCollection<int> pages, string author, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(author) || pages.Count == 0)
            return pdf;
        lock (Pdfium.Gate)
        {
            if (AuthorlessNames(previousFile, pages, password) is not { } authorless)
                return pdf;
            using var document = PdfiumDocument.Open(pdf, password);
            if (document is null)
                return pdf;
            var stamped = 0;
            foreach (var page in pages.Where(p => p >= 0 && p < document.PageCount))
                document.ForEachAnnotation(page, annotation =>
                {
                    if (IsNew(annotation, authorless.GetValueOrDefault(page)) && Pdfium.SetString(annotation, "T\0"u8, author))
                        stamped++;
                });
            return stamped == 0 ? pdf : document.SaveIncremental();
        }
    }

    private static bool IsNew(nint annotation, HashSet<string>? authorlessBefore) =>
        ((AnnotationSubtype)Pdfium.FPDFAnnot_GetSubtype(annotation)).IsMarkup()
        && !Pdfium.HasKey(annotation, "T\0"u8)
        && Pdfium.GetString(annotation, "NM\0"u8) is { } name
        && authorlessBefore?.Contains(name) != true;

    /// <summary>
    /// The names of the authorless annotations on each of <paramref name="pages"/> in <paramref name="file"/>. Empty
    /// when there's no file yet. Null when it can't be read, since then nothing can be told apart and nothing is stamped.
    /// </summary>
    private static Dictionary<int, HashSet<string>>? AuthorlessNames(string? file, IReadOnlyCollection<int> pages, string? password)
    {
        var names = new Dictionary<int, HashSet<string>>();
        if (file is null || !File.Exists(file))
            return names;
        using var document = PdfiumDocument.OpenFile(file, password);
        if (document is null)
            return null;
        foreach (var page in pages.Where(p => p >= 0 && p < document.PageCount))
        {
            var onPage = names[page] = [];
            document.ForEachAnnotation(page, annotation =>
            {
                if (!Pdfium.HasKey(annotation, "T\0"u8) && Pdfium.GetString(annotation, "NM\0"u8) is { } name)
                    onPage.Add(name);
            });
        }
        return names;
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~MarkupAuthorTests`
Expected: all 9 pass.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Pdf/MarkupAuthor.cs CoffeePdf.Core.Tests/Pdf/MarkupAuthorTests.cs
git commit -m "Write the markup author into the annotations made in CoffeePDF"
```

---

### Task 3: Writing a file whole

**Files:**
- Create: `CoffeePdf.Core/Files/AtomicFile.cs`
- Modify: `CoffeePdf.Core.Tests/CoffeePdf.Core.Tests.csproj`
- Test: `CoffeePdf.Core.Tests/Files/AtomicFileTests.cs`

**Interfaces:**
- Produces: `AtomicFile.Write(string path, ReadOnlySpan<byte> bytes)`, and its temporary file's name parts,
  `AtomicFile.TemporaryPrefix` (`".coffeepdf-"`) and `AtomicFile.TemporarySuffix` (`".tmp"`).

- [ ] **Step 1: Write the failing tests**

The tests set Unix file modes. Those calls raise the platform analyzer's CA1416 warning, an error here, unless the
assembly says it doesn't run on Windows, which it doesn't (spec §10). Add to `CoffeePdf.Core.Tests.csproj`:
```xml
  <ItemGroup>
    <!-- The core's tests run on macOS and Linux (spec §10), so Unix-only calls such as File.SetUnixFileMode are fine. -->
    <AssemblyAttribute Include="System.Runtime.Versioning.UnsupportedOSPlatformAttribute">
      <_Parameter1>windows</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

`CoffeePdf.Core.Tests/Files/AtomicFileTests.cs`:
```csharp
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Tests.Files;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void It_writes_a_new_file()
    {
        var path = Path.Combine(_dir, "new.pdf");

        AtomicFile.Write(path, "new"u8);

        Assert.Equal("new"u8.ToArray(), File.ReadAllBytes(path));
    }

    [Fact]
    public void It_replaces_a_files_bytes_and_leaves_no_temporary_file()
    {
        var path = Path.Combine(_dir, "doc.pdf");
        File.WriteAllBytes(path, "old, and longer"u8.ToArray());

        AtomicFile.Write(path, "new"u8);

        Assert.Equal("new"u8.ToArray(), File.ReadAllBytes(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void It_keeps_the_files_permissions()
    {
        var path = Path.Combine(_dir, "private.pdf");
        File.WriteAllBytes(path, "old"u8.ToArray());
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        AtomicFile.Write(path, "new"u8);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void A_name_too_long_for_a_suffix_still_works()
    {
        // Healdan's copies are named up to 241 bytes (spec §6.8), and a file name can't pass 255.
        var path = Path.Combine(_dir, new string('n', 241) + ".pdf");

        AtomicFile.Write(path, "new"u8);

        Assert.Equal("new"u8.ToArray(), File.ReadAllBytes(path));
    }

    [Fact]
    public void A_failed_rename_leaves_the_target_and_no_temporary_file()
    {
        var target = Directory.CreateDirectory(Path.Combine(_dir, "a-folder.pdf")).FullName;

        Assert.ThrowsAny<IOException>(() => AtomicFile.Write(target, "new"u8));

        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void A_folder_that_cant_be_written_leaves_the_file_as_it_was()
    {
        var path = Path.Combine(_dir, "doc.pdf");
        File.WriteAllBytes(path, "old"u8.ToArray());
        var mode = File.GetUnixFileMode(_dir);
        File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => AtomicFile.Write(path, "new"u8));
        }
        finally
        {
            File.SetUnixFileMode(_dir, mode);
        }

        Assert.Equal("old"u8.ToArray(), File.ReadAllBytes(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~AtomicFileTests`
Expected: the build fails. `AtomicFile` doesn't exist yet.

- [ ] **Step 3: Write `AtomicFile`**

`CoffeePdf.Core/Files/AtomicFile.cs`:
```csharp
namespace CoffeePdf.Core.Files;

/// <summary>
/// Writes a file whole or not at all: to a temporary file beside it, then renamed over it, as the PdfViewer's own
/// save did (spec §16 row 2). A crash part-way leaves the old file, never half a new one, and Healdan's Helper learns
/// to count the rename as a save (§4.4 item 8, §16 Decision 3).
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// The temporary file's name is <c>.coffeepdf-</c>, 32 hex digits and <c>.tmp</c>, whatever the target's name, so
    /// even a Healdan copy's longest name fits. The Helper's watch ignores names like it (§4.4 item 8).
    /// </summary>
    public const string TemporaryPrefix = ".coffeepdf-";

    public const string TemporarySuffix = ".tmp";

    /// <summary>
    /// Writes <paramref name="bytes"/> to <paramref name="path"/>, keeping an existing file's permissions. On failure
    /// the file is as it was, and the exception is the one that stopped it.
    /// </summary>
    public static void Write(string path, ReadOnlySpan<byte> bytes)
    {
        var target = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(target)!, $"{TemporaryPrefix}{Guid.NewGuid():N}{TemporarySuffix}");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsWindows() && File.Exists(target))
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The write's own failure is the one to report.
        }
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~AtomicFileTests`
Expected: all 6 pass.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Files/AtomicFile.cs CoffeePdf.Core.Tests/Files/AtomicFileTests.cs
git commit -m "Write files whole, through a temporary file renamed over them"
```

---

### Task 4: Settings, and the default Markup author

**Files:**
- Create: `CoffeePdf.Core/Settings/SettingsLocation.cs`, `CoffeePdf.Core/Settings/SettingsStore.cs`,
  `CoffeePdf.Core/Settings/UserNames.cs`
- Test: `CoffeePdf.Core.Tests/Settings/SettingsLocationTests.cs`, `CoffeePdf.Core.Tests/Settings/SettingsStoreTests.cs`,
  `CoffeePdf.Core.Tests/Settings/UserNamesTests.cs`

**Interfaces:**
- Consumes (Task 3): `AtomicFile.Write`.
- Produces:
  - `SettingsLocation.OverrideVariable` (`"COFFEEPDF_SETTINGS_DIR"`) and `SettingsLocation.Folder() : string`.
  - `SettingsStore(string folder)`, with `FilePath`, `HealdanEnabled` (`bool`, get and set) and `MarkupAuthor`
    (`string?`, get and set). Blank clears it.
  - `UserNames.FullName() : string`, and `UserNames.Gecos(IEnumerable<string> passwdLines, string user) : string?`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Settings/SettingsLocationTests.cs`:
```csharp
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

/// <summary>One class, since its tests change environment variables and xUnit runs a class's tests one at a time.</summary>
public sealed class SettingsLocationTests : IDisposable
{
    private readonly string? _override = Environment.GetEnvironmentVariable(SettingsLocation.OverrideVariable);
    private readonly string? _xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, _override);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _xdg);
    }

    [Fact]
    public void The_override_wins()
    {
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, "/tmp/coffeepdf-settings");

        Assert.Equal("/tmp/coffeepdf-settings", SettingsLocation.Folder());
    }

    [Fact]
    public void Otherwise_its_the_platforms_own_place()
    {
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, null);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "/tmp/xdg");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var expected = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support", "CoffeePDF")
            : Path.Combine("/tmp/xdg", "coffeepdf");
        Assert.Equal(expected, SettingsLocation.Folder());
    }
}
```

`CoffeePdf.Core.Tests/Settings/SettingsStoreTests.cs`:
```csharp
using System.Text.Json.Nodes;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void With_no_file_Healdan_support_is_off_and_the_author_follows_the_full_name()
    {
        var store = new SettingsStore(Path.Combine(_dir, "not-yet"));

        Assert.False(store.HealdanEnabled);
        Assert.Null(store.MarkupAuthor);
    }

    [Fact]
    public void Settings_are_kept_in_the_shape_the_Helper_reads()
    {
        var store = new SettingsStore(_dir);

        store.HealdanEnabled = true;
        store.MarkupAuthor = "  Test Reviewer ";

        var json = JsonNode.Parse(File.ReadAllText(store.FilePath))!;
        Assert.True(json["healdan"]!["enabled"]!.GetValue<bool>());
        Assert.Equal("Test Reviewer", json["markup"]!["author"]!.GetValue<string>());
        var again = new SettingsStore(_dir);
        Assert.True(again.HealdanEnabled);
        Assert.Equal("Test Reviewer", again.MarkupAuthor);
    }

    [Fact]
    public void A_blank_author_goes_back_to_the_full_name()
    {
        var store = new SettingsStore(_dir) { MarkupAuthor = "Test Reviewer" };

        store.MarkupAuthor = " ";

        Assert.Null(store.MarkupAuthor);
        Assert.Null(JsonNode.Parse(File.ReadAllText(store.FilePath))!["markup"]!["author"]);
    }

    [Fact]
    public void Keys_this_version_doesnt_know_are_kept()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, """{ "later": { "thing": 1 }, "healdan": { "enabled": true, "extra": "x" } }""");

        store.MarkupAuthor = "Test Reviewer";

        var json = JsonNode.Parse(File.ReadAllText(store.FilePath))!;
        Assert.Equal(1, json["later"]!["thing"]!.GetValue<int>());
        Assert.Equal("x", json["healdan"]!["extra"]!.GetValue<string>());
        Assert.True(store.HealdanEnabled);
    }

    [Fact]
    public void A_file_that_cant_be_read_gives_the_defaults_and_the_next_change_replaces_it()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ not json");

        Assert.False(store.HealdanEnabled);
        store.HealdanEnabled = true;

        Assert.True(new SettingsStore(_dir).HealdanEnabled);
    }
}
```

`CoffeePdf.Core.Tests/Settings/UserNamesTests.cs`:
```csharp
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

public sealed class UserNamesTests
{
    private static readonly string[] Passwd =
    [
        "root:x:0:0:root:/root:/bin/bash",
        "tester:x:1000:1000:Test Reviewer,Room 1,,:/home/tester:/bin/bash",
        "blank:x:1001:1001::/home/blank:/bin/bash",
    ];

    [Fact]
    public void The_comment_fields_first_part_is_the_full_name() =>
        Assert.Equal("Test Reviewer", UserNames.Gecos(Passwd, "tester"));

    [Fact]
    public void No_comment_or_no_entry_gives_none()
    {
        Assert.Null(UserNames.Gecos(Passwd, "blank"));
        Assert.Null(UserNames.Gecos(Passwd, "nobody-here"));
    }

    [Fact]
    public void There_is_always_a_name()
    {
        // Asserted, never printed: it's the account's own name.
        Assert.False(string.IsNullOrWhiteSpace(UserNames.FullName()));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~CoffeePdf.Core.Tests.Settings`
Expected: the build fails. `CoffeePdf.Core.Settings` doesn't exist yet.

- [ ] **Step 3: Write the settings**

`CoffeePdf.Core/Settings/SettingsLocation.cs`:
```csharp
namespace CoffeePdf.Core.Settings;

/// <summary>Where CoffeePDF keeps <c>settings.json</c> and <c>recent.json</c> (spec §5).</summary>
public static class SettingsLocation
{
    /// <summary>Overrides the folder, for tests and development, as the Helper's <c>HEALDAN_HELPER_DEV_ROOT</c> does.</summary>
    public const string OverrideVariable = "COFFEEPDF_SETTINGS_DIR";

    /// <summary>
    /// <c>~/Library/Application Support/CoffeePDF/</c> on macOS, and <c>$XDG_CONFIG_HOME/coffeepdf/</c> (or
    /// <c>~/.config/coffeepdf/</c>) elsewhere, unless <see cref="OverrideVariable"/> is set.
    /// </summary>
    public static string Folder()
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } overridden)
            return overridden;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "CoffeePDF");
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".config");
        return Path.Combine(config, "coffeepdf");
    }
}
```

`CoffeePdf.Core/Settings/SettingsStore.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Settings;

/// <summary>
/// The preferences (spec §5), in <c>settings.json</c>. The Helper reads <c>healdan.enabled</c> from it (§4.4 item 5), so
/// its shape is a contract: <c>{ "healdan": { "enabled": false }, "markup": { "author": "…" } }</c>. Keys this version
/// doesn't know are kept. A file that can't be read gives the defaults, and the next change replaces it.
/// </summary>
public sealed class SettingsStore(string folder)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public string FilePath { get; } = Path.Combine(folder, "settings.json");

    /// <summary>Healdan support (§5), off by default. Phase 1 only keeps it, and Phase 2 acts on it.</summary>
    public bool HealdanEnabled
    {
        get => Load()["healdan"]?["enabled"] is JsonValue value && value.TryGetValue(out bool enabled) && enabled;
        set => Change(root => Section(root, "healdan")["enabled"] = value);
    }

    /// <summary>The Markup author the user set (§5), or null to follow the macOS full name. Setting it blank clears it.</summary>
    public string? MarkupAuthor
    {
        get => Load()["markup"]?["author"] is JsonValue value && value.TryGetValue(out string? author) && !string.IsNullOrWhiteSpace(author)
            ? author
            : null;
        set => Change(root =>
        {
            var markup = Section(root, "markup");
            if (string.IsNullOrWhiteSpace(value))
                markup.Remove("author");
            else
                markup["author"] = value.Trim();
        });
    }

    private JsonObject Load()
    {
        try
        {
            return JsonNode.Parse(File.ReadAllBytes(FilePath)) as JsonObject ?? new JsonObject();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new JsonObject();
        }
    }

    private void Change(Action<JsonObject> change)
    {
        var root = Load();
        change(root);
        Directory.CreateDirectory(folder);
        AtomicFile.Write(FilePath, System.Text.Encoding.UTF8.GetBytes(root.ToJsonString(Indented)));
    }

    private static JsonObject Section(JsonObject root, string name)
    {
        if (root[name] is JsonObject section)
            return section;
        section = new JsonObject();
        root[name] = section;
        return section;
    }
}
```

`CoffeePdf.Core/Settings/UserNames.cs`:
```csharp
using System.ComponentModel;
using System.Diagnostics;

namespace CoffeePdf.Core.Settings;

/// <summary>The account's full name, the Markup author's default (spec §5). Never logged or printed.</summary>
public static class UserNames
{
    /// <summary>
    /// macOS's full name (<c>id -F</c>), or on Linux the passwd entry's comment, falling back to the account's user
    /// name. Runs a process on macOS, so callers keep the answer.
    /// </summary>
    public static string FullName()
    {
        var name = OperatingSystem.IsMacOS() ? RunIdF()
            : OperatingSystem.IsLinux() && File.Exists("/etc/passwd") ? Gecos(File.ReadLines("/etc/passwd"), Environment.UserName)
            : null;
        return string.IsNullOrWhiteSpace(name) ? Environment.UserName : name.Trim();
    }

    /// <summary>The first comma-separated part of <paramref name="user"/>'s comment in <paramref name="passwdLines"/>, or null.</summary>
    public static string? Gecos(IEnumerable<string> passwdLines, string user)
    {
        foreach (var line in passwdLines)
        {
            var fields = line.Split(':');
            if (fields.Length >= 5 && fields[0] == user)
                return fields[4].Split(',')[0] is { Length: > 0 } name ? name : null;
        }
        return null;
    }

    private static string? RunIdF()
    {
        try
        {
            var start = new ProcessStartInfo("/usr/bin/id") { RedirectStandardOutput = true, UseShellExecute = false };
            start.ArgumentList.Add("-F");
            using var id = Process.Start(start);
            if (id is null)
                return null;
            var name = id.StandardOutput.ReadToEnd();
            id.WaitForExit();
            return id.ExitCode == 0 ? name : null;
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~CoffeePdf.Core.Tests.Settings`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Settings CoffeePdf.Core.Tests/Settings
git commit -m "Keep the preferences in settings.json, with the full name as the author's default"
```

---

### Task 5: Open Recent's list

**Files:**
- Create: `CoffeePdf.Core/Settings/RecentDocuments.cs`
- Test: `CoffeePdf.Core.Tests/Settings/RecentDocumentsTests.cs`

**Interfaces:**
- Consumes (Task 3): `AtomicFile.Write`.
- Produces: `RecentDocuments(string folder)`, with:
  - `Capacity` (`const int`, 10);
  - `FilePath`;
  - `Paths : IReadOnlyList<string>`, of the files still there, newest first;
  - `Add(string)`, `Remove(string)` and `Clear()`;
  - `event EventHandler? Changed`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Settings/RecentDocumentsTests.cs`:
```csharp
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

public sealed class RecentDocumentsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, []);
        return path;
    }

    [Fact]
    public void Files_are_listed_newest_first_once_each()
    {
        var recent = new RecentDocuments(_dir);
        string a = Touch("a.pdf"), b = Touch("b.pdf");

        recent.Add(a);
        recent.Add(b);
        recent.Add(a);

        Assert.Equal(new[] { a, b }, recent.Paths);
    }

    [Fact]
    public void The_list_holds_the_newest_ten()
    {
        var recent = new RecentDocuments(_dir);
        var files = Enumerable.Range(1, 12).Select(i => Touch($"{i}.pdf")).ToList();

        files.ForEach(recent.Add);

        Assert.Equal(Enumerable.Reverse(files).Take(RecentDocuments.Capacity), recent.Paths);
    }

    [Fact]
    public void Files_can_be_removed_and_the_list_cleared()
    {
        var recent = new RecentDocuments(_dir);
        string a = Touch("a.pdf"), b = Touch("b.pdf");
        recent.Add(a);
        recent.Add(b);

        recent.Remove(b);
        Assert.Equal(new[] { a }, recent.Paths);
        recent.Clear();
        Assert.Empty(recent.Paths);
    }

    [Fact]
    public void A_file_thats_gone_is_left_out()
    {
        var recent = new RecentDocuments(_dir);
        var a = Touch("a.pdf");
        recent.Add(a);

        File.Delete(a);

        Assert.Empty(recent.Paths);
    }

    [Fact]
    public void The_list_is_kept_and_each_change_is_announced()
    {
        var recent = new RecentDocuments(_dir);
        var changes = 0;
        recent.Changed += (_, _) => changes++;
        var a = Touch("a.pdf");

        recent.Add(a);

        Assert.Equal(1, changes);
        Assert.Equal(new[] { a }, new RecentDocuments(_dir).Paths);
    }

    [Fact]
    public void A_file_that_cant_be_read_gives_an_empty_list()
    {
        var recent = new RecentDocuments(_dir);
        File.WriteAllText(recent.FilePath, "{ not json");

        Assert.Empty(recent.Paths);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~RecentDocumentsTests`
Expected: the build fails. `RecentDocuments` doesn't exist yet.

- [ ] **Step 3: Write `RecentDocuments`**

`CoffeePdf.Core/Settings/RecentDocuments.cs`:
```csharp
using System.Text.Json;
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Settings;

/// <summary>
/// File › Open Recent (spec §2.1): the PDFs last opened or saved as, newest first, as a JSON array of full paths in
/// <c>recent.json</c> beside the preferences. Phase 2 keeps Healdan's copies out (§6.10).
/// </summary>
public sealed class RecentDocuments(string folder)
{
    public const int Capacity = 10;

    public string FilePath { get; } = Path.Combine(folder, "recent.json");

    /// <summary>Raised after every change, so each window's Open Recent can follow.</summary>
    public event EventHandler? Changed;

    /// <summary>The files, newest first, leaving out any that are gone.</summary>
    public IReadOnlyList<string> Paths => Load().Where(File.Exists).ToList();

    public void Add(string path)
    {
        var full = Path.GetFullPath(path);
        Save([full, .. Load().Where(p => p != full)]);
    }

    public void Remove(string path)
    {
        var full = Path.GetFullPath(path);
        Save(Load().Where(p => p != full).ToList());
    }

    public void Clear() => Save([]);

    private List<string> Load()
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllBytes(FilePath)) ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private void Save(List<string> paths)
    {
        Directory.CreateDirectory(folder);
        AtomicFile.Write(FilePath, JsonSerializer.SerializeToUtf8Bytes(paths.Take(Capacity).ToList()));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~RecentDocumentsTests`
Expected: all 6 pass.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core/Settings/RecentDocuments.cs CoffeePdf.Core.Tests/Settings/RecentDocumentsTests.cs
git commit -m "Keep Open Recent's list in recent.json"
```

---

### Task 6: The window's save

This replaces Phase 0's `OnSave`, which called the PdfViewer's own `SaveAsync`.

**Files:**
- Create: `CoffeePdf.Core/Documents/DocumentTitle.cs`, `CoffeePdf/Dialogs/MessageDialog.axaml`,
  `CoffeePdf/Dialogs/MessageDialog.axaml.cs`
- Modify: `CoffeePdf/App.axaml.cs`, `CoffeePdf/Windows/DocumentWindow.axaml`, `CoffeePdf/Windows/DocumentWindow.axaml.cs`,
  `CoffeePdf.Tests/Headless.cs`, `CoffeePdf.Tests/CoffeePdf.Tests.csproj`
- Test: `CoffeePdf.Core.Tests/Documents/DocumentTitleTests.cs`, `CoffeePdf.Tests/Windows/DocumentWindowSaveTests.cs`,
  `CoffeePdf.Tests/Windows/TestWindows.cs`, `CoffeePdf.Tests/Dialogs/MessageDialogTests.cs`

**Interfaces:**
- Consumes:
  - Task 2: `MarkupAuthor.Stamp`.
  - Task 3: `AtomicFile.Write`.
  - Task 4: `SettingsStore`, `SettingsLocation.Folder()` and `UserNames.FullName()`.
  - Task 1: `PdfAnnotations.Read`, in the tests.
- Produces:
  - `DocumentTitle.For(string? path, bool edited) : string`.
  - `App.Current` (`App`), `App.Settings` (`SettingsStore`) and `App.MarkupAuthor` (`string`).
  - `DocumentWindow`:
    - `FilePath`, `HasUnsavedChanges` and `Load(string)`;
    - `SaveAsync() : Task<bool>` and `internal SaveToAsync(string target) : Task<bool>`;
    - `internal ReportError : Func<string, string, Task>`, called with the heading and the detail.
  - `MessageDialog.ShowAsync(Window owner, string heading, string detail, IReadOnlyList<string> rightButtons, IReadOnlyList<string>? leftButtons = null) : Task<string?>`.
  - `MessageDialog.ShowErrorAsync(Window owner, string heading, string detail) : Task`.
  - Test helpers in `TestWindows`:
    - `OpenAsync(string path) : Task<(DocumentWindow Window, List<string> Errors)>`, which first sets the Markup author
      to `TestWindows.Author` (`"Test Reviewer"`);
    - `WaitForAsync(Func<bool> condition) : Task`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Core.Tests/Documents/DocumentTitleTests.cs`:
```csharp
using CoffeePdf.Core.Documents;

namespace CoffeePdf.Core.Tests.Documents;

public sealed class DocumentTitleTests
{
    [Fact]
    public void An_empty_window_is_CoffeePDF() => Assert.Equal("CoffeePDF", DocumentTitle.For(null, edited: false));

    [Fact]
    public void A_file_shows_its_name() => Assert.Equal("Report.pdf", DocumentTitle.For("/docs/Report.pdf", edited: false));

    [Fact]
    public void Unsaved_changes_say_Edited_as_Preview_does() =>
        Assert.Equal("Report.pdf — Edited", DocumentTitle.For("/docs/Report.pdf", edited: true));
}
```

`CoffeePdf.Tests/Windows/TestWindows.cs`:
```csharp
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Document windows for the tests. Call these on the UI thread.</summary>
internal static class TestWindows
{
    /// <summary>The Markup author the tests save with: a made-up name, never the account's.</summary>
    public const string Author = "Test Reviewer";

    /// <summary>
    /// Shows a DocumentWindow with <paramref name="path"/> loaded, collecting the errors it would show. It first sets
    /// the Markup author to <see cref="Author"/>, in the run's own settings folder (Headless).
    /// </summary>
    public static async Task<(DocumentWindow Window, List<string> Errors)> OpenAsync(string path)
    {
        App.Current.Settings.MarkupAuthor = Author;
        var errors = new List<string>();
        var window = new DocumentWindow();
        window.ReportError = (heading, detail) =>
        {
            errors.Add($"{heading} {detail}");
            return Task.CompletedTask;
        };
        window.Show();
        await Viewers.ShowAndLoadAsync(window.Viewer, () => window.Load(path));
        return (window, errors);
    }

    /// <summary>Waits until <paramref name="condition"/> holds, letting the UI thread run meanwhile, for at most 10 seconds.</summary>
    public static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition never held");
            await Task.Yield();
        }
    }
}
```

`CoffeePdf.Tests/Windows/DocumentWindowSaveTests.cs`:
```csharp
using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.4, §7 and §16 Decision 1: every save writes the author, keeps signatures, and says when it fails.</summary>
[Trait("Category", "PdfViewer")]
public sealed class DocumentWindowSaveTests : IDisposable
{
    private const string Author = TestWindows.Author;

    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [PdfViewerFact]
    public async Task A_save_writes_the_markup_author_into_a_new_note()
    {
        // This also watches the PdfViewer: the author is written only into annotations it names (/NM).
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var saved = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            return await window.SaveAsync();
        });

        Assert.True(saved);
        var note = Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(path), 0));
        Assert.Equal(Author, note.Author);
    }

    [PdfViewerFact]
    public async Task Another_apps_note_keeps_its_own_author()
    {
        var path = Write("noted.pdf", MinimalPdf.Build(withForeignNote: true));

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            return await window.SaveAsync();
        });

        var authors = PdfAnnotations.Read(File.ReadAllBytes(path), 0).Select(a => a.Author).ToList();
        Assert.Equal(new string?[] { MinimalPdf.ForeignNoteAuthor, Author }, authors);
    }

    [PdfViewerFact]
    public async Task A_signed_pdf_stays_signed_through_two_saves_and_its_notes_get_the_author()
    {
        var original = SignedPdf.Build();
        var path = Write("signed.pdf", original);

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "First");
            await window.SaveAsync();
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "Second");
            return await window.SaveAsync();
        });

        var saved = File.ReadAllBytes(path);
        Assert.Equal(original, saved[..original.Length]);
        SignedPdf.VerifyFirstSignature(saved);
        var notes = PdfAnnotations.Read(saved, 0).Where(a => a.Subtype == AnnotationSubtype.Text).ToList();
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(Author, n.Author));
    }

    [PdfViewerFact]
    public async Task The_title_says_Edited_until_the_save()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (editedTitle, editedUnsaved, savedTitle, savedUnsaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var (title, unsaved) = (window.Title, window.HasUnsavedChanges);
            await window.SaveAsync();
            return (title, unsaved, window.Title, window.HasUnsavedChanges);
        });

        Assert.Equal("fixture.pdf — Edited", editedTitle);
        Assert.True(editedUnsaved);
        Assert.Equal("fixture.pdf", savedTitle);
        Assert.False(savedUnsaved);
    }

    [PdfViewerFact]
    public async Task An_undo_after_a_save_leaves_unsaved_changes()
    {
        // The PdfViewer's IsDirty misses it (spec §16, the planning probe).
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var unsaved = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            await window.Viewer.UndoAsync();
            return window.HasUnsavedChanges;
        });

        Assert.True(unsaved);
    }

    [PdfViewerFact]
    public async Task A_failed_save_keeps_the_changes_unsaved_and_says_so()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());
        var mode = File.GetUnixFileMode(_dir);

        var (saved, unsaved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                return (await window.SaveAsync(), window.HasUnsavedChanges, errorList);
            }
            finally
            {
                File.SetUnixFileMode(_dir, mode);
            }
        });

        Assert.False(saved);
        Assert.True(unsaved);
        Assert.Contains("“fixture.pdf” couldn’t be saved", Assert.Single(errors));
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
    }

    [PdfViewerFact]
    public async Task The_PdfViewers_own_saving_is_off()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (allowed, autoSave) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            return (window.Viewer.AllowDocumentSaving, window.Viewer.AutoSave);
        });

        Assert.False(allowed);
        Assert.False(autoSave);
    }
}
```

`CoffeePdf.Tests/Dialogs/MessageDialogTests.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using CoffeePdf.Dialogs;

namespace CoffeePdf.Tests.Dialogs;

public sealed class MessageDialogTests
{
    [Fact]
    public async Task It_answers_with_the_button_chosen_and_puts_the_last_on_the_right_as_the_default()
    {
        var (answer, rightmostIsDefault) = await Headless.RunAsync(async () =>
        {
            var owner = new Window();
            owner.Show();
            var answering = MessageDialog.ShowAsync(owner, "Heading", "Detail", ["Cancel", "Save"], ["Don't Save"]);
            var dialog = (MessageDialog)owner.OwnedWindows.Single();
            var save = (Button)dialog.RightButtons.Children[^1];
            var isDefault = save.IsDefault;
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var result = await answering;
            owner.Close();
            return (result, isDefault);
        });

        Assert.Equal("Save", answer);
        Assert.True(rightmostIsDefault);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.sln --filter "FullyQualifiedName~DocumentTitleTests|FullyQualifiedName~DocumentWindowSaveTests|FullyQualifiedName~MessageDialogTests"`
Expected: the build fails. `DocumentTitle`, `App.Current`, `App.Settings`, `SaveAsync` returning `Task<bool>`,
`ReportError` and `MessageDialog` don't exist yet.

- [ ] **Step 3: Write the title**

`CoffeePdf.Core/Documents/DocumentTitle.cs`:
```csharp
namespace CoffeePdf.Core.Documents;

/// <summary>A document window's title (spec §6.1's local mode). Phase 2 adds Healdan's titles.</summary>
public static class DocumentTitle
{
    /// <summary>The file's name, with Preview's " — Edited" while it has unsaved changes. An empty window is CoffeePDF.</summary>
    public static string For(string? path, bool edited)
    {
        if (path is null)
            return "CoffeePDF";
        var name = Path.GetFileName(path);
        return edited ? $"{name} — Edited" : name;
    }
}
```

- [ ] **Step 4: Write the message dialog**

`CoffeePdf/Dialogs/MessageDialog.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CoffeePdf.Dialogs.MessageDialog"
        Width="440" SizeToContent="Height" CanResize="False"
        WindowStartupLocation="CenterOwner" ShowInTaskbar="False" Title="">
  <StackPanel Margin="20" Spacing="12">
    <TextBlock x:Name="Heading" FontWeight="SemiBold" TextWrapping="Wrap" />
    <TextBlock x:Name="Detail" TextWrapping="Wrap" />
    <Grid ColumnDefinitions="Auto,*,Auto" Margin="0,8,0,0">
      <StackPanel x:Name="LeftButtons" Orientation="Horizontal" Spacing="8" />
      <StackPanel x:Name="RightButtons" Grid.Column="2" Orientation="Horizontal" Spacing="8" />
    </Grid>
  </StackPanel>
</Window>
```

`CoffeePdf/Dialogs/MessageDialog.axaml.cs`:
```csharp
using Avalonia.Controls;

namespace CoffeePdf.Dialogs;

/// <summary>A modal message over a window, with its buttons in macOS's places: the default on the right.</summary>
public partial class MessageDialog : Window
{
    public MessageDialog() => InitializeComponent();

    /// <summary>
    /// Shows <paramref name="heading"/> and <paramref name="detail"/> over <paramref name="owner"/>, and answers with
    /// the label of the button chosen, or null if the dialog was closed. The last of <paramref name="rightButtons"/>
    /// is the default (Return), and a button labelled Cancel answers Escape.
    /// </summary>
    public static Task<string?> ShowAsync(Window owner, string heading, string detail, IReadOnlyList<string> rightButtons, IReadOnlyList<string>? leftButtons = null)
    {
        var dialog = new MessageDialog();
        dialog.Heading.Text = heading;
        dialog.Detail.Text = detail;
        dialog.Detail.IsVisible = detail.Length > 0;
        foreach (var label in leftButtons ?? [])
            dialog.LeftButtons.Children.Add(dialog.AddButton(label));
        foreach (var label in rightButtons)
            dialog.RightButtons.Children.Add(dialog.AddButton(label));
        if (dialog.RightButtons.Children.LastOrDefault() is Button primary)
            primary.IsDefault = true;
        return dialog.ShowDialog<string?>(owner);
    }

    /// <summary>Tells the user something went wrong, with an OK button.</summary>
    public static Task ShowErrorAsync(Window owner, string heading, string detail) => ShowAsync(owner, heading, detail, ["OK"]);

    private Button AddButton(string label)
    {
        var button = new Button { Content = label, MinWidth = 88, IsCancel = label == "Cancel" };
        button.Click += (_, _) => Close(label);
        return button;
    }
}
```

- [ ] **Step 5: Give the app its settings, and the tests their own folder**

A test below makes a folder read-only, so add the same assembly attribute as Task 3 gave the core's tests, to
`CoffeePdf.Tests/CoffeePdf.Tests.csproj`:
```xml
  <ItemGroup>
    <!-- The app's tests run on macOS (spec §10), so Unix-only calls such as File.SetUnixFileMode are fine. -->
    <AssemblyAttribute Include="System.Runtime.Versioning.UnsupportedOSPlatformAttribute">
      <_Parameter1>windows</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

In `CoffeePdf/App.axaml.cs`, add `using CoffeePdf.Core.Settings;`, and add these members to `App`, replacing
`Initialize`:
```csharp
    private readonly Lazy<string> _fullName = new(UserNames.FullName);

    /// <summary>The running app, from anywhere in it.</summary>
    public static new App Current => (App)Application.Current!;

    /// <summary>The preferences (spec §5), in <see cref="SettingsLocation.Folder"/>.</summary>
    public SettingsStore Settings { get; private set; } = null!;

    /// <summary>The name each save writes into new markups: the one set in Settings, else the macOS full name (spec §5).</summary>
    public string MarkupAuthor => Settings.MarkupAuthor ?? _fullName.Value;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Settings = new SettingsStore(SettingsLocation.Folder());
    }
```

In `CoffeePdf.Tests/Headless.cs`, set the settings folder before the session starts, and add
`using CoffeePdf.Core.Settings;`. Replace the `Session` field with:
```csharp
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
    {
        // The app's settings and Open Recent go to a folder of the run's own, never the real one.
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, Directory.CreateTempSubdirectory("coffeepdf-settings-").FullName);
        return HeadlessUnitTestSession.StartNew(typeof(Headless));
    });
```

- [ ] **Step 6: Write the window's save**

In `CoffeePdf/Windows/DocumentWindow.axaml`, set the PdfViewer's saving off, and replace its comment:
```xml
  <!-- Every save goes through DocumentWindow.SaveToAsync, which writes the markup author (spec §16 Decision 1), so the
       PdfViewer's own saving is off. AutoSave stays off too (spec §6.4): each edit would otherwise become a save. -->
  <pdf:PdfViewer x:Name="Viewer"
                 ViewMode="Continuous"
                 SidebarMode="Thumbnails"
                 AllowDocumentSaving="False"
                 AutoSave="False" />
```

Replace `CoffeePdf/Windows/DocumentWindow.axaml.cs` with:
```csharp
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Dialogs;

namespace CoffeePdf.Windows;

/// <summary>One PDF's window (spec §2.1), hosting the PdfViewer. It's empty until a file is loaded.</summary>
/// <remarks>
/// Every save goes through <see cref="SaveToAsync"/>. The PdfViewer saves to memory, <see cref="MarkupAuthor"/> writes
/// the author into the markups made here, and <see cref="AtomicFile"/> renames the result over the file. The
/// PdfViewer's own saving is off, so its toolbar and shortcuts can't save around that.
/// </remarks>
public partial class DocumentWindow : Window
{
    // The pages an annotation was added to since the file loaded: the only ones a save writes the author on.
    private readonly HashSet<int> _markedPages = [];
    private string? _path;
    private bool _saved;           // a save has worked since the file loaded
    private bool _editedSinceSave; // an undo or redo since that save, which the PdfViewer's IsDirty misses
    private bool _saveFailed;      // the last save failed, though saving to memory cleared IsDirty

    public DocumentWindow()
    {
        InitializeComponent();
        ReportError = (heading, detail) => MessageDialog.ShowErrorAsync(this, heading, detail);
        Viewer.AnnotationAdded += (_, e) => _markedPages.Add(e.Annotation.PageIndex);
        Viewer.UndoRedoStateChanged += (_, _) =>
        {
            if (_saved && !Viewer.IsDirty)
                _editedSinceSave = true;
            UpdateTitle();
        };
        Viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == PdfViewer.IsDirtyProperty)
                UpdateTitle();
        };
    }

    /// <summary>The file shown, or null while the window is empty. Save As moves it to the new file.</summary>
    public string? FilePath => _path;

    /// <summary>Whether the document has changes its file doesn't.</summary>
    public bool HasUnsavedChanges => Viewer.HasDocument && (Viewer.IsDirty || _editedSinceSave || _saveFailed);

    /// <summary>Shows a save's failure, with a heading and the reason. The tests replace it.</summary>
    internal Func<string, string, Task> ReportError { get; set; }

    /// <summary>Shows <paramref name="path"/>.</summary>
    public void Load(string path)
    {
        _path = Path.GetFullPath(path);
        _markedPages.Clear();
        _saved = _editedSinceSave = _saveFailed = false;
        Viewer.Source = _path;
        UpdateTitle();
    }

    /// <summary>Saves to the window's file (⌘S). Returns whether it saved; a failure has been shown.</summary>
    public Task<bool> SaveAsync() => _path is null ? Task.FromResult(false) : SaveToAsync(_path);

    /// <summary>
    /// Writes the document to <paramref name="target"/> (spec §6.4), and the window then shows that file. The
    /// author goes into the new markups, and the file is replaced whole. Returns whether it saved.
    /// </summary>
    internal async Task<bool> SaveToAsync(string target)
    {
        if (!Viewer.HasDocument)
            return false;
        try
        {
            using var memory = new MemoryStream();
            await Viewer.SaveDocumentAsync(memory);
            var pdf = memory.ToArray();
            var previous = _path;
            int[] pages = [.. _markedPages];
            var author = App.Current.MarkupAuthor;
            var password = Viewer.Password;
            await Task.Run(() => AtomicFile.Write(target, MarkupAuthor.Stamp(pdf, previous, pages, author, password)));
            _path = Path.GetFullPath(target);
            _saved = true;
            _editedSinceSave = _saveFailed = false;
            UpdateTitle();
            return true;
        }
        catch (Exception error) // whatever stopped it, the user is told, and the changes stay unsaved
        {
            _saveFailed = true;
            UpdateTitle();
            await ReportError($"“{Path.GetFileName(target)}” couldn’t be saved.", error.Message);
            return false;
        }
    }

    private void UpdateTitle() => Title = DocumentTitle.For(_path, HasUnsavedChanges);

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
                App.Current.OpenFile(path);
    }

    private async void OnSave(object? sender, EventArgs e) => await SaveAsync();

    private void OnClose(object? sender, EventArgs e) => Close();
}
```

- [ ] **Step 7: Run the tests to see them pass**

Load the key, and then run the tests:
```bash
eval "$(grep -E '^[[:space:]]*export[[:space:]]+AVALONIA_LICENSE_KEY=' ~/.zshrc | tail -1)"
dotnet test CoffeePdf.sln
```
Expected: everything passes, including the new tests. That eval prints nothing. Without the key, the PdfViewer's tests
skip and the rest pass.

If `A_save_writes_the_markup_author_into_a_new_note` fails with no author, check that the PdfViewer still writes a
`/NM`. The author rule depends on it (spec §16).

- [ ] **Step 8: Commit**

```bash
git add -A CoffeePdf CoffeePdf.Core CoffeePdf.Core.Tests CoffeePdf.Tests
git commit -m "Save through the window: the PdfViewer to memory, the author written, the file replaced whole"
```

---

### Task 7: Save As and Open Recent

**Files:**
- Create: `CoffeePdf/Windows/ActionCommand.cs`
- Modify: `CoffeePdf/App.axaml.cs`, `CoffeePdf/Windows/DocumentWindow.axaml`, `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Test: `CoffeePdf.Tests/Windows/SaveAsAndRecentTests.cs`

**Interfaces:**
- Consumes:
  - Task 5: `RecentDocuments`.
  - Task 6: `DocumentWindow.SaveToAsync`, `App.Current`, `TestWindows.OpenAsync` and `TestWindows.WaitForAsync`.
- Produces:
  - `App.Recent` (`RecentDocuments`) and `internal App.Windows` (`IReadOnlyList<DocumentWindow>`).
  - `DocumentWindow.SaveAsAsync() : Task<bool>`.
  - `internal DocumentWindow.ShowRecent(IReadOnlyList<string>)`.
  - `internal DocumentWindow.MenuItem(string header) : NativeMenuItem`.
  - `internal sealed class ActionCommand(Action execute, Func<bool>? canExecute = null) : ICommand`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/SaveAsAndRecentTests.cs`:
```csharp
using Avalonia.Controls;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

[Trait("Category", "PdfViewer")]
public sealed class SaveAsAndRecentTests : IDisposable
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
    public async Task Save_As_writes_the_new_file_moves_the_window_to_it_and_leaves_the_old_one()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());
        var copy = Path.Combine(_dir, "copy.pdf");

        var (shown, title, recent) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveToAsync(copy);
            return (window.FilePath, window.Title, App.Current.Recent.Paths);
        });

        Assert.Equal(copy, shown);
        Assert.Equal("copy.pdf", title);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
        Assert.Equal(copy, recent[0]);
    }

    [PdfViewerFact]
    public async Task Open_Recent_lists_the_files_newest_first_then_Clear_Menu()
    {
        string a = Write("a.pdf", MinimalPdf.Build()), b = Write("b.pdf", MinimalPdf.Build());

        var headers = await Headless.RunAsync(() =>
        {
            var window = new DocumentWindow();
            window.ShowRecent([b, a]);
            var shown = window.MenuItem("Open Recent").Menu!.Items.Select(i => i is NativeMenuItem item ? item.Header : "—").ToList();
            window.Close();
            return Task.FromResult(shown);
        });

        Assert.Equal(new string?[] { "b.pdf", "a.pdf", "—", "Clear Menu" }, headers);
    }

    [PdfViewerFact]
    public async Task Choosing_a_recent_file_opens_it_and_opening_a_file_adds_it()
    {
        var path = Write("recent.pdf", MinimalPdf.Build());

        var (opened, listed) = await Headless.RunAsync(async () =>
        {
            var window = new DocumentWindow();
            window.ShowRecent([path]);
            window.MenuItem("recent.pdf").Command!.Execute(null);
            window.Close();
            var shown = App.Current.Windows.FirstOrDefault(w => w.FilePath == path);
            if (shown is null)
                return (false, false);
            await TestWindows.WaitForAsync(() => shown.Viewer.HasDocument);
            shown.Close();
            return (true, App.Current.Recent.Paths.Contains(path));
        });

        Assert.True(opened);
        Assert.True(listed);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter FullyQualifiedName~SaveAsAndRecentTests`
Expected: the build fails. `App.Recent`, `App.Windows`, `ShowRecent` and `MenuItem` don't exist yet.

- [ ] **Step 3: Write the command for menu items built in code**

`CoffeePdf/Windows/ActionCommand.cs`:
```csharp
using System.Windows.Input;

namespace CoffeePdf.Windows;

/// <summary>A command for a menu item built in code, such as Open Recent's, so the tests can run it.</summary>
internal sealed class ActionCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    // Nothing changes CanExecute while a menu is shown: each window rebuilds its Open Recent instead.
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();
}
```

- [ ] **Step 4: Keep the recents in the app**

In `CoffeePdf/App.axaml.cs`:

Add a property beside `Settings`:
```csharp
    /// <summary>File › Open Recent's list (spec §2.1).</summary>
    public RecentDocuments Recent { get; private set; } = null!;

    /// <summary>The document windows open.</summary>
    internal IReadOnlyList<DocumentWindow> Windows => _windows;
```

In `Initialize`, after `Settings = …`:
```csharp
        Recent = new RecentDocuments(SettingsLocation.Folder());
```

In `OpenFile`, replace the first two lines (`if (!PdfFile.IsPdf(path)) return;`) with:
```csharp
        if (!PdfFile.IsPdf(path))
        {
            Recent.Remove(path); // a recent file that's gone, or no longer a PDF
            return;
        }
        Recent.Add(path);
```

- [ ] **Step 5: Add Save As and Open Recent to the window**

In `CoffeePdf/Windows/DocumentWindow.axaml`, replace the File menu's `NativeMenu` with:
```xml
        <NativeMenu>
          <NativeMenuItem Header="Open…" Gesture="Meta+O" Click="OnOpen" />
          <NativeMenuItem Header="Open Recent">
            <NativeMenu />
          </NativeMenuItem>
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Close" Gesture="Meta+W" Click="OnClose" />
          <NativeMenuItem Header="Save" Gesture="Meta+S" Click="OnSave" />
          <NativeMenuItem Header="Save As…" Gesture="Meta+Shift+S" Click="OnSaveAs" />
        </NativeMenu>
```

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`:

At the end of the constructor, add:
```csharp
        ShowRecent(App.Current.Recent.Paths);
        App.Current.Recent.Changed += OnRecentChanged;
        Closed += (_, _) => App.Current.Recent.Changed -= OnRecentChanged;
```

In `SaveToAsync`, after `_path = Path.GetFullPath(target);`, add:
```csharp
            if (_path != previous)
                App.Current.Recent.Add(_path);
```

Add these members:
```csharp
    /// <summary>Save As (⇧⌘S): writes the document to a file the user picks, and the window moves to it.</summary>
    public async Task<bool> SaveAsAsync()
    {
        if (!Viewer.HasDocument)
            return false;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = Path.GetFileName(_path),
            DefaultExtension = "pdf",
            FileTypeChoices = [FilePickerFileTypes.Pdf],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath() is { } target && await SaveToAsync(target);
    }

    /// <summary>Fills File › Open Recent with <paramref name="paths"/>, newest first, and Clear Menu.</summary>
    internal void ShowRecent(IReadOnlyList<string> paths)
    {
        var menu = MenuItem("Open Recent").Menu!;
        menu.Items.Clear();
        foreach (var path in paths)
            menu.Items.Add(new NativeMenuItem { Header = Path.GetFileName(path), Command = new ActionCommand(() => App.Current.OpenFile(path)) });
        if (paths.Count > 0)
            menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem { Header = "Clear Menu", Command = new ActionCommand(App.Current.Recent.Clear, () => paths.Count > 0) });
    }

    /// <summary>The menu bar's item with <paramref name="header"/>, at any depth.</summary>
    internal NativeMenuItem MenuItem(string header) =>
        Find(NativeMenu.GetMenu(this)!, header) ?? throw new InvalidOperationException($"No menu item {header}");

    private static NativeMenuItem? Find(NativeMenu menu, string header)
    {
        foreach (var item in menu.Items.OfType<NativeMenuItem>())
        {
            if (item.Header == header)
                return item;
            if (item.Menu is { } submenu && Find(submenu, header) is { } found)
                return found;
        }
        return null;
    }

    private void OnRecentChanged(object? sender, EventArgs e) => ShowRecent(App.Current.Recent.Paths);

    private async void OnSaveAs(object? sender, EventArgs e) => await SaveAsAsync();
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.sln`, with the key loaded as in Task 6.
Expected: everything passes.

- [ ] **Step 7: Commit**

```bash
git add -A CoffeePdf CoffeePdf.Tests
git commit -m "Add Save As and Open Recent"
```

---

### Task 8: Unsaved changes, quitting, and running with no window

**Files:**
- Create: `CoffeePdf/Dialogs/SaveChoice.cs`
- Modify: `CoffeePdf/Dialogs/MessageDialog.axaml.cs`, `CoffeePdf/Windows/DocumentWindow.axaml.cs`,
  `CoffeePdf/App.axaml.cs`, `CoffeePdf/Program.cs`
- Test: `CoffeePdf.Tests/Windows/ClosingTests.cs`, `CoffeePdf.Tests/Dialogs/MessageDialogTests.cs`

**Interfaces:**
- Consumes:
  - Task 6: `MessageDialog.ShowAsync`, `DocumentWindow.SaveAsync`, `HasUnsavedChanges` and `TestWindows.OpenAsync`.
- Produces:
  - `enum SaveChoice { Save, DontSave, Cancel }`.
  - `MessageDialog.AskToSaveAsync(Window owner, string fileName) : Task<SaveChoice>`.
  - `DocumentWindow`:
    - `CloseWithPromptAsync() : Task<bool>`;
    - `internal AskToSave : Func<Task<SaveChoice>>`.
  - `App.CloseAllAsync(IReadOnlyList<DocumentWindow>) : Task<bool>`, an internal static method.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/ClosingTests.cs`:
```csharp
using CoffeePdf.Core.Pdf;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.8: the usual Save changes? prompt, when a window closes and when CoffeePDF quits.</summary>
[Trait("Category", "PdfViewer")]
public sealed class ClosingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    /// <summary>A window with an unsaved note, whose prompt answers <paramref name="answer"/> and counts its asks.</summary>
    private static async Task<(DocumentWindow Window, Func<bool> Closed, Func<int> Asked)> EditedAsync(string path, SaveChoice answer)
    {
        var (window, _) = await TestWindows.OpenAsync(path);
        var closed = false;
        var asked = 0;
        window.Closed += (_, _) => closed = true;
        window.AskToSave = () =>
        {
            asked++;
            return Task.FromResult(answer);
        };
        await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
        return (window, () => closed, () => asked);
    }

    [PdfViewerFact]
    public async Task A_window_without_changes_closes_without_asking()
    {
        var path = Write("clean.pdf");

        var (result, closed, asked) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var wasClosed = false;
            var asks = 0;
            window.Closed += (_, _) => wasClosed = true;
            window.AskToSave = () => { asks++; return Task.FromResult(SaveChoice.Cancel); };
            return (await window.CloseWithPromptAsync(), wasClosed, asks);
        });

        Assert.True(result);
        Assert.True(closed);
        Assert.Equal(0, asked);
    }

    [PdfViewerFact]
    public async Task Cancel_keeps_the_window_and_its_changes()
    {
        var path = Write("doc.pdf");

        var (result, closed, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, _) = await EditedAsync(path, SaveChoice.Cancel);
            var answer = await window.CloseWithPromptAsync();
            var state = (answer, isClosed(), window.HasUnsavedChanges);
            window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            await window.CloseWithPromptAsync();
            return state;
        });

        Assert.False(result);
        Assert.False(closed);
        Assert.True(unsaved);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
    }

    [PdfViewerFact]
    public async Task Dont_Save_closes_and_leaves_the_file()
    {
        var path = Write("doc.pdf");

        var closed = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, _) = await EditedAsync(path, SaveChoice.DontSave);
            await window.CloseWithPromptAsync();
            return isClosed();
        });

        Assert.True(closed);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
    }

    [PdfViewerFact]
    public async Task Save_saves_then_closes()
    {
        var path = Write("doc.pdf");

        var closed = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, _) = await EditedAsync(path, SaveChoice.Save);
            await window.CloseWithPromptAsync();
            return isClosed();
        });

        Assert.True(closed);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(path), 0)).Author);
    }

    [PdfViewerFact]
    public async Task Closing_the_window_itself_asks_too()
    {
        var path = Write("doc.pdf");

        var (asked, closed) = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, asks) = await EditedAsync(path, SaveChoice.Cancel);
            var prompted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.AskToSave = () =>
            {
                prompted.TrySetResult();
                return Task.FromResult(SaveChoice.Cancel);
            };
            window.Close(); // the red button and ⌘W both come here
            await prompted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var state = (true, isClosed());
            window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            await window.CloseWithPromptAsync();
            return state;
        });

        Assert.True(asked);
        Assert.False(closed);
    }

    [PdfViewerFact]
    public async Task Quitting_closes_each_window_in_turn_and_stops_at_a_Cancel()
    {
        string first = Write("first.pdf"), second = Write("second.pdf");

        var (quit, firstClosed, secondClosed) = await Headless.RunAsync(async () =>
        {
            var (a, aClosed, _) = await EditedAsync(first, SaveChoice.DontSave);
            var (b, bClosed, _) = await EditedAsync(second, SaveChoice.Cancel);
            var result = await App.CloseAllAsync([a, b]);
            var state = (result, aClosed(), bClosed());
            b.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            await b.CloseWithPromptAsync();
            return state;
        });

        Assert.False(quit);
        Assert.True(firstClosed);
        Assert.False(secondClosed);
    }
}
```

Add a test to `CoffeePdf.Tests/Dialogs/MessageDialogTests.cs`:
```csharp
    [Fact]
    public async Task Save_changes_offers_Dont_Save_on_the_left_and_Save_as_the_default()
    {
        var (left, right, answer) = await Headless.RunAsync(async () =>
        {
            var owner = new Window();
            owner.Show();
            var asking = MessageDialog.AskToSaveAsync(owner, "Report.pdf");
            var dialog = (MessageDialog)owner.OwnedWindows.Single();
            var labels = (
                dialog.LeftButtons.Children.Cast<Button>().Select(b => (string)b.Content!).ToList(),
                dialog.RightButtons.Children.Cast<Button>().Select(b => (string)b.Content!).ToList());
            ((Button)dialog.LeftButtons.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var choice = await asking;
            owner.Close();
            return (labels.Item1, labels.Item2, choice);
        });

        Assert.Equal(new[] { "Don’t Save" }, left);
        Assert.Equal(new[] { "Cancel", "Save" }, right);
        Assert.Equal(SaveChoice.DontSave, answer);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~ClosingTests|FullyQualifiedName~MessageDialogTests"`
Expected: the build fails. `SaveChoice`, `AskToSave`, `CloseWithPromptAsync`, `CloseAllAsync` and `AskToSaveAsync`
don't exist yet.

- [ ] **Step 3: Write the prompt**

`CoffeePdf/Dialogs/SaveChoice.cs`:
```csharp
namespace CoffeePdf.Dialogs;

/// <summary>The answer to the Save changes? prompt (spec §6.8).</summary>
public enum SaveChoice
{
    Save,
    DontSave,
    Cancel,
}
```

Add to `MessageDialog`:
```csharp
    /// <summary>The usual Save changes? prompt (spec §6.8): Don’t Save on the left, Cancel and Save on the right.</summary>
    public static async Task<SaveChoice> AskToSaveAsync(Window owner, string fileName) =>
        await ShowAsync(owner,
            $"Do you want to save the changes made to the document “{fileName}”?",
            "Your changes will be lost if you don’t save them.",
            ["Cancel", "Save"], ["Don’t Save"]) switch
        {
            "Save" => SaveChoice.Save,
            "Don’t Save" => SaveChoice.DontSave,
            _ => SaveChoice.Cancel,
        };
```

- [ ] **Step 4: Ask before a window closes**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`:

Add fields:
```csharp
    private bool _closeConfirmed; // asked, or nothing to ask: the next close goes through
    private bool _asking;         // the prompt is showing
```

In the constructor, after `ReportError = …`:
```csharp
        AskToSave = () => MessageDialog.AskToSaveAsync(this, Path.GetFileName(_path) ?? "CoffeePDF");
```

Add members:
```csharp
    /// <summary>Asks Save, Don’t Save or Cancel. The tests replace it.</summary>
    internal Func<Task<SaveChoice>> AskToSave { get; set; }

    /// <summary>
    /// Closes the window, first asking to save its changes, if it has any (spec §6.8). Returns false if it stayed
    /// open: the user cancelled, or the save failed.
    /// </summary>
    public async Task<bool> CloseWithPromptAsync()
    {
        if (_asking)
            return false;
        if (HasUnsavedChanges)
        {
            _asking = true;
            SaveChoice choice;
            try
            {
                choice = await AskToSave();
            }
            finally
            {
                _asking = false;
            }
            if (choice == SaveChoice.Cancel || (choice == SaveChoice.Save && !await SaveAsync()))
                return false;
        }
        _closeConfirmed = true;
        Close();
        return true;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || !HasUnsavedChanges)
            return;
        // The red button, ⌘W, or the app quitting: ask first, then close for real.
        e.Cancel = true;
        _ = CloseWithPromptAsync();
    }
```

- [ ] **Step 5: Ask before quitting, and keep running with no window**

In `CoffeePdf/Program.cs`, keep the app running when its last window closes, as Preview does (owner, 2026-10-30):
```csharp
    [STAThread]
    public static int Main(string[] args) =>
        // Running with no window open, as Preview does (owner, 2026-10-30): ⌘Q quits.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
```

In `CoffeePdf/App.axaml.cs`:

Add a field:
```csharp
    private bool _quitConfirmed;
```

In `OnFrameworkInitializationCompleted`, inside the `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)`
block, before `foreach (var arg …)`, add:
```csharp
            desktop.ShutdownRequested += OnShutdownRequested;
```

Replace `OnActivated` with:
```csharp
    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e.Kind == ActivationKind.Reopen)
        {
            // The Dock icon, clicked with no window open: an empty window, where ⌘O and Open Recent work.
            if (_windows.Count == 0)
                NewWindow().Show();
            return;
        }
        if (e is not FileActivatedEventArgs { Kind: ActivationKind.File } opened)
            return;
        foreach (var file in opened.Files)
            if (file.TryGetLocalPath() is { } path)
                OpenFile(path);
    }
```

Add members:
```csharp
    /// <summary>
    /// Closes <paramref name="windows"/> one at a time, each asking to save its changes (spec §6.8). Stops at the first
    /// that stays open, and returns whether all closed.
    /// </summary>
    internal static async Task<bool> CloseAllAsync(IReadOnlyList<DocumentWindow> windows)
    {
        foreach (var window in windows)
        {
            window.Activate();
            if (!await window.CloseWithPromptAsync())
                return false;
        }
        return true;
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_quitConfirmed || !_windows.Any(w => w.HasUnsavedChanges))
            return;
        e.Cancel = true;
        _ = QuitAsync(sender as IClassicDesktopStyleApplicationLifetime);
    }

    private async Task QuitAsync(IClassicDesktopStyleApplicationLifetime? desktop)
    {
        if (!await CloseAllAsync(_windows.Where(w => w.HasUnsavedChanges).ToList()))
            return;
        _quitConfirmed = true;
        desktop?.Shutdown();
    }
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.sln`, with the key loaded.
Expected: everything passes.

- [ ] **Step 7: Commit**

```bash
git add -A CoffeePdf CoffeePdf.Tests
git commit -m "Ask to save changes before a window closes or CoffeePDF quits, and keep running with no window"
```

---

### Task 9: Settings, and the default PDF app

**Files:**
- Create: `CoffeePdf/Platform/DefaultPdfApp.cs`, `CoffeePdf/Windows/SettingsWindow.axaml`,
  `CoffeePdf/Windows/SettingsWindow.axaml.cs`
- Modify: `CoffeePdf/CoffeePdf.csproj`, `CoffeePdf/App.axaml.cs`
- Test: `CoffeePdf.Tests/Windows/SettingsWindowTests.cs`, `CoffeePdf.Tests/Platform/DefaultPdfAppTests.cs`

**Interfaces:**
- Consumes (Task 4): `SettingsStore` and `UserNames.FullName()`. Also `App.Settings` and `App.MarkupAuthor` from Task 6.
- Produces:
  - `internal interface IDefaultPdfApp { bool IsSupported; bool IsCoffeePdf(); bool MakeCoffeePdf(); }`.
  - `internal sealed class MacDefaultPdfApp : IDefaultPdfApp`.
  - `SettingsWindow()`, and `internal SettingsWindow(SettingsStore store, string fullName, IDefaultPdfApp defaultApp)`.
  - The app menu's `Settings…` (⌘,), and `App.ShowSettings()`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/SettingsWindowTests.cs`:
```csharp
using CoffeePdf.Core.Settings;
using CoffeePdf.Platform;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §5: the Markup author, defaulting to the full name, and the Default PDF app button.</summary>
public sealed class SettingsWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class FakeDefaultApp(bool supported, bool isDefault) : IDefaultPdfApp
    {
        public bool IsDefault { get; private set; } = isDefault;
        public bool IsSupported => supported;
        public bool IsCoffeePdf() => IsDefault;
        public bool MakeCoffeePdf() => IsDefault = true;
    }

    [Fact]
    public async Task It_shows_the_author_set_with_the_full_name_as_the_placeholder()
    {
        var store = new SettingsStore(_dir) { MarkupAuthor = "Set Name" };

        var (text, placeholder) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(store, "Default Name", new FakeDefaultApp(true, true));
            return Task.FromResult((window.AuthorBox.Text, window.AuthorBox.PlaceholderText));
        });

        Assert.Equal("Set Name", text);
        Assert.Equal("Default Name", placeholder);
    }

    [Fact]
    public async Task Typing_a_name_keeps_it_and_clearing_it_goes_back_to_the_full_name()
    {
        var store = new SettingsStore(_dir);

        var (typed, cleared) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(store, "Default Name", new FakeDefaultApp(true, true));
            window.AuthorBox.Text = "New Name";
            var afterTyping = new SettingsStore(_dir).MarkupAuthor;
            window.AuthorBox.Text = "";
            return Task.FromResult((afterTyping, new SettingsStore(_dir).MarkupAuthor));
        });

        Assert.Equal("New Name", typed);
        Assert.Null(cleared);
    }

    [Fact]
    public async Task The_button_makes_CoffeePDF_the_default_and_then_goes()
    {
        var defaultApp = new FakeDefaultApp(true, false);

        var (before, after, status) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(new SettingsStore(_dir), "Default Name", defaultApp);
            var shown = window.MakeDefaultButton.IsVisible;
            window.MakeDefault();
            return Task.FromResult((shown, window.MakeDefaultButton.IsVisible, window.DefaultAppStatus.Text));
        });

        Assert.True(before);
        Assert.True(defaultApp.IsDefault);
        Assert.False(after);
        Assert.Equal("CoffeePDF opens PDFs.", status);
    }

    [Fact]
    public async Task Where_theres_no_default_app_to_set_the_section_is_hidden()
    {
        var shown = await Headless.RunAsync(() =>
            Task.FromResult(new SettingsWindow(new SettingsStore(_dir), "Default Name", new FakeDefaultApp(false, false)).DefaultAppSection.IsVisible));

        Assert.False(shown);
    }
}
```

`CoffeePdf.Tests/Platform/DefaultPdfAppTests.cs`:
```csharp
using CoffeePdf.Platform;

namespace CoffeePdf.Tests.Platform;

public sealed class DefaultPdfAppTests
{
    [Fact]
    public void It_reads_the_default_without_changing_it()
    {
        // Reads Launch Services on macOS. MakeCoffeePdf is never called here: it would change the Mac's default.
        var app = new MacDefaultPdfApp();

        Assert.Equal(OperatingSystem.IsMacOS(), app.IsSupported);
        var isDefault = app.IsCoffeePdf(); // true or false, without throwing
        Assert.True(isDefault || !isDefault);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~SettingsWindowTests|FullyQualifiedName~DefaultPdfAppTests"`
Expected: the build fails. `SettingsWindow` and `CoffeePdf.Platform` don't exist yet.

- [ ] **Step 3: Write the default PDF app**

In `CoffeePdf/CoffeePdf.csproj`, add to the `PropertyGroup`:
```xml
    <!-- Platform/DefaultPdfApp.cs passes pointers to Core Foundation. -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

`CoffeePdf/Platform/DefaultPdfApp.cs`:
```csharp
using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>Settings' Default PDF app (spec §5): whether CoffeePDF opens PDFs, and a way to make it.</summary>
internal interface IDefaultPdfApp
{
    /// <summary>Whether this platform has a default to read and set.</summary>
    bool IsSupported { get; }

    bool IsCoffeePdf();

    /// <summary>Makes CoffeePDF the default for PDFs. Returns whether Launch Services took it.</summary>
    bool MakeCoffeePdf();
}

/// <summary>The default PDF app through macOS's Launch Services, for every role, by CoffeePDF's bundle id.</summary>
internal sealed unsafe partial class MacDefaultPdfApp : IDefaultPdfApp
{
    public const string BundleId = "com.healdan.coffeepdf";

    private const string PdfType = "com.adobe.pdf";
    private const uint AllRoles = 0xFFFFFFFF;
    private const string CoreServices = "/System/Library/Frameworks/CoreServices.framework/CoreServices";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    public bool IsSupported => OperatingSystem.IsMacOS();

    public bool IsCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CfString(PdfType);
        try
        {
            var handler = LSCopyDefaultRoleHandlerForContentType(type, AllRoles);
            if (handler == 0)
                return false;
            try
            {
                return string.Equals(ManagedString(handler), BundleId, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                CFRelease(handler);
            }
        }
        finally
        {
            CFRelease(type);
        }
    }

    public bool MakeCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CfString(PdfType);
        var bundle = CfString(BundleId);
        try
        {
            return LSSetDefaultRoleHandlerForContentType(type, AllRoles, bundle) == 0;
        }
        finally
        {
            CFRelease(bundle);
            CFRelease(type);
        }
    }

    private static nint CfString(string text)
    {
        fixed (char* characters = text)
            return CFStringCreateWithCharacters(0, characters, text.Length);
    }

    private static string ManagedString(nint cfString)
    {
        var length = CFStringGetLength(cfString);
        var buffer = new char[length];
        fixed (char* b = buffer)
            CFStringGetCharacters(cfString, new CFRange { Location = 0, Length = length }, b);
        return new string(buffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CFRange
    {
        public nint Location;
        public nint Length;
    }

    // Deprecated since macOS 12, and still the plain C call for it.
    [LibraryImport(CoreServices)] private static partial nint LSCopyDefaultRoleHandlerForContentType(nint contentType, uint role);
    [LibraryImport(CoreServices)] private static partial int LSSetDefaultRoleHandlerForContentType(nint contentType, uint role, nint handlerBundleId);
    [LibraryImport(CoreFoundation)] private static partial nint CFStringCreateWithCharacters(nint allocator, char* characters, nint length);
    [LibraryImport(CoreFoundation)] private static partial nint CFStringGetLength(nint text);
    [LibraryImport(CoreFoundation)] private static partial void CFStringGetCharacters(nint text, CFRange range, char* buffer);
    [LibraryImport(CoreFoundation)] private static partial void CFRelease(nint cf);
}
```

- [ ] **Step 4: Write the Settings window**

`CoffeePdf/Windows/SettingsWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="CoffeePdf.Windows.SettingsWindow"
        Title="Settings" Width="480" SizeToContent="Height" CanResize="False">
  <StackPanel Margin="20" Spacing="20">
    <StackPanel Spacing="6">
      <TextBlock Text="Markup author" FontWeight="SemiBold" />
      <TextBox x:Name="AuthorBox" />
      <TextBlock TextWrapping="Wrap" Opacity="0.7"
                 Text="The name written into each markup you add. Leave it empty to use your macOS full name." />
    </StackPanel>
    <StackPanel x:Name="DefaultAppSection" Spacing="6">
      <TextBlock Text="Default PDF app" FontWeight="SemiBold" />
      <TextBlock x:Name="DefaultAppStatus" TextWrapping="Wrap" />
      <Button x:Name="MakeDefaultButton" Content="Make CoffeePDF the Default PDF App" Click="OnMakeDefault" />
    </StackPanel>
  </StackPanel>
</Window>
```

`CoffeePdf/Windows/SettingsWindow.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using CoffeePdf.Core.Settings;
using CoffeePdf.Platform;

namespace CoffeePdf.Windows;

/// <summary>
/// Settings (spec §5): the Markup author, and the Default PDF app. Healdan support joins it in Phase 2, when it does
/// something. Each change is kept at once.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IDefaultPdfApp? _defaultApp;

    public SettingsWindow() => InitializeComponent();

    internal SettingsWindow(SettingsStore store, string fullName, IDefaultPdfApp defaultApp) : this()
    {
        _defaultApp = defaultApp;
        AuthorBox.Text = store.MarkupAuthor ?? "";
        AuthorBox.PlaceholderText = fullName;
        AuthorBox.TextChanged += (_, _) => store.MarkupAuthor = AuthorBox.Text;
        DefaultAppSection.IsVisible = defaultApp.IsSupported;
        ShowDefaultApp();
    }

    /// <summary>The Default PDF app button.</summary>
    internal void MakeDefault()
    {
        _defaultApp?.MakeCoffeePdf();
        ShowDefaultApp();
    }

    private void ShowDefaultApp()
    {
        var isDefault = _defaultApp?.IsCoffeePdf() == true;
        DefaultAppStatus.Text = isDefault ? "CoffeePDF opens PDFs." : "Another app opens PDFs.";
        MakeDefaultButton.IsVisible = !isDefault;
    }

    private void OnMakeDefault(object? sender, RoutedEventArgs e) => MakeDefault();
}
```

- [ ] **Step 5: Put Settings… in the app menu**

In `CoffeePdf/App.axaml.cs`, add `using Avalonia.Input;` and `using CoffeePdf.Platform;`.

Add a field:
```csharp
    private SettingsWindow? _settingsWindow;
```

At the end of `Initialize`, add the app menu. Avalonia keeps macOS's own About, Hide and Quit beside it:
```csharp
        var settings = new NativeMenuItem { Header = "Settings…", Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta) };
        settings.Click += (_, _) => ShowSettings();
        var appMenu = new NativeMenu();
        appMenu.Items.Add(settings);
        NativeMenu.SetMenu(this, appMenu);
```

Add a member:
```csharp
    /// <summary>Settings… (⌘,): one window, brought forward if it's open.</summary>
    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(Settings, _fullName.Value, new MacDefaultPdfApp());
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.sln`, with the key loaded.
Expected: everything passes. `SettingsWindowTests` and `DefaultPdfAppTests` use `[Fact]`, so they run without the key
too.

- [ ] **Step 7: Commit**

```bash
git add -A CoffeePdf CoffeePdf.Tests
git commit -m "Add Settings, with the Markup author and the default PDF app"
```

---

### Task 10: The menu bar, and printing

**Files:**
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml`, `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Test: `CoffeePdf.Tests/Windows/MenuTests.cs`

**Interfaces:**
- Consumes (Task 7): `DocumentWindow.MenuItem(string)`, plus `TestWindows.OpenAsync` and `TestWindows.WaitForAsync`
  from Tasks 6 and 7.
- Produces:
  - `DocumentWindow.PrintAsync() : Task`.
  - `internal` handlers, which the tests call: `OnTool`, `OnViewMode`, `OnSidebar`, `OnZoomIn` and `OnUndo`.

- [ ] **Step 1: Write the failing tests**

`CoffeePdf.Tests/Windows/MenuTests.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Pdf.Services;
using Avalonia.Input;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §2.1: the PdfViewer's viewing, markup and printing, from a Mac menu bar with Preview's shortcuts.</summary>
[Trait("Category", "PdfViewer")]
public sealed class MenuTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Fixture()
    {
        var path = Path.Combine(_dir, "fixture.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    private static readonly (string Header, string Gesture)[] Shortcuts =
    [
        ("Open…", "Meta+O"), ("Close", "Meta+W"), ("Save", "Meta+S"), ("Save As…", "Meta+Shift+S"), ("Print…", "Meta+P"),
        ("Undo", "Meta+Z"), ("Redo", "Meta+Shift+Z"), ("Find Next", "Meta+G"), ("Find Previous", "Meta+Shift+G"),
        ("Actual Size", "Meta+D0"), ("Zoom to Fit", "Meta+D9"), ("Zoom In", "Meta+OemPlus"), ("Zoom Out", "Meta+OemMinus"),
        ("Hide Sidebar", "Alt+Meta+D1"), ("Thumbnails", "Alt+Meta+D2"), ("Table of Contents", "Alt+Meta+D3"),
        ("Bookmarks", "Alt+Meta+D4"), ("Previous Page", "Alt+Meta+Up"), ("Next Page", "Alt+Meta+Down"),
        ("Toggle Bookmark", "Meta+D"), ("Highlight", "Ctrl+Meta+H"), ("Underline", "Ctrl+Meta+U"),
        ("Strikethrough", "Ctrl+Meta+S"), ("Text", "Ctrl+Meta+T"), ("Note", "Ctrl+Meta+N"),
    ];

    [PdfViewerFact]
    public async Task Each_menu_item_has_Previews_shortcut()
    {
        var wrong = await Headless.RunAsync(() =>
        {
            var window = new DocumentWindow();
            var mismatched = Shortcuts.Where(s => !Equals(window.MenuItem(s.Header).Gesture, KeyGesture.Parse(s.Gesture)))
                .Select(s => s.Header).ToList();
            window.Close();
            return Task.FromResult(mismatched);
        });

        Assert.Empty(wrong);
    }

    [PdfViewerFact]
    public async Task The_tool_view_and_sidebar_items_drive_the_PdfViewer()
    {
        // With a document open: the PdfViewer keeps its annotation tools off until there's one (its 12.0.1 changelog).
        var path = Fixture();

        var (tool, view, sidebar) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.OnTool(window.MenuItem("Highlight"), EventArgs.Empty);
            window.OnViewMode(window.MenuItem("Two Pages"), EventArgs.Empty);
            window.OnSidebar(window.MenuItem("Table of Contents"), EventArgs.Empty);
            return (window.Viewer.ActiveTool, window.Viewer.ViewMode, window.Viewer.SidebarMode);
        });

        Assert.Equal(PdfViewerTool.Highlight, tool);
        Assert.Equal(PdfViewMode.TwoPages, view);
        Assert.Equal(SidebarMode.TableOfContents, sidebar);
    }

    [PdfViewerFact]
    public async Task Undo_and_Redo_follow_the_PdfViewer()
    {
        var path = Fixture();

        var states = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var seen = new List<(bool Undo, bool Redo)> { (window.MenuItem("Undo").IsEnabled, window.MenuItem("Redo").IsEnabled) };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            seen.Add((window.MenuItem("Undo").IsEnabled, window.MenuItem("Redo").IsEnabled));
            window.OnUndo(window.MenuItem("Undo"), EventArgs.Empty);
            await TestWindows.WaitForAsync(() => window.MenuItem("Redo").IsEnabled);
            seen.Add((window.MenuItem("Undo").IsEnabled, window.MenuItem("Redo").IsEnabled));
            return seen;
        });

        Assert.Equal(new[] { (false, false), (true, false), (false, true) }, states);
    }

    [PdfViewerFact]
    public async Task Zoom_In_zooms_in()
    {
        var path = Fixture();

        var (before, after) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var zoom = window.Viewer.ZoomLevel;
            window.OnZoomIn(window.MenuItem("Zoom In"), EventArgs.Empty);
            return (zoom, window.Viewer.ZoomLevel);
        });

        Assert.True(after > before);
    }

    [PdfViewerFact]
    public async Task Print_sends_the_document_to_the_print_service_under_its_name()
    {
        var path = Fixture();
        var printer = new FakePrinter();

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Viewer.PrintService = printer;
            await window.PrintAsync();
            return true;
        });

        Assert.Equal("fixture.pdf", Assert.Single(printer.Titles));
    }

    private sealed class FakePrinter : IPrintService
    {
        public List<string?> Titles { get; } = [];
        public bool IsSupported => true;

        public Task PrintAsync(IPdfDocument document, PrintOptions? options = null, CancellationToken cancellationToken = default)
        {
            Titles.Add(options?.Title);
            return Task.CompletedTask;
        }

        public Task ShowPrintPreviewAsync(IPdfDocument document, PrintOptions? options = null) => Task.CompletedTask;
    }

}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter FullyQualifiedName~MenuTests`
Expected: the build fails. `OnTool`, `OnViewMode`, `OnSidebar`, `OnUndo`, `OnZoomIn` and `PrintAsync` don't exist yet.

- [ ] **Step 3: Write the menu bar**

Replace the `<NativeMenu.Menu>` element of `CoffeePdf/Windows/DocumentWindow.axaml` with:
```xml
  <NativeMenu.Menu>
    <NativeMenu>
      <NativeMenuItem Header="File">
        <NativeMenu>
          <NativeMenuItem Header="Open…" Gesture="Meta+O" Click="OnOpen" />
          <NativeMenuItem Header="Open Recent">
            <NativeMenu />
          </NativeMenuItem>
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Close" Gesture="Meta+W" Click="OnClose" />
          <NativeMenuItem Header="Save" Gesture="Meta+S" Click="OnSave" />
          <NativeMenuItem Header="Save As…" Gesture="Meta+Shift+S" Click="OnSaveAs" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Print…" Gesture="Meta+P" Click="OnPrint" />
        </NativeMenu>
      </NativeMenuItem>
      <NativeMenuItem Header="Edit">
        <!-- Copy (⌘C) and Select All (⌘A) are the PdfViewer's own shortcuts, so they work in its text fields too. -->
        <NativeMenu>
          <NativeMenuItem Header="Undo" Gesture="Meta+Z" Click="OnUndo" IsEnabled="False" />
          <NativeMenuItem Header="Redo" Gesture="Meta+Shift+Z" Click="OnRedo" IsEnabled="False" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Find Next" Gesture="Meta+G" Click="OnFindNext" />
          <NativeMenuItem Header="Find Previous" Gesture="Meta+Shift+G" Click="OnFindPrevious" />
        </NativeMenu>
      </NativeMenuItem>
      <NativeMenuItem Header="View">
        <NativeMenu>
          <NativeMenuItem Header="Actual Size" Gesture="Meta+D0" Click="OnActualSize" />
          <NativeMenuItem Header="Zoom to Fit" Gesture="Meta+D9" Click="OnZoomToFit" />
          <NativeMenuItem Header="Zoom In" Gesture="Meta+OemPlus" Click="OnZoomIn" />
          <NativeMenuItem Header="Zoom Out" Gesture="Meta+OemMinus" Click="OnZoomOut" />
          <NativeMenuItem Header="Fit Width" Click="OnFitWidth" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Single Page" Click="OnViewMode" CommandParameter="SinglePage" />
          <NativeMenuItem Header="Continuous" Click="OnViewMode" CommandParameter="Continuous" />
          <NativeMenuItem Header="Two Pages" Click="OnViewMode" CommandParameter="TwoPages" />
          <NativeMenuItem Header="Two Pages Continuous" Click="OnViewMode" CommandParameter="TwoPagesContinuous" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Hide Sidebar" Gesture="Alt+Meta+D1" Click="OnSidebar" CommandParameter="None" />
          <NativeMenuItem Header="Thumbnails" Gesture="Alt+Meta+D2" Click="OnSidebar" CommandParameter="Thumbnails" />
          <NativeMenuItem Header="Table of Contents" Gesture="Alt+Meta+D3" Click="OnSidebar" CommandParameter="TableOfContents" />
          <NativeMenuItem Header="Bookmarks" Gesture="Alt+Meta+D4" Click="OnSidebar" CommandParameter="Bookmarks" />
        </NativeMenu>
      </NativeMenuItem>
      <NativeMenuItem Header="Go">
        <NativeMenu>
          <NativeMenuItem Header="Previous Page" Gesture="Alt+Meta+Up" Click="OnPreviousPage" />
          <NativeMenuItem Header="Next Page" Gesture="Alt+Meta+Down" Click="OnNextPage" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Toggle Bookmark" Gesture="Meta+D" Click="OnToggleBookmark" />
        </NativeMenu>
      </NativeMenuItem>
      <NativeMenuItem Header="Tools">
        <NativeMenu>
          <NativeMenuItem Header="Annotate">
            <NativeMenu>
              <NativeMenuItem Header="Select Text" Click="OnTool" CommandParameter="None" />
              <NativeMenuItemSeparator />
              <NativeMenuItem Header="Highlight" Gesture="Ctrl+Meta+H" Click="OnTool" CommandParameter="Highlight" />
              <NativeMenuItem Header="Underline" Gesture="Ctrl+Meta+U" Click="OnTool" CommandParameter="Underline" />
              <NativeMenuItem Header="Strikethrough" Gesture="Ctrl+Meta+S" Click="OnTool" CommandParameter="Strikeout" />
              <NativeMenuItem Header="Squiggly" Click="OnTool" CommandParameter="Squiggly" />
              <NativeMenuItem Header="Text" Gesture="Ctrl+Meta+T" Click="OnTool" CommandParameter="Text" />
              <NativeMenuItem Header="Note" Gesture="Ctrl+Meta+N" Click="OnTool" CommandParameter="Note" />
              <NativeMenuItem Header="Draw" Click="OnTool" CommandParameter="Draw" />
              <NativeMenuItem Header="Shape" Click="OnTool" CommandParameter="Shape" />
              <NativeMenuItem Header="Stamp" Click="OnTool" CommandParameter="Stamp" />
            </NativeMenu>
          </NativeMenuItem>
        </NativeMenu>
      </NativeMenuItem>
    </NativeMenu>
  </NativeMenu.Menu>
```

Hide the PdfViewer's Redact tool, since true redaction is v2's (spec §2.2). Add `IsRedactToolVisible="False"` to the
`pdf:PdfViewer` element.

- [ ] **Step 4: Write the handlers and printing**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`:

Add `using Avalonia.Controls.Pdf.Services;`.

In the constructor's `UndoRedoStateChanged` handler, before `UpdateTitle();`, add:
```csharp
            MenuItem("Undo").IsEnabled = Viewer.CanUndo;
            MenuItem("Redo").IsEnabled = Viewer.CanRedo;
```

At the end of `Load`, add:
```csharp
        MenuItem("Undo").IsEnabled = MenuItem("Redo").IsEnabled = false;
```

Add members:
```csharp
    /// <summary>Print… (⌘P): the system print dialog, with the unsaved markups, under the file's name.</summary>
    public Task PrintAsync() => Viewer.PrintAsync(new PrintOptions { Title = Path.GetFileName(_path) ?? "CoffeePDF" });

    private static T Parameter<T>(object? sender) where T : struct, Enum =>
        Enum.Parse<T>((string)((NativeMenuItem)sender!).CommandParameter!);

    internal void OnTool(object? sender, EventArgs e) => Viewer.ActiveTool = Parameter<PdfViewerTool>(sender);
    internal void OnViewMode(object? sender, EventArgs e) => Viewer.ViewMode = Parameter<PdfViewMode>(sender);
    internal void OnSidebar(object? sender, EventArgs e) => Viewer.SidebarMode = Parameter<SidebarMode>(sender);
    internal void OnUndo(object? sender, EventArgs e) => Viewer.Undo();
    internal void OnZoomIn(object? sender, EventArgs e) => Viewer.ZoomIn();
    private void OnRedo(object? sender, EventArgs e) => Viewer.Redo();
    private void OnFindNext(object? sender, EventArgs e) => Viewer.FindNext();
    private void OnFindPrevious(object? sender, EventArgs e) => Viewer.FindPrevious();
    private void OnActualSize(object? sender, EventArgs e) => Viewer.ResetZoom();
    private void OnZoomToFit(object? sender, EventArgs e) => Viewer.FitPage();
    private void OnZoomOut(object? sender, EventArgs e) => Viewer.ZoomOut();
    private void OnFitWidth(object? sender, EventArgs e) => Viewer.FitWidth();
    private void OnPreviousPage(object? sender, EventArgs e) => Viewer.PreviousPage();
    private void OnNextPage(object? sender, EventArgs e) => Viewer.NextPage();
    private void OnToggleBookmark(object? sender, EventArgs e) => Viewer.ToggleBookmarkCurrentPage();
    private async void OnPrint(object? sender, EventArgs e) => await PrintAsync();
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.sln`, with the key loaded.
Expected: everything passes.

- [ ] **Step 6: Commit**

```bash
git add -A CoffeePdf CoffeePdf.Tests
git commit -m "Add the menu bar, with Preview's shortcuts, and printing"
```

---

### Task 11: Docs, the run on the Mac, and the owner's checks

**Files:**
- Modify: `CLAUDE.md`, `README.md`, `docs/ROADMAP.md`, `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`

- [ ] **Step 1: Record the rules this phase added**

In `CLAUDE.md`, add these bullets under **Rules**:
```markdown
- **Every save goes through `DocumentWindow.SaveToAsync`:** the PdfViewer saves to memory, `MarkupAuthor` writes the
  author, and `AtomicFile` replaces the file. The PdfViewer's `AllowDocumentSaving` stays off (spec §16 Decision 1).
- **PDFium is shared with the PdfViewer.** The core's calls take `Pdfium.Gate`, and nothing calls `FPDF_DestroyLibrary`.
- **Tests never touch the real settings:** `Headless` sets `COFFEEPDF_SETTINGS_DIR`, and core tests pass a folder.
```

In `README.md`, change the status line's "in Phase 0 (the foundation spike)" to "in Phase 1 (the standalone app)".

- [ ] **Step 2: Run everything, with and without the key**

```bash
dotnet test CoffeePdf.sln
```
Expected without the key: everything passes, and the PdfViewer's tests skip.

```bash
eval "$(grep -E '^[[:space:]]*export[[:space:]]+AVALONIA_LICENSE_KEY=' ~/.zshrc | tail -1)"
dotnet test CoffeePdf.sln
```
Expected with the key: everything passes, and nothing skips except `What_the_viewer_does_without_a_licence`.

- [ ] **Step 3: Build a package for the owner's checks**

Sign it with the owner's Developer ID, with `SIGN_IDENTITY` set as on 2026-10-29. Since §16 Decision 4, an unsigned
package launches too, but only on the Mac that built it.
```bash
packaging/mac/package.sh 0.1.0-phase1
```
Expected: `packaging/mac/out/CoffeePDF-0.1.0-phase1.dmg`.

- [ ] **Step 4: Update the roadmap and the spec**

In `docs/ROADMAP.md`, set Phase 1's status to "built YYYY-MM-DD; the owner's checks pending", with the day it's built.

In the spec's §16, add a short "Phase 1 (built YYYY-MM-DD)" paragraph after the planning probe's. It records:
- what the tests showed;
- anything the build found that this plan didn't expect.

- [ ] **Step 5: List the owner's checks in the pull request**

Each pull request that changes the look or the Mac's integration lists its checks for the owner (spec §8). Run them on
the Mac with the package from Step 3:

1. **The author:**
   - Mark up a PDF with a highlight, a note, a text box, a drawing, a shape and a stamp. Save with ⌘S, and open it
     in Preview.
   - Expected: each markup's author is your full name.
   - Then set another name in Settings…, add a note, save, and check it in Preview.
2. **Settings… (⌘,):**
   - The CoffeePDF menu has About, Settings…, Hide and Quit.
   - The author field shows your name as its placeholder.
   - No label is clipped or overlapping (§14 item 6).
3. **The default PDF app:**
   - Make another app the default, then press Make CoffeePDF the Default PDF App.
   - Expected: macOS may confirm. Then a double-clicked PDF opens in CoffeePDF.
4. **Closing with changes:** ⌘W, and also the red button, on a marked-up PDF.
   - Expected: Save changes? asks.
   - Cancel keeps the window, Don’t Save closes it, and Save saves and closes it.
5. **Quitting with changes:** ⌘Q with two marked-up windows.
   - Expected: each window asks in turn, and a Cancel stops the quit.
6. **No window:** close the last window.
   - Expected: CoffeePDF stays in the Dock with its menu bar.
   - Clicking the Dock icon opens an empty window, from which ⌘O works.
7. **Open Recent:** it lists the files opened, newest first, and Clear Menu empties it.
8. **Save As (⇧⌘S):** expected, the window moves to the new file, and the old file is unchanged.
9. **Print… (⌘P):** expected, the macOS print dialog opens with the document, including any unsaved markups.
10. **The shortcuts:** zoom (⌘+, ⌘−, ⌘0, ⌘9), pages (⌥⌘↑ and ⌥⌘↓) and the Annotate tools.
    - Each press acts once.
    - ⌘Z in the search field undoes your typing, not a markup.
11. **Forms:** fill a field, save, and open the file in Preview. Expected: the value shows.

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md README.md docs
git commit -m "Record Phase 1's build, and list the owner's checks"
```
