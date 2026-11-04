# Finding 3's fix: a full save for unsigned PDFs Implementation Plan

Written 2026-11-03 from the spec's §16, "Finding 3's fix: a full save for unsigned PDFs", which the owner approved the
same day. Its code hasn't been built yet. Two of its facts were checked against PDFium 157 beforehand, with throwaway
tools outside the repository: a signature field built as Task 1's `Signed()` builds it counts as a signature
(`FPDF_GetSignatureCount` answered 1), and a full save of an encrypted PDF writes `/P -4` and `/R 3` in its encryption
dictionary.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every save of an unsigned PDF writes only what the document uses, so a markup that was undone, and the
drawings the PdfViewer replaced, no longer stay in the file.

**Architecture:** A new `PdfCompaction.Compact(pdf, password, warn)` in `CoffeePdf.Core/Pdf/` opens the bytes in
CoffeePDF's own PDFium and returns its full save (`FPDF_SaveAsCopy` with `FPDF_NO_INCREMENTAL`). A signed PDF, or bytes
PDFium can't open or save, come back as they were. `DocumentWindow.SaveToAsync` runs it on what `MarkupAuthor.Stamp`
returns, in the write that already runs off the UI thread, so every save, including the copy written for Healdan, gets
it, and the file is still written once through `AtomicFile`.

**Tech Stack:** .NET 10, C#, xUnit, PDFium 157 (bblanchon, bound as `"coffeepdfium"`), Avalonia 12.1.4 headless for
the app's tests, Avalonia's PdfViewer 12.0.1.

**Spec:** `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`: §6.4 ("What a save writes"), and §16, "Finding 3's
fix: a full save for unsigned PDFs". The findings behind it are in §16, Phase 3, finding 3.

## Global Constraints

- `TreatWarningsAsErrors` is on: a warning fails the build.
- `CoffeePdf.Core` references no Avalonia assembly (`ArchitectureTests`). Logic goes in the core, with its tests in
  `CoffeePdf.Core.Tests`.
- CoffeePDF's calls into PDFium use its own copy (`libcoffeepdfium`, bound as `"coffeepdfium"`), take `Pdfium.Gate`, and
  never call `FPDF_DestroyLibrary`.
- Every save goes through `DocumentWindow.SaveToAsync`. The PdfViewer's `AutoSave` and `AllowDocumentSaving` stay off.
- A signed PDF keeps its incremental save (owner, 2026-11-03). Nothing in this plan changes how a signed PDF is saved.
- A save never fails for want of the full save: bytes PDFium can't open or save are written as they were.
- Nothing logs a file's contents. A log line names the problem, never the document's text.
- Tests that need the PdfViewer use `[PdfViewerFact]` in a class with `[Trait("Category", "PdfViewer")]`. Tests wait for
  conditions, never for fixed times.
- Tests save with the made-up author `Test Reviewer`. No PDF is ever committed, and no personal detail goes into a file
  or a commit (the hooks in `.githooks` refuse them).
- The Avalonia licence key is never printed or logged. Never build with `-v:d`, `-v:diag` or `-bl`.
- Never push, tag or open a pull request without the owner's go-ahead.

---

### Before Task 1: commit the design

The spec's and the roadmap's edits from the design session, and this plan, aren't committed yet.

- [ ] **Step 1: Check the hooks are on, and commit**

```bash
git config core.hooksPath   # expect a path ending in .githooks; if empty, run: git config core.hooksPath .githooks
git add docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md docs/ROADMAP.md docs/superpowers/plans/2026-11-03-full-save-for-unsigned-pdfs.md
git commit -m "Record finding 3, and design a full save for unsigned PDFs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Expected: one commit, which the hooks accept.

---

### Task 1: `PdfCompaction` in the core

**Files:**
- Create: `CoffeePdf.Core/Pdf/PdfCompaction.cs`
- Modify: `CoffeePdf.Core/Pdf/Pdfium.cs` (the save flags, `SaveIncremental`, the imports, and `PdfiumDocument`)
- Create: `CoffeePdf.Core.Tests/Fixtures/PdfBytes.cs`
- Modify: `CoffeePdf.Core.Tests/Fixtures/EncryptedPdf.cs` (`Build` takes a user password and an unused object)
- Create: `CoffeePdf.Core.Tests/Pdf/PdfCompactionTests.cs`

**Interfaces:**
- Consumes: `PdfiumDocument.Open(byte[] pdf, string? password)`, `Pdfium.Gate`, `MarkupAuthor.Stamp`, `PdfAnnotations.Read`,
  `MinimalPdf.Objects`/`MinimalPdf.Write`, `AnnotatedPdf.Build`/`AnnotatedPdf.Note`, all as they are.
- Produces:
  - `public static byte[] PdfCompaction.Compact(byte[] pdf, string? password, Action<string>? warn = null)`, in
    namespace `CoffeePdf.Core.Pdf`. Task 2 calls it.
  - `internal static bool PdfBytes.Holds(byte[] pdf, string text)` and `internal static int PdfBytes.Saves(byte[] pdf)`,
    in namespace `CoffeePdf.Core.Tests.Fixtures`. Task 2's tests use them.
  - `EncryptedPdf.Build(string userPassword = UserPassword, string? unusedObject = null)`. Existing calls of
    `EncryptedPdf.Build()` keep working.

- [ ] **Step 1: Write the test helper `PdfBytes`**

Create `CoffeePdf.Core.Tests/Fixtures/PdfBytes.cs`:

```csharp
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Core.Tests.Fixtures;

