namespace CoffeePdf.Core.Documents;

/// <summary>A document window's title (spec §6.1's local mode). Phase 2 adds Healdan's titles.</summary>
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
}
