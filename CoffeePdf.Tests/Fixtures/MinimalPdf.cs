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
