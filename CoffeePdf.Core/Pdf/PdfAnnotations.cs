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
