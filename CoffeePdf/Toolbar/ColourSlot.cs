using Avalonia.Controls.Pdf.Core;

namespace CoffeePdf.Toolbar;

/// <summary>
/// One colour the Colour menu edits (toolbar spec §3): its tab's name, what its menu calls none where it may be none
/// ("No Colour" for a fill, a border or a stroke; "Label's Colour" for a stamp, which then takes its label's), and the
/// viewer's property it reads and sets.
/// </summary>
internal sealed record ColourSlot(
    string Name, string? None, Func<PdfAnnotationColor?> Get, Action<PdfAnnotationColor?> Set)
{
    /// <summary>What a fill, a border or a stroke calls none.</summary>
    public const string NoColour = "No Colour";
}
