namespace CoffeePdf.Tests.Fixtures;

/// <summary>A PDF of several Letter pages, each with the minimal fixture's text, for the page box's tests.</summary>
internal static class PagesPdf
{
    public static byte[] Build(int pages)
    {
        // Objects 1 to 5 are the minimal fixture's: the catalog, the page tree, its page, the text and the font; 6, its
        // note, is on no page. The other pages (7 on) are copies of its page, sharing the text and the font.
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: [], acroForm: null);
        var page = objects[2];
        var kids = new List<string> { "3 0 R" };
        for (var i = 1; i < pages; i++)
        {
            objects.Add(page);
            kids.Add($"{objects.Count} 0 R");
        }
        objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', kids)}] /Count {pages} >>";
        return MinimalPdf.Write(objects);
    }
}
