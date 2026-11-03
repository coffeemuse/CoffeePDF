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
    /// Returns <paramref name="pdf"/> itself for a signed PDF, told by a <c>/ByteRange</c> in its bytes, whose signature
    /// a full save would break, so an undone markup may stay in the update a save appends to it: a known limit for v1
    /// (owner, 2026-11-03). It does the same for bytes PDFium can't open or save, and tells <paramref name="warn"/>
    /// why, without the file's contents. A save never fails for want of a full save, as it never does for want of an
    /// author.
    /// </remarks>
    public static byte[] Compact(byte[] pdf, string? password, Action<string>? warn = null)
    {
        if (IsSigned(pdf))
            return pdf;
        string problem;
        lock (Pdfium.Gate)
        {
            using var document = PdfiumDocument.Open(pdf, password);
            if (document is null)
                problem = "PDFium couldn't open it";
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

    /// <summary>
    /// Whether <paramref name="pdf"/> holds a signature, told by a signature dictionary's <c>/ByteRange</c> in its bytes.
    /// Every signature dictionary has one, and it stays readable there: the dictionary can't sit in a compressed object
    /// stream, since the byte range must leave out its own <c>/Contents</c>, and names aren't encrypted. PDFium's
    /// <c>FPDF_GetSignatureCount</c> isn't used, since it reads only the form's top-level fields: it counts no signature
    /// in a signed field under a parent field, and one in an empty signature field (probe, 2026-11-03). Any other
    /// <c>/ByteRange</c> only keeps a PDF's incremental save.
    /// </summary>
    private static bool IsSigned(byte[] pdf) => pdf.AsSpan().IndexOf("/ByteRange"u8) >= 0;
}
