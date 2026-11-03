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
