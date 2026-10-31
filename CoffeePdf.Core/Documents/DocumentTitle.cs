namespace CoffeePdf.Core.Documents;

/// <summary>A document window's title (spec §6.1): a local file's, and a Healdan copy's.</summary>
public static class DocumentTitle
{
    /// <summary>The file's name, with Preview's " — Edited" while it has unsaved changes. An empty window is CoffeePDF.</summary>
    public static string For(string? path, bool edited)
    {
        if (path is null)
            return "CoffeePDF";
        var name = Path.GetFileName(path);
        return edited ? $"{name} — Edited" : name;
    }

    /// <summary>
    /// A Healdan copy's title (spec §6.1): the Helper's, as it gives it, such as <c>Settlement Agreement #1234 v2.1</c>,
    /// with " — Edited" while it has unsaved changes, and " — Saving to Healdan…" while a save is on its way there (§6.4).
    /// </summary>
    public static string ForCopy(string title, bool edited, bool saving) =>
        $"{title}{(edited ? " — Edited" : "")}{(saving ? " — Saving to Healdan…" : "")}";
}