/// <summary>
/// Reads a PDF's bytes as anyone with a PDF tool could, without a PDF library, for spec §16, finding 3's fix: a markup
/// that's gone from the page mustn't be in the file.
/// </summary>
internal static class PdfBytes
{
    /// <summary>
    /// Whether <paramref name="text"/> is anywhere in <paramref name="pdf"/>, in the file's bytes or in a stream once
    /// inflated, as ASCII or UTF-16BE, or as either one's hex in upper or lower case. It doesn't decrypt.
    /// </summary>
    public static bool Holds(byte[] pdf, string text)
    {
        var forms = Forms(text);
        return Places(pdf).Any(place => forms.Any(form => place.AsSpan().IndexOf(form) >= 0));
    }

    /// <summary>How many saves are stacked in <paramref name="pdf"/>: each ends with <c>%%EOF</c>.</summary>
    public static int Saves(byte[] pdf) => Regex.Count(Encoding.Latin1.GetString(pdf), "%%EOF");

    private static List<byte[]> Forms(string text)
    {
        var ascii = Encoding.ASCII.GetBytes(text);
        var utf16 = Encoding.BigEndianUnicode.GetBytes(text);
        var hex = new[] { ascii, utf16 }
            .SelectMany(bytes => new[] { Convert.ToHexString(bytes), Convert.ToHexString(bytes).ToLowerInvariant() })
            .Select(Encoding.ASCII.GetBytes);
        return [ascii, utf16, .. hex];
    }

