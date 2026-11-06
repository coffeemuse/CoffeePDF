using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;

namespace CoffeePdf.Toolbar;

/// <summary>The format menus the toolbar shows beside its tools (toolbar spec §3's table).</summary>
[Flags]
internal enum FormatMenus
{
    None = 0,
    Colour = 1,
    Line = 2,
    Text = 4,
}

/// <summary>What's selected on a page, as far as the format menus go.</summary>
internal enum PageSelection
{
    None,
    Shape,
    TextBox,
}

/// <summary>
/// The toolbar's small rules (toolbar spec §4). They hold no Avalonia state, so their tests need no window. The core
/// can't hold them, since they speak the PdfViewer's types.
/// </summary>
internal static class ToolbarRules
{
    /// <summary>The zoom menu's levels, 50 to 400% (spec §3).</summary>
    public static readonly IReadOnlyList<double> ZoomPresets = [0.5, 0.75, 1, 1.25, 1.5, 2, 4];

    /// <summary>The Shapes menu, in the spec's order.</summary>
    public static readonly IReadOnlyList<ShapeType> Shapes =
        [ShapeType.Line, ShapeType.Arrow, ShapeType.Rectangle, ShapeType.Circle, ShapeType.Polygon, ShapeType.Star];

    /// <summary>The Text menu's fonts, in probe 6's order: the built-in toolbar's (toolbar spec §10).</summary>
    public static readonly IReadOnlyList<string> Fonts =
        ["Helvetica", "Arial", "Times New Roman", "Courier", "Georgia", "Verdana"];

    /// <summary>The Text menu's sizes, in points, in probe 6's order: the built-in toolbar's (toolbar spec §10).</summary>
    public static readonly IReadOnlyList<float> FontSizes =
        [6, 8, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 60, 72];

    /// <summary>The Colour menu's swatches: yellow, green, blue, pink, purple, red, orange and black.</summary>
    public static readonly IReadOnlyList<PdfAnnotationColor> Swatches =
    [
        new(255, 212, 0, 255), new(52, 199, 89, 255), new(0, 122, 255, 255), new(255, 45, 85, 255),
        new(175, 82, 222, 255), new(255, 59, 48, 255), new(255, 149, 0, 255), new(0, 0, 0, 255),
    ];

    /// <summary>The format menus for <paramref name="tool"/>, armed, or for <paramref name="selection"/>, which wins.</summary>
    public static FormatMenus For(PdfViewerTool tool, PageSelection selection) => selection switch
    {
        PageSelection.Shape => FormatMenus.Colour | FormatMenus.Line,
        PageSelection.TextBox => FormatMenus.Colour | FormatMenus.Line | FormatMenus.Text,
        _ => tool switch
        {
            PdfViewerTool.Highlight or PdfViewerTool.Underline or PdfViewerTool.Strikeout or PdfViewerTool.Squiggly
                or PdfViewerTool.Note or PdfViewerTool.Stamp => FormatMenus.Colour,
            PdfViewerTool.Draw or PdfViewerTool.Shape => FormatMenus.Colour | FormatMenus.Line,
            PdfViewerTool.Text => FormatMenus.Colour | FormatMenus.Line | FormatMenus.Text,
            _ => FormatMenus.None,
        },
    };

    /// <summary>
    /// A typed zoom, "135%" or "135", as a zoom level (1 is 100%) clamped to [<paramref name="min"/>,
    /// <paramref name="max"/>], or null when it isn't a number (spec §5).
    /// </summary>
    public static double? ParseZoom(string? text, double min, double max)
    {
        var digits = text?.Trim().TrimEnd('%').Trim();
        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && double.IsFinite(percent)
            ? Math.Clamp(percent / 100, min, max)
            : null;
    }

    /// <summary>A zoom level as the zoom box shows it: a whole percentage.</summary>
    public static string ZoomText(double level) =>
        $"{Math.Round(level * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";

    /// <summary>A typed page number clamped to 1 to <paramref name="count"/>, or null when it isn't one (spec §5).</summary>
    public static int? ParsePage(string? text, int count) =>
        count > 0 && int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var page)
            ? Math.Clamp(page, 1, count)
            : null;

    /// <summary>
    /// <paramref name="labels"/> with <paramref name="label"/> first. The Stamp tool places the first label in
    /// <c>StampLabels</c> unless its own menu chose one, and nothing public chooses one (spec §6, probe 3).
    /// </summary>
    public static IReadOnlyList<string> WithLabelFirst(IReadOnlyList<string> labels, string label) =>
        [label, .. labels.Where(l => l != label)];

    /// <summary>A typed colour, "#RRGGBB" or "RRGGBB", opaque; null when it isn't one.</summary>
    public static PdfAnnotationColor? ParseHex(string? text)
    {
        var hex = text?.Trim().TrimStart('#');
        return hex is { Length: 6 } && int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
            ? new PdfAnnotationColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255)
            : null;
    }

    /// <summary>A colour as the Colour menu's box shows it: "#RRGGBB".</summary>
    public static string Hex(PdfAnnotationColor colour) => $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
}
