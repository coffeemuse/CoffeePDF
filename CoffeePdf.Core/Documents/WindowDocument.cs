using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Documents;

/// <summary>A document window's mode (spec §6.1).</summary>
public enum DocumentMode
{
    /// <summary>A file outside the Helper's <c>Copies/</c> folder, or one the Helper doesn't know.</summary>
    Local,

    /// <summary>A copy whose <c>/v1/state</c> hasn't answered yet (§6.2): read-only meanwhile.</summary>
    Asking,

    /// <summary>A copy checked out to this computer.</summary>
    Editable,

    /// <summary>A read-only open, a document held elsewhere, or a copy whose save Healdan refused (§6.9).</summary>
    ReadOnly,

    /// <summary>A copy while the Helper's channel doesn't answer (§6.9).</summary>
    HelperAway,

    /// <summary>A copy, with Healdan support off: read-only, with a bar offering to turn support on.</summary>
    SupportOff,
}

/// <summary>
/// A window's file as CoffeePDF sees it (spec §6.1): where it is, its mode, and what the Helper last said of it. The
/// window keeps one and replaces it as the Helper answers. Everything the mode decides is here, so it's tested
/// without a window.
/// </summary>
/// <param name="Path">The file's full path.</param>
/// <param name="Mode">Its mode.</param>
/// <param name="Copy">The Helper's last answer about it, for a copy.</param>
/// <param name="Refused">A copy whose save Healdan refused, read-only since (§6.9).</param>
public sealed record WindowDocument(string Path, DocumentMode Mode, CopyState? Copy = null, bool Refused = false)
{
    /// <summary>
    /// A file just opened (§6.2): local outside the Helper's <c>Copies/</c> folder, <see cref="DocumentMode.SupportOff"/>
    /// for a copy with Healdan support off, and otherwise <see cref="DocumentMode.Asking"/> until the Helper answers.
    /// </summary>
    public static WindowDocument Opened(string path, bool healdan, bool inCopies) =>
        new(System.IO.Path.GetFullPath(path), !inCopies ? DocumentMode.Local : !healdan ? DocumentMode.SupportOff : DocumentMode.Asking);

    /// <summary>
    /// The document after the Helper's answer about it, or after it didn't answer (<paramref name="state"/> null). A
    /// read-only answer for a copy the Helper had called editable is a refused save (§6.9). A read-only copy stays so,
    /// since the Helper doesn't hand a check-out back to an open window.
    /// </summary>
    public WindowDocument Answered(CopyState? state)
    {
        if (Mode is DocumentMode.Local or DocumentMode.SupportOff)
            return this; // the Helper isn't asked about these
        if (state is null)
            return Mode == DocumentMode.ReadOnly ? this : this with { Mode = DocumentMode.HelperAway };
        if (!state.Known)
            return Mode == DocumentMode.Asking ? this with { Mode = DocumentMode.Local } : this;
        if (Mode == DocumentMode.ReadOnly)
            return this with { Copy = state };
        if (state.ReadOnly)
            return this with { Mode = DocumentMode.ReadOnly, Copy = state, Refused = Copy is { Known: true, ReadOnly: false } };
        return this with { Mode = DocumentMode.Editable, Copy = state };
    }

    /// <summary>
    /// Whether the window keeps the copy's lock file while it's open (§6.8): an editable copy, as the Helper says, or as
    /// its name says while the Helper is away.
    /// </summary>
    public bool HoldsLock => Mode == DocumentMode.Editable || (Mode == DocumentMode.HelperAway && !ReadOnlyByName);

    /// <summary>Whether the window's saves are followed to Healdan (§6.4), which goes on while the Helper is away (§6.9).</summary>
    public bool FollowsSaves => HoldsLock;

    /// <summary>Whether the Helper is told when the window lets the copy go (§6.8): every copy, while support is on.</summary>
    public bool TellsHelper => Mode is DocumentMode.Asking or DocumentMode.Editable or DocumentMode.ReadOnly or DocumentMode.HelperAway;