    /// <summary>The file's bytes, then each stream's, inflated, for those that inflate.</summary>
    private static IEnumerable<byte[]> Places(byte[] pdf)
    {
        yield return pdf;
        var text = Encoding.Latin1.GetString(pdf);
        foreach (Match match in Regex.Matches(text, @"stream\r?\n"))
        {
            var start = match.Index + match.Length;
            var end = text.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0)
                continue;
            var inflated = Inflate(pdf, start, end - start);
            if (inflated is not null)
                yield return inflated;
        }
    }

    /// <summary>The zlib data at <paramref name="start"/> inflated, or null if it isn't zlib data.</summary>
    private static byte[]? Inflate(byte[] pdf, int start, int length)
    {
        try
        {
            using var input = new MemoryStream(pdf, start, length);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 2: Let `EncryptedPdf` build a PDF with only an owner password, and one with an unused object**

In `CoffeePdf.Core.Tests/Fixtures/EncryptedPdf.cs`, replace the start of `Build` down to the end of the `objects`
array, so the user password and an extra object are parameters:

```csharp
    /// <summary>
    /// The PDF, which opens with <paramref name="userPassword"/>: an empty one makes a PDF with only an owner password,
    /// which opens without one. <paramref name="unusedObject"/>, when given, goes in as object 7, which nothing refers
    /// to. It mustn't hold a string or a stream, since those would need encrypting.
    /// </summary>
    public static byte[] Build(string userPassword = UserPassword, string? unusedObject = null)
    {
        var owner = OwnerEntry(userPassword);
        var key = FileKey(owner, userPassword);
        var user = UserEntry(key);
        var content = Rc4(ObjectKey(key, 4), "BT /F1 24 Tf 72 700 Td (CoffeePDF fixture) Tj ET"u8.ToArray());
        byte[][] objects =
        [
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"),
            [.. Ascii($"<< /Length {content.Length} >>\nstream\n"), .. content, .. Ascii("\nendstream")],
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            // The encryption dictionary's own strings aren't encrypted.
            Ascii($"<< /Filter /Standard /V 2 /R 3 /Length 128 /P {Permissions} /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> >>"),
        ];
        if (unusedObject is not null)
            objects = [.. objects, Ascii(unusedObject)];
```

Leave the rest of `Build` (the writing of the objects, the cross-reference and the trailer) as it is: it already counts
`objects.Length`. Then make `OwnerEntry` and `FileKey` take the user password instead of reading the constant:

```csharp
    /// <summary>Algorithm 3: /O, the user password's padding encrypted with a key from the owner password.</summary>
    private static byte[] OwnerEntry(string userPassword)
    {
        var hash = MD5.HashData(Pad(OwnerPassword));
        for (var i = 0; i < 50; i++)
            hash = MD5.HashData(hash);
        return Rc4Rounds(hash, Pad(userPassword));
    }

    /// <summary>Algorithm 2: the file's key, from the user password, /O, the permissions and the file's ID.</summary>
    private static byte[] FileKey(byte[] owner, string userPassword)
    {
        var permissions = BitConverter.GetBytes(Permissions); // little-endian on every platform .NET runs these tests on
        var hash = MD5.HashData([.. Pad(userPassword), .. owner, .. permissions, .. FileId]);
        for (var i = 0; i < 50; i++)
            hash = MD5.HashData(hash);
        return hash;
    }
```

- [ ] **Step 3: Write the failing tests**

Create `CoffeePdf.Core.Tests/Pdf/PdfCompactionTests.cs`:

```csharp
using System.Text;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fixtures;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>
/// Spec §6.4 and §16, finding 3's fix: a save keeps only what an unsigned PDF uses, keeps its encryption, and leaves a
/// signed PDF as it was.
/// </summary>
public sealed class PdfCompactionTests
{
    private const string Author = "Test Reviewer";

    /// <summary>An object nothing refers to, as the PdfViewer leaves for a markup that was undone. Its name is the marker.</summary>
    private const string Leftover = "<< /Type /CoffeePdfLeftover >>";

    private const string Marker = "CoffeePdfLeftover";

    [Fact]
    public void An_object_nothing_uses_is_gone_and_the_page_is_the_same()
    {
        var pdf = WithLeftover();

        var compacted = PdfCompaction.Compact(pdf, null);

        Assert.True(PdfBytes.Holds(pdf, Marker));
        Assert.False(PdfBytes.Holds(compacted, Marker));
        Assert.True(PdfBytes.Holds(compacted, "CoffeePDF fixture")); // the page's text
        Assert.Equal(PdfAnnotations.Read(pdf, 0), PdfAnnotations.Read(compacted, 0));
    }

    [Fact]
    public void Saves_stacked_in_a_file_come_back_as_one_keeping_what_MarkupAuthor_reads()
    {
        // MarkupAuthor tells new markups by /NM, /T and the PdfViewer's /SNC, so the full save must keep all three.
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null), AnnotatedPdf.Note(null, null, left: 200, byPdfViewer: true));
        var stamped = MarkupAuthor.Stamp(pdf, previousFile: null, [0], Author);

        var compacted = PdfCompaction.Compact(stamped, null);

        Assert.Equal(2, PdfBytes.Saves(stamped));
        Assert.Equal(1, PdfBytes.Saves(compacted));
        Assert.Equal(PdfAnnotations.Read(stamped, 0), PdfAnnotations.Read(compacted, 0));
        Assert.All(PdfAnnotations.Read(compacted, 0), a => Assert.Equal(Author, a.Author));
        Assert.True(PdfBytes.Holds(compacted, "/SNC"));
    }

    [Fact]
    public void A_signed_pdf_comes_back_as_it_was()
    {
        // A full save would break the signature, so a signed PDF keeps its incremental save (owner, 2026-11-03).
        var pdf = Signed();

        Assert.Same(pdf, PdfCompaction.Compact(pdf, null));
    }

    [Fact]
    public void An_encrypted_pdf_keeps_its_encryption_and_loses_what_it_doesnt_use()
    {
        var pdf = EncryptedPdf.Build(unusedObject: Leftover);

        var compacted = PdfCompaction.Compact(pdf, EncryptedPdf.UserPassword);

        Assert.True(PdfBytes.Holds(pdf, Marker));
        Assert.False(PdfBytes.Holds(compacted, Marker));
        Assert.Throws<InvalidDataException>(() => PdfAnnotations.Read(compacted, 0));
        Assert.Empty(PdfAnnotations.Read(compacted, 0, EncryptedPdf.UserPassword));
        var text = Encoding.Latin1.GetString(compacted);
        Assert.Matches(@"/R\s*3\b", text);
        Assert.Matches(@"/P\s*-4\b", text);
    }

    [Fact]
    public void A_pdf_with_only_an_owner_password_keeps_its_encryption()
    {
        var pdf = EncryptedPdf.Build(userPassword: "", unusedObject: Leftover);

        var compacted = PdfCompaction.Compact(pdf, null);

        Assert.False(PdfBytes.Holds(compacted, Marker));
        Assert.Empty(PdfAnnotations.Read(compacted, 0)); // it still opens without a password
        var text = Encoding.Latin1.GetString(compacted);
        Assert.Matches(@"/Encrypt\s*\d+\s+\d+\s+R", text);
        Assert.Matches(@"/P\s*-4\b", text);
    }

    [Fact]
    public void An_encrypted_pdf_without_its_password_comes_back_as_it_was_and_says_why()
    {
        var pdf = EncryptedPdf.Build();
        var warnings = new List<string>();

        Assert.Same(pdf, PdfCompaction.Compact(pdf, null, warnings.Add));
        Assert.Equal("A save had no full save: PDFium couldn't open it", Assert.Single(warnings));
    }

    [Fact]
    public void Bytes_that_arent_a_pdf_come_back_as_they_were()
    {
        var bytes = "not a PDF"u8.ToArray();
        var warnings = new List<string>();

        Assert.Same(bytes, PdfCompaction.Compact(bytes, null, warnings.Add));
        Assert.Single(warnings);
    }

    /// <summary>The minimal PDF with another app's note on its page, and the leftover as object 7.</summary>
    private static byte[] WithLeftover()
    {
        var objects = MinimalPdf.Objects(withForeignNote: true, extraAnnotations: [], acroForm: null);
        objects.Add(Leftover);
        return MinimalPdf.Write(objects);
    }

    /// <summary>
    /// The minimal PDF with a signature field whose value is a signature dictionary (objects 7 and 8), which PDFium
    /// counts as a signature, and the leftover (9). The signature's bytes aren't real: the app's tests check a real one.
    /// </summary>
    private static byte[] Signed()
    {
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: ["7 0 R"], acroForm: "<< /Fields [7 0 R] /SigFlags 3 >>");
        objects.Add("<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P 3 0 R /V 8 0 R >>");
        objects.Add("<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> >>");
        objects.Add(Leftover);
        return MinimalPdf.Write(objects);
    }
}
```

- [ ] **Step 4: Run the tests to see them fail**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~PdfCompactionTests"`

Expected: the build fails with `CS0103: The name 'PdfCompaction' does not exist in the current context`.

- [ ] **Step 5: Add the full save and the signature count to the PDFium binding**

In `CoffeePdf.Core/Pdf/Pdfium.cs`, beside the constant `Incremental`, add:

```csharp
    /// <summary><c>FPDF_NO_INCREMENTAL</c>: a full save, writing only what the document uses (spec §6.4).</summary>
    private const uint NoIncremental = 2;
```

Replace `SaveIncremental(nint document)` with these three, keeping its body as `Save`'s, with `flags` in place of
`Incremental`:

```csharp
    /// <summary>Writes <paramref name="document"/> as an incremental update.</summary>
    public static byte[] SaveIncremental(nint document) => Save(document, Incremental);

    /// <summary>Writes <paramref name="document"/> whole: one revision, holding only what it uses, with its encryption kept.</summary>
    public static byte[] SaveFull(nint document) => Save(document, NoIncremental);

    private static byte[] Save(nint document, uint flags)
    {
        using var output = new MemoryStream();
        var target = GCHandle.Alloc(output);
        try
        {
            var writer = new FileWrite { Version = 1, WriteBlock = &WriteBlock, Target = GCHandle.ToIntPtr(target) };
            if (FPDF_SaveAsCopy(document, &writer, new CULong(flags)) == 0)
                throw new IOException($"PDFium couldn't write the document (error {FPDF_GetLastError().Value})");
            return output.ToArray();
        }
        finally
        {
            target.Free();
        }
    }
```

Add the import beside the others:

```csharp
    [LibraryImport(Library)] public static partial int FPDF_GetSignatureCount(nint document);
```

In `PdfiumDocument`, beside `SaveIncremental()`, add:

```csharp
    /// <summary>How many signatures the document holds: signature fields with a value (<c>FPDF_GetSignatureCount</c>).</summary>
    public int SignatureCount => Pdfium.FPDF_GetSignatureCount(Handle);

    public byte[] SaveFull() => Pdfium.SaveFull(Handle);
```

- [ ] **Step 6: Write `PdfCompaction`**

Create `CoffeePdf.Core/Pdf/PdfCompaction.cs`:

```csharp
namespace CoffeePdf.Core.Pdf;

/// <summary>
/// Gives an unsigned PDF a full save in CoffeePDF's own PDFium, so the file holds only what the document uses (spec
/// §6.4; §16, finding 3's fix). The PdfViewer's save writes every object it has made while the document is open, used
/// or not, so without this a markup that was undone, and the drawings the PdfViewer replaced, stay in the file, unseen
/// but readable. Every save runs it on the bytes <see cref="MarkupAuthor"/> returns.
/// </summary>
public static class PdfCompaction
{
    /// <summary>
    /// Returns PDFium's full save of <paramref name="pdf"/>: one revision, holding only what the document uses, with its
    /// encryption and permissions kept. An encrypted PDF needs its <paramref name="password"/>, unless it has only an
    /// owner password.
    /// </summary>
    /// <remarks>
    /// Returns <paramref name="pdf"/> itself for a signed PDF, whose signature a full save would break, so an undone
    /// markup may stay in the update a save appends to it: a known limit for v1 (owner, 2026-11-03). It does the same
    /// for bytes PDFium can't open or save, and tells <paramref name="warn"/> why, without the file's contents. A save
    /// never fails for want of a full save, as it never does for want of an author.
    /// </remarks>
    public static byte[] Compact(byte[] pdf, string? password, Action<string>? warn = null)
    {
        string problem;
        lock (Pdfium.Gate)
        {
            using var document = PdfiumDocument.Open(pdf, password);
            if (document is null)
                problem = "PDFium couldn't open it";
            else if (document.SignatureCount > 0)
                return pdf;
            else
            {
                try
                {
                    return document.SaveFull();
                }
                catch (IOException e) // Pdfium.Save's own message, which names PDFium's error and nothing of the file
                {
                    problem = e.Message;
                }
            }
        }
        warn?.Invoke($"A save had no full save: {problem}");
        return pdf;
    }
}
```

- [ ] **Step 7: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Core.Tests --filter "FullyQualifiedName~PdfCompactionTests"`

Expected: 7 passed.

If `A_pdf_with_only_an_owner_password_keeps_its_encryption` fails because the full save drops the encryption, stop:
don't loosen the test. That's a decision for the owner (whether such a PDF keeps an incremental save instead), and the
spec's §16 records the answer.

- [ ] **Step 8: Run the core's whole suite**

Run: `dotnet test CoffeePdf.Core.Tests`

Expected: no failures, and no warnings in the build. `MarkupAuthorTests` and `MinimalPdfTests` still pass, since
`EncryptedPdf.Build()` with no arguments builds the same file as before.

- [ ] **Step 9: Commit**

```bash
git add CoffeePdf.Core/Pdf/PdfCompaction.cs CoffeePdf.Core/Pdf/Pdfium.cs CoffeePdf.Core.Tests/Fixtures/PdfBytes.cs CoffeePdf.Core.Tests/Fixtures/EncryptedPdf.cs CoffeePdf.Core.Tests/Pdf/PdfCompactionTests.cs
git commit -m "Give an unsigned PDF a full save in the core, keeping only what it uses" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: every save gets the full save

**Files:**
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml.cs` (the class's remarks, `SaveToAsync`'s summary, and its write)
- Modify: `CoffeePdf.Tests/CoffeePdf.Tests.csproj` (link `PdfBytes.cs`)
- Modify: `CoffeePdf.Tests/Windows/DocumentWindowSaveTests.cs` (three tests)
- Modify: `CLAUDE.md` (the rule for saves)

**Interfaces:**
- Consumes: `PdfCompaction.Compact(byte[] pdf, string? password, Action<string>? warn = null)` and `PdfBytes.Holds`
  from Task 1; `MarkupAuthor.Stamp`, `HealdanLink.Log` (a `CoffeeLog`, whose `Warn(string)` is thread-safe),
  `TestWindows.OpenAsync`, `PdfViewer.AddStickyNoteAsync`, `AddStampAsync` and `UndoAsync`, as they are.
- Produces: nothing new for later tasks.

The tests need the licence key: run them with `AVALONIA_LICENSE_KEY` set, and never print it.

- [ ] **Step 1: Link `PdfBytes` into the app's tests**

In `CoffeePdf.Tests/CoffeePdf.Tests.csproj`, beside the other linked fixtures, add:

```xml
    <Compile Include="../CoffeePdf.Core.Tests/Fixtures/PdfBytes.cs" Link="Fixtures/PdfBytes.cs" />
```

- [ ] **Step 2: Write the failing tests**

In `CoffeePdf.Tests/Windows/DocumentWindowSaveTests.cs`, after `A_save_writes_the_markup_author_into_an_empty_note`,
add:

```csharp
    [PdfViewerFact]
    public async Task A_note_undone_before_a_save_never_reaches_the_file()
    {
        // Spec §16, finding 3's fix: the PdfViewer's save writes the undone note too, unused, with its words in plain text.
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (saved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Withdrawn remark");
            await window.Viewer.UndoAsync();
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "Kept");
            return (await window.SaveAsync(), errorList);
        });

        Assert.True(saved);
        Assert.Empty(errors);
        var bytes = File.ReadAllBytes(path);
        Assert.False(PdfBytes.Holds(bytes, "Withdrawn remark"));
        Assert.Equal(Author, Assert.Single(PdfAnnotations.Read(bytes, 0)).Author);
    }

    [PdfViewerFact]
    public async Task A_note_undone_after_a_save_is_gone_at_the_next_save()
    {
        // A save doesn't reload the PdfViewer's document, so the note it undid stays in its copy of the document.
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (saved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "Kept");
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Withdrawn remark");
            var first = await window.SaveAsync();
            await window.Viewer.UndoAsync();
            return (first && await window.SaveAsync(), errorList);
        });

        Assert.True(saved);
        Assert.Empty(errors);
        var bytes = File.ReadAllBytes(path);
        Assert.False(PdfBytes.Holds(bytes, "Withdrawn remark"));
        Assert.Equal(Author, Assert.Single(PdfAnnotations.Read(bytes, 0)).Author);
    }

    [PdfViewerFact]
    public async Task A_stamp_undone_never_reaches_the_file()
    {
        // The stamp's label is in its drawing too, which PdfBytes reads once inflated.
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (saved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStampAsync(0, "Withdrawn stamp", 300, 400, null, null, null);
            await window.Viewer.UndoAsync();
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "Kept");
            return (await window.SaveAsync(), errorList);
        });

        Assert.True(saved);
        Assert.Empty(errors);
        var bytes = File.ReadAllBytes(path);
        Assert.False(PdfBytes.Holds(bytes, "Withdrawn stamp"));
        Assert.Single(PdfAnnotations.Read(bytes, 0));
    }
