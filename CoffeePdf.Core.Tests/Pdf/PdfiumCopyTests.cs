using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>The core's PDFium is a copy of its own (CoffeePdf.Core.csproj), not the PdfViewer's.</summary>
public sealed class PdfiumCopyTests
{
    [Fact]
    public void The_core_has_its_own_copy_of_PDFium_beside_the_tests_and_reads_through_it()
    {
        var name = OperatingSystem.IsMacOS() ? "libcoffeepdfium.dylib" : "libcoffeepdfium.so";

        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, name)), $"{name} isn't beside the tests");
        Assert.Single(PdfAnnotations.Read(AnnotatedPdf.Build(AnnotatedPdf.Note("n-1", "Zoë Q. Reviewer")), 0));
    }
}
