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
    /// A new annotation is a markup annotation with no author that isn't one of the authorless annotations in
    /// <paramref name="previousFile"/>, the file the save replaces. One with a name (<c>/NM</c>) is matched by its name.
    /// The PdfViewer names what it makes except an empty sticky note (the owner's check 12b), so one without a name must
    /// carry the <c>/SNC</c> the PdfViewer marks its notes with, and is matched by its subtype, rectangle and text, each
    /// in the file accounting for one just like it. The rectangle holds still because the PdfViewer gave the note its
    /// appearance when it made it; another app's note without one is resized by the PdfViewer's save, which is why the
    /// mark is needed. So another app's authorless annotations stay authorless, and those made in this window get the
    /// author again at every save, since the PdfViewer's own copy never has it.
    /// </para>
    /// <para>
    /// Returns <paramref name="pdf"/> itself when there's nothing to write: no author, no pages, nothing new, a PDF
    /// that PDFium can't open, such as an encrypted one without its <paramref name="password"/>, or a
    /// <paramref name="previousFile"/> that exists but PDFium can't open, since then nothing can be told apart. A save
    /// never fails for want of an author.
    /// </para>
    /// </remarks>
    public static byte[] Stamp(byte[] pdf, string? previousFile, IReadOnlyCollection<int> pages, string author, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(author) || pages.Count == 0)
            return pdf;
        lock (Pdfium.Gate)
        {
            if (Authorless(previousFile, pages, password) is not { } authorless)
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

    /// <summary>Whether <paramref name="annotation"/> is new. An unnamed one that isn't takes its match out of <paramref name="before"/>.</summary>
    private static bool IsNew(nint annotation, AuthorlessOnPage? before)
    {
        if (!((AnnotationSubtype)Pdfium.FPDFAnnot_GetSubtype(annotation)).IsMarkup() || Pdfium.HasKey(annotation, "T\0"u8))
            return false;
        if (Pdfium.GetString(annotation, "NM\0"u8) is { } name)
            return before?.Names.Contains(name) != true;
        return IsPdfViewersNote(annotation) && before?.Unnamed.Remove(Shape.Of(annotation)) != true;
    }

    /// <summary>Whether <paramref name="annotation"/> carries the <c>/SNC</c> the PdfViewer 12.0.1 marks its sticky notes with.</summary>
    private static bool IsPdfViewersNote(nint annotation) => Pdfium.HasKey(annotation, "SNC\0"u8);

    /// <summary>
    /// The authorless annotations on each of <paramref name="pages"/> in <paramref name="file"/>. Empty when there's no
    /// file yet. Null when it can't be read, since then nothing can be told apart and nothing is stamped.
    /// </summary>
    private static Dictionary<int, AuthorlessOnPage>? Authorless(string? file, IReadOnlyCollection<int> pages, string? password)
    {
        var authorless = new Dictionary<int, AuthorlessOnPage>();
        if (file is null || !File.Exists(file))
            return authorless;
        using var document = PdfiumDocument.OpenFile(file, password);
        if (document is null)
            return null;
        foreach (var page in pages.Where(p => p >= 0 && p < document.PageCount))
        {
            var onPage = authorless[page] = new AuthorlessOnPage();
            document.ForEachAnnotation(page, annotation =>
            {
                if (Pdfium.HasKey(annotation, "T\0"u8))
                    return;
                if (Pdfium.GetString(annotation, "NM\0"u8) is { } name)
                    onPage.Names.Add(name);
                else if (IsPdfViewersNote(annotation))
                    onPage.Unnamed.Add(Shape.Of(annotation));
            });
        }
        return authorless;
    }

    /// <summary>A page's authorless annotations: the names of those with one, and the shapes of the PdfViewer's without.</summary>
    private sealed class AuthorlessOnPage
    {
        public HashSet<string> Names { get; } = [];
        public List<Shape> Unnamed { get; } = [];
    }

    /// <summary>
    /// What tells apart annotations without a name: the subtype, the rectangle to a hundredth of a point, and the text.
    /// </summary>
    private readonly record struct Shape(AnnotationSubtype Subtype, float Left, float Bottom, float Right, float Top, string Contents)
    {
        public static Shape Of(nint annotation)
        {
            var (left, bottom, right, top) = Pdfium.GetRect(annotation) ?? default;
            return new Shape((AnnotationSubtype)Pdfium.FPDFAnnot_GetSubtype(annotation), MathF.Round(left, 2),
                MathF.Round(bottom, 2), MathF.Round(right, 2), MathF.Round(top, 2), Pdfium.GetString(annotation, "Contents\0"u8) ?? "");
        }
    }
}