```

The file already has `using CoffeePdf.Core.Pdf;` and `using CoffeePdf.Core.Tests.Fixtures;`.

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~never_reaches_the_file|FullyQualifiedName~is_gone_at_the_next_save"`

Expected: 3 failed, each at `Assert.False(PdfBytes.Holds(…))`: today's save keeps the undone markup. If any of them is
skipped instead, the licence key isn't set.

- [ ] **Step 4: Run the full save in `SaveToAsync`**

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`, `SaveToAsync`, replace:

```csharp
            var write = WriteFile;
            await Task.Run(() => write(target, MarkupAuthor.Stamp(pdf, previous, pages, author, password)));
```

with:

```csharp
            var write = WriteFile;
            var log = Healdan.Log;
            // An unsigned PDF keeps only what it uses (spec §6.4): the PdfViewer's save writes what was undone too.
            await Task.Run(() => write(target,
                PdfCompaction.Compact(MarkupAuthor.Stamp(pdf, previous, pages, author, password), password, log.Warn)));
```

Then bring the two comments in step with it. In the class's remarks, replace:

```csharp
/// Every save goes through <see cref="SaveToAsync"/>. The PdfViewer saves to memory, <see cref="MarkupAuthor"/> writes
/// the author into the markups made here, and <see cref="AtomicFile"/> renames the result over the file. The
```

with:

```csharp
/// Every save goes through <see cref="SaveToAsync"/>. The PdfViewer saves to memory, <see cref="MarkupAuthor"/> writes
/// the author into the markups made here, <see cref="PdfCompaction"/> gives an unsigned PDF a full save, so the file
/// holds only what the document uses, and <see cref="AtomicFile"/> renames the result over the file. The
```

And replace `SaveToAsync`'s summary:

```csharp
    /// <summary>
    /// Writes the document to <paramref name="target"/> (spec §6.4), and the window then shows that file. The
    /// author goes into the new markups, and the file is replaced whole. Returns whether it saved. With
    /// <paramref name="asCopy"/>, it writes a copy for Healdan to file instead (§6.6): the window stays on its own
    /// file, whose changes stay unsaved.
    /// </summary>
