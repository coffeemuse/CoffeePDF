namespace CoffeePdf.Core.Documents;

/// <summary>What opening a PDF does to the open windows.</summary>
public enum WindowAction
{
    /// <summary>Bring forward the window that already shows the file.</summary>
    Activate,

    /// <summary>Load the file into an empty window, such as the one shown at launch.</summary>
    LoadInto,

    /// <summary>Open a new window for the file.</summary>
    OpenNew,
}

/// <summary>Where a PDF being opened goes (spec §2.1: one window per document).</summary>
/// <param name="Action">What to do.</param>
/// <param name="Index">The window's index in the list given to <see cref="For"/>, or -1 for <see cref="WindowAction.OpenNew"/>.</param>
public readonly record struct WindowChoice(WindowAction Action, int Index)
{
    /// <summary>
    /// Chooses for <paramref name="path"/>, given each open window's file in order (null for an empty window). Paths
    /// are compared in full, so <c>/docs/sub/../a.pdf</c> and <c>/docs/a.pdf</c> are the same file.
    /// </summary>
    public static WindowChoice For(IReadOnlyList<string?> windowPaths, string path)
    {
        var full = Path.GetFullPath(path);
        for (var i = 0; i < windowPaths.Count; i++)
            if (windowPaths[i] is { } open && Path.GetFullPath(open) == full)
                return new WindowChoice(WindowAction.Activate, i);
        for (var i = 0; i < windowPaths.Count; i++)
            if (windowPaths[i] is null)
                return new WindowChoice(WindowAction.LoadInto, i);
        return new WindowChoice(WindowAction.OpenNew, -1);
    }
}
