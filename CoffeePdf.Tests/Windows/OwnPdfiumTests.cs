using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Tests.Windows;

/// <summary>CoffeePDF's PDFium is its own copy (CoffeePdf.Core's csproj), so the PdfViewer's shutdown can't reach it.</summary>
[Trait("Category", "PdfViewer")]
public sealed class OwnPdfiumTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>
    /// The PdfViewer calls FPDF_DestroyLibrary when its last document closes. When the core shared that copy, and had
    /// initialised it before (Pdfium.Initialise remembers that, as after any save in the app), its next call (here, the
    /// first page's load) crashed the test host with a segmentation fault.
    /// </summary>
    [PdfViewerFact]
    public async Task The_core_still_reads_annotations_after_the_only_window_has_closed()
    {
        var path = Path.Combine(_dir, "fixture.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        var note = MinimalPdf.Build(withForeignNote: true);

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            Assert.Single(PdfAnnotations.Read(note, 0)); // the core has now initialised its PDFium, as a save does
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.Close();
            await TestWindows.WaitForAsync(() => closed);
            return 0;
        });

        var annotation = Assert.Single(PdfAnnotations.Read(note, 0));
        Assert.Equal(MinimalPdf.ForeignNoteAuthor, annotation.Author);
    }
}