```

with:

```csharp
    /// <summary>
    /// Writes the document to <paramref name="target"/> (spec §6.4), and the window then shows that file. The
    /// author goes into the new markups, an unsigned PDF keeps only what it uses, and the file is replaced whole.
    /// Returns whether it saved. With <paramref name="asCopy"/>, it writes a copy for Healdan to file instead (§6.6):
    /// the window stays on its own file, whose changes stay unsaved.
    /// </summary>
```

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test CoffeePdf.Tests --filter "FullyQualifiedName~never_reaches_the_file|FullyQualifiedName~is_gone_at_the_next_save"`

Expected: 3 passed.

- [ ] **Step 6: Update the rule in `CLAUDE.md`**

Replace:

```markdown
- **Every save goes through `DocumentWindow.SaveToAsync`:** the PdfViewer saves to memory, `MarkupAuthor` writes the
  author, and `AtomicFile` replaces the file. The PdfViewer's `AllowDocumentSaving` stays off (spec §16 Decision 1).
```

with:

```markdown
- **Every save goes through `DocumentWindow.SaveToAsync`:** the PdfViewer saves to memory, `MarkupAuthor` writes the
  author, `PdfCompaction` gives an unsigned PDF a full save (spec §6.4), and `AtomicFile` replaces the file. The
  PdfViewer's `AllowDocumentSaving` stays off (spec §16 Decision 1).
```

