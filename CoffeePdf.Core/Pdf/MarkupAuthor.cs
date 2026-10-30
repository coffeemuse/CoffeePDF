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
