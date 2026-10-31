namespace CoffeePdf.Core.Documents;

/// <summary>
/// What a window allows in its mode (spec §6.1, §6.3): marking up and saving, the Healdan menu and its items, and
/// whether ⌘O and ⇧⌘S go to the Helper's windows first.
/// </summary>
public sealed record WindowCommands(bool Markup, bool Save, bool HealdanMenu, bool SaveToHealdan, bool SaveAsNewVersion,
    bool SaveAsNewDocument, bool Properties, bool HelperBoxes)
{
    /// <summary>An empty window's: nothing to mark up or save. While support is on, the Healdan menu shows and ⌘O goes to the Helper.</summary>
    public static WindowCommands Empty(bool healdan) => new(false, false, healdan, false, false, false, false, healdan);
}

/// <summary>What a bar's button does.</summary>
public enum BarAction
{
    None,
    SaveAsNewDocument,
    TurnOnHealdan,
}

/// <summary>The bar across the top of a Healdan copy's window (spec §6.1, §6.9), and its one button.</summary>
public sealed record WindowBar(string Text, BarAction Action)
{
    public const string ReadOnlyOpen = "Read-only — opened without a check-out";

    public const string Refused =
        "Healdan didn’t take this copy’s last save: it’s no longer checked out to you. Use Save as New Document to keep your changes.";

    public const string HelperAway = "Healdan Helper isn’t running. Saves stay on this Mac, and go to Healdan once it’s back.";

    public const string HelperAwayReadOnly = "Read-only — Healdan Helper isn’t running.";

    public const string SupportOff = "This is a copy from Healdan, and Healdan support is off, so it’s read-only.";

    /// <summary>A read-only copy of a document someone has checked out. The Helper's <c>holdBy</c> names the person, not the computer.</summary>
    public static string HeldBy(string holder) => $"Read-only — checked out by {holder}";
}