- [ ] **Step 7: Run everything, with the key and without it**

Run: `dotnet test CoffeePdf.sln`

Expected: no failures, and no warnings in the build. The only skip is the probe `What_the_viewer_does_without_a_licence`.
`A_signed_pdf_stays_signed_through_two_saves_and_its_notes_get_the_author` and
`A_pdf_unlocked_in_the_PdfViewers_prompt_saves_its_notes_with_the_author` still pass: a signed PDF keeps its incremental
save, and an encrypted one its encryption.

Run: `env -u AVALONIA_LICENSE_KEY dotnet test CoffeePdf.sln`

Expected: no failures. The PdfViewer's tests skip, the new three among them.

Note both runs' counts, for Task 3's record.

- [ ] **Step 8: Commit**

```bash
git add CoffeePdf/Windows/DocumentWindow.axaml.cs CoffeePdf.Tests/CoffeePdf.Tests.csproj CoffeePdf.Tests/Windows/DocumentWindowSaveTests.cs CLAUDE.md
git commit -m "Keep only what an unsigned PDF uses at every save, so an undone markup stays out of the file" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: measure it, record the build, and package it for the owner's checks

**Files:**
- Create: `CoffeePdf.Core.Tests/Pdf/PdfCompactionProbe.cs`
- Modify: `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` (§16, "Finding 3's fix")
- Modify: `docs/ROADMAP.md` (the status, and Phase 4's row)

**Interfaces:**
- Consumes: `PdfCompaction.Compact` from Task 1, `MinimalPdf.Write`.
- Produces: the record of the build, and a package for the owner.

- [ ] **Step 1: Write the probe**

Create `CoffeePdf.Core.Tests/Pdf/PdfCompactionProbe.cs`:

```csharp
using System.Diagnostics;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;
using Xunit.Abstractions;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>
/// Spec §16, finding 3's fix: how long the full save adds to a save of a large PDF, a question with no right answer to
/// assert. Run with: dotnet test CoffeePdf.Core.Tests --filter Category=Probe --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "Probe")]
public sealed class PdfCompactionProbe(ITestOutputHelper output)
{
    private const int Pages = 500;