    /// <summary>The window's title (§6.1). <paramref name="saving"/> shows only where saves go to Healdan.</summary>
    public string Title(bool edited, bool saving) =>
        Mode != DocumentMode.Local && CopyTitle is { } title
            ? DocumentTitle.ForCopy(title, edited, saving && FollowsSaves)
            : DocumentTitle.For(Path, edited);

    /// <summary>The bar across the window's top, if its mode has one (§6.1, §6.9).</summary>
    public WindowBar? Bar => Mode switch
    {
        DocumentMode.ReadOnly when Refused => new(WindowBar.Refused, BarAction.SaveAsNewDocument),
        DocumentMode.ReadOnly when Copy?.HoldBy is { Length: > 0 } holder => new(WindowBar.HeldBy(holder), BarAction.SaveAsNewDocument),
        DocumentMode.ReadOnly => new(WindowBar.ReadOnlyOpen, BarAction.SaveAsNewDocument),
        DocumentMode.HelperAway when ReadOnlyByName => new(WindowBar.HelperAwayReadOnly, BarAction.None),
        DocumentMode.HelperAway => new(WindowBar.HelperAway, BarAction.None),
        DocumentMode.SupportOff => new(WindowBar.SupportOff, BarAction.TurnOnHealdan),
        _ => null,
    };

    /// <summary>
    /// What the mode allows (§6.1, §6.3). A local window follows Healdan support as it is now; a copy's mode was set
    /// when it opened, and turning support off waits for its window to close (§5).
    /// </summary>
    public WindowCommands Commands(bool healdan)
    {
        var editable = !ReadOnlyByName;
        return Mode switch
        {
            DocumentMode.Local => new(Markup: true, Save: true, HealdanMenu: healdan, SaveToHealdan: healdan, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: healdan, HelperBoxes: healdan),
            // Save to Healdan on a copy is its ordinary save, as in the LibreOffice extension.
            DocumentMode.Editable => new(Markup: true, Save: true, HealdanMenu: true, SaveToHealdan: true, SaveAsNewVersion: true,
                SaveAsNewDocument: true, Properties: true, HelperBoxes: true),
            DocumentMode.ReadOnly => new(Markup: false, Save: false, HealdanMenu: true, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: true, Properties: false, HelperBoxes: true),
            // Healdan's commands are off with the Helper away. ⌘O and ⇧⌘S still try it first, and fall to the Mac's boxes at once.
            DocumentMode.HelperAway => new(Markup: editable, Save: editable, HealdanMenu: true, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: false, HelperBoxes: true),
            DocumentMode.Asking => new(Markup: false, Save: false, HealdanMenu: true, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: false, HelperBoxes: true),
            _ => new(Markup: false, Save: false, HealdanMenu: false, SaveToHealdan: false, SaveAsNewVersion: false,
                SaveAsNewDocument: false, Properties: false, HelperBoxes: false),
        };
    }

    /// <summary>
    /// The name the Helper's Save window starts with (§6.6): a copy's name without its doc ref, else the file's name
    /// without its extension, as the LibreOffice extension's.
    /// </summary>
    public string NameForHealdan =>
        (Mode == DocumentMode.Local ? null : DocumentName)
        ?? (System.IO.Path.GetFileNameWithoutExtension(Path) is { Length: > 0 } stem ? stem : "Untitled");

    /// <summary>The copy's name says it's read-only: <c>… (read-only).pdf</c>.</summary>
    private bool ReadOnlyByName => CopyName.Parse(Path)?.ReadOnly == true;

    /// <summary>
    /// A copy's title: the Helper's, as the LibreOffice extension shows it, else the copy's file name without its
    /// extension, which is what the Helper's would be. Null for a file not named as a copy.
    /// </summary>
    private string? CopyTitle =>
        Copy is { Known: true, Title.Length: > 0 } copy ? copy.Title
        : CopyName.Parse(Path) is not null ? System.IO.Path.GetFileNameWithoutExtension(Path)
        : null;

    /// <summary>The document's name without its doc ref: from the Helper's answer when it gives one, else from the file's name.</summary>
    private string? DocumentName =>
        (Copy is { Known: true } copy ? CopyName.NameIn(copy.Title, copy.DocRef) : null) ?? CopyName.Parse(Path)?.Name;
}
