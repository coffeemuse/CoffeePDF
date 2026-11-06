using Avalonia.Controls.Pdf.Core;

namespace CoffeePdf.Toolbar;

/// <summary>
/// One colour the Colour menu edits (toolbar spec §3): its tab's name, whether it may be none (a fill or a border), and
/// the viewer's property it reads and sets.
/// </summary>
internal sealed record ColourSlot(string Name, bool AllowsNone, Func<PdfAnnotationColor?> Get, Action<PdfAnnotationColor?> Set);