    [Fact]
    public void How_long_a_full_save_of_a_large_pdf_takes()
    {
        var pdf = Large(imageBytes: 30_000_000);

        var clock = Stopwatch.StartNew();
        var compacted = PdfCompaction.Compact(pdf, null);
        clock.Stop();

        Assert.NotSame(pdf, compacted);
        output.WriteLine($"{pdf.Length:N0} bytes, {Pages} pages, the first with an uncompressed image: the full save took " +
            $"{clock.ElapsedMilliseconds:N0} ms and wrote {compacted.Length:N0} bytes");
    }

    /// <summary>
    /// <see cref="Pages"/> pages of text, the first also showing an uncompressed RGB image of about
    /// <paramref name="imageBytes"/> bytes. Objects: 1 the catalog, 2 the page tree, 3 the font, 4 the image, then each
    /// page and its text, 5 + 2i and 6 + 2i.
    /// </summary>
    private static byte[] Large(int imageBytes)
    {
        var side = (int)Math.Sqrt(imageBytes / 3.0);
        var kids = string.Join(' ', Enumerable.Range(0, Pages).Select(i => $"{5 + 2 * i} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {Pages} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Type /XObject /Subtype /Image /Width {side} /Height {side} /ColorSpace /DeviceRGB /BitsPerComponent 8 " +
            $"/Length {side * side * 3} >>\nstream\n{new string('A', side * side * 3)}\nendstream",
        };
        for (var i = 0; i < Pages; i++)
        {
            var image = i == 0 ? " /XObject << /Im1 4 0 R >>" : "";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >>{image} >> /Contents {6 + 2 * i} 0 R >>");
            var text = (i == 0 ? "q 612 0 0 792 0 0 cm /Im1 Do Q " : "") + $"BT /F1 24 Tf 72 700 Td (Page {i + 1}) Tj ET";
            objects.Add($"<< /Length {text.Length} >>\nstream\n{text}\nendstream");
        }
        return MinimalPdf.Write(objects);
    }
}
```

- [ ] **Step 2: Run the probe**

Run: `dotnet test CoffeePdf.Core.Tests --filter Category=Probe --logger "console;verbosity=detailed"`

Expected: 1 passed, printing the size, the time and the bytes written. Run it twice more and note all three times. CI
leaves the probe out, as it does every probe (`--filter "Category!=Probe"` in `.github/workflows/ci.yml`).

- [ ] **Step 3: Record the build in the spec**

In `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`, at the end of §16, "Finding 3's fix: a full save for
unsigned PDFs", add a bullet in the section's style, with the counts noted in Task 2's Step 7, the times from Step 2,
and the day it was built in place of the angle brackets. Write what was observed, not what was expected:

```markdown
- **Built (<date>).** `PdfCompaction.Compact` runs in every save (`DocumentWindow.SaveToAsync`).
  - The tests: with the key, the core's <n> passed, and the app's <n> passed and 1 skipped (the keyless probe).
    Without it, the core's <n> passed, and the app's <n> passed and <n> skipped.
  - A PDF with only an owner password keeps its encryption through the full save (`PdfCompactionTests`).
  - The probe `How_long_a_full_save_of_a_large_pdf_takes`: <size> bytes and 500 pages took <t1>, <t2> and <t3> ms.
  - Left: the owner's checks on the Mac.
```

Also in §16, replace "and finding 3's fix built and checked (below)" in "Left before v1.0" with "and the owner's checks
of finding 3's fix (below)".

- [ ] **Step 4: Update the roadmap**

In `docs/ROADMAP.md`, Phase 4's row, replace "finding 3's fix (a full save for unsigned PDFs, designed 2026-11-03)
built and checked" with "the owner's checks of finding 3's fix (a full save for unsigned PDFs, built <date>)". In the
status above the table, replace "Its fix, a full save for unsigned PDFs, is designed (2026-11-03, the spec's §16)." with
"Its fix, a full save for unsigned PDFs, is built (<date>, the spec's §16), and waits for the owner's checks."

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf.Core.Tests/Pdf/PdfCompactionProbe.cs docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md docs/ROADMAP.md
git commit -m "Measure the full save on a large PDF, and record the build" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Package it for the owner's checks**

Run, on the Mac, with the key set and no signing identity:

```bash
packaging/mac/package.sh 0.3.1-fullsave.1
```

Expected: `packaging/mac/out/CoffeePDF-0.3.1-fullsave.1.dmg`, ad-hoc signed without the hardened runtime (Decision 4),
so it runs on the Mac that built it. The package isn't committed (git ignores `packaging/mac/out/`).

- [ ] **Step 7: Hand over to the owner**

Push nothing, and open no pull request. Give the owner:

1. **The checks on the Mac,** with the package from Step 6, CoffeePDF quit first:
   - A local PDF that isn't signed: add a note with a word of your choosing, undo it, add another note, and save. Then
     check the word isn't in the file: `strings -a <file.pdf> | grep -c '<the word>'` should print 0. Open the file in
     Preview: the page, the kept note and any form values look as before.
   - A Healdan document: open it from Healdan, mark it up, undo one markup and press ⌘S. The title moves on to the next
     version. Close the window, so it's checked in. Download that version from Healdan, and check the undone markup's
     word isn't in it.
   - A password-protected PDF: mark it up and save. Preview still asks for its password.
   - A signed PDF: mark it up and save. It still opens in Preview, with its signature.
2. **The note for Avalonia,** for the owner to send through Avalonia's Pro support if they choose:

   > The PdfViewer 12.0.1 writes every PDF object it has created while a document is open into `SaveDocumentAsync`'s
   > output, whether the document still uses it or not. A sticky note or stamp that's undone, and the appearance
   > streams it replaces when an annotation is redrawn, stay in the saved file as unreferenced objects, with their
   > text readable by any PDF tool. Undoing a note before saving is enough to show it. We now give unsigned PDFs a full
   > save of our own afterwards, but a signed PDF's incremental update still carries them. Could the save write only
   > the objects the document references?
