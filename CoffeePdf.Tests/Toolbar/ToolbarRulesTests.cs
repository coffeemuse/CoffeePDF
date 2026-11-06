using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using CoffeePdf.Toolbar;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>The toolbar spec's §3 and §5: the toolbar's rules, without a window.</summary>
public sealed class ToolbarRulesTests
{
    [Theory]
    [InlineData(PdfViewerTool.Highlight, "None", "Colour")]
    [InlineData(PdfViewerTool.Underline, "None", "Colour")]
    [InlineData(PdfViewerTool.Strikeout, "None", "Colour")]
    [InlineData(PdfViewerTool.Squiggly, "None", "Colour")]
    [InlineData(PdfViewerTool.Note, "None", "Colour")]
    [InlineData(PdfViewerTool.Stamp, "None", "Colour")]
    [InlineData(PdfViewerTool.Draw, "None", "Colour, Line")]
    [InlineData(PdfViewerTool.Shape, "None", "Colour, Line")]
    [InlineData(PdfViewerTool.Text, "None", "Colour, Line, Text")]
    [InlineData(PdfViewerTool.Link, "None", "None")]
    [InlineData(PdfViewerTool.None, "None", "None")]
    // A selection gets none, whatever is armed: the PdfViewer ends it at a press outside it, and its style properties
    // don't restyle it (the owner's check 6, probes 8 and 8b). Its own right-click menu does.
    [InlineData(PdfViewerTool.None, "Shape", "None")]
    [InlineData(PdfViewerTool.None, "TextBox", "None")]
    [InlineData(PdfViewerTool.Shape, "Shape", "None")]
    [InlineData(PdfViewerTool.Text, "TextBox", "None")]
    [InlineData(PdfViewerTool.Highlight, "Shape", "None")]
    public void An_armed_tool_gets_the_specs_format_menus_and_a_selection_none(PdfViewerTool tool, string selection, string menus) =>
        Assert.Equal(Enum.Parse<FormatMenus>(menus), ToolbarRules.For(tool, Enum.Parse<PageSelection>(selection)));

    [Theory]
    [InlineData("135%", 1.35)]
    [InlineData("135", 1.35)]
    [InlineData(" 50 % ", 0.5)]
    [InlineData("100000", 8)]
    [InlineData("1", 0.25)]
    public void A_typed_zoom_is_read_and_clamped(string typed, double level) =>
        Assert.Equal(level, ToolbarRules.ParseZoom(typed, min: 0.25, max: 8)!.Value, precision: 6);

    [Theory]
    [InlineData("")]
    [InlineData("big")]
    [InlineData("%")]
    [InlineData(null)]
    public void Anything_else_is_no_zoom(string? typed) => Assert.Null(ToolbarRules.ParseZoom(typed, 0.25, 8));

    [Theory]
    [InlineData(1.0, "100%")]
    [InlineData(1.356, "136%")]
    [InlineData(0.5, "50%")]
    public void A_zoom_shows_as_a_whole_percentage(double level, string shown) => Assert.Equal(shown, ToolbarRules.ZoomText(level));

    [Theory]
    [InlineData("2", 12, 2)]
    [InlineData(" 12 ", 12, 12)]
    [InlineData("40", 12, 12)]
    [InlineData("0", 12, 1)]
    public void A_typed_page_is_read_and_clamped(string typed, int count, int page) =>
        Assert.Equal(page, ToolbarRules.ParsePage(typed, count));

    [Theory]
    [InlineData("two", 12)]
    [InlineData("-3", 12)]
    [InlineData("", 12)]
    [InlineData("1", 0)]
    public void Anything_else_is_no_page(string typed, int count) => Assert.Null(ToolbarRules.ParsePage(typed, count));

    [Fact]
    public void A_chosen_stamp_label_goes_first_and_the_rest_keep_their_order() =>
        Assert.Equal(new[] { "Draft", "Approved", "Final", "Void" }, ToolbarRules.WithLabelFirst(["Approved", "Draft", "Final", "Void"], "Draft"));

    [Fact]
    public void A_hex_colour_is_read_with_or_without_its_hash()
    {
        Assert.Equal(new PdfAnnotationColor(0x11, 0x22, 0xAB, 255), ToolbarRules.ParseHex("#1122ab"));
        Assert.Equal(new PdfAnnotationColor(0x11, 0x22, 0xAB, 255), ToolbarRules.ParseHex(" 1122AB "));
        Assert.Null(ToolbarRules.ParseHex("#12345"));
        Assert.Null(ToolbarRules.ParseHex("#GG0000"));
        Assert.Equal("#1122AB", ToolbarRules.Hex(new PdfAnnotationColor(0x11, 0x22, 0xAB, 255)));
    }

    [Fact]
    public void A_colour_is_shown_as_it_looks_on_a_white_page()
    {
        // The owner's check 1 (2026-11-06): the half-opaque highlight's sample, drawn with its opacity, blended with the
        // dark toolbar and looked darker than the highlight on the page.
        Assert.Equal(new PdfAnnotationColor(0, 122, 255, 255), ToolbarRules.OnPaper(new PdfAnnotationColor(0, 122, 255, 255)));
        Assert.Equal(new PdfAnnotationColor(252, 230, 172, 255), ToolbarRules.OnPaper(new PdfAnnotationColor(0xFA, 0xCD, 0x5A, 0x80)));
        Assert.Equal(new PdfAnnotationColor(255, 255, 255, 255), ToolbarRules.OnPaper(new PdfAnnotationColor(0, 0, 0, 0)));
    }

    [Fact]
    public void The_lists_are_the_specs()
    {
        Assert.Equal(new[] { 0.5, 0.75, 1, 1.25, 1.5, 2, 4 }, ToolbarRules.ZoomPresets);
        Assert.Equal(new[] { ShapeType.Line, ShapeType.Arrow, ShapeType.Rectangle, ShapeType.Circle, ShapeType.Polygon, ShapeType.Star }, ToolbarRules.Shapes);
        Assert.Equal(["Yellow", "Green", "Blue", "Pink", "Purple", "Red", "Orange", "Black"], ToolbarRules.Swatches.Select(s => s.Name));
        Assert.NotEmpty(ToolbarRules.Fonts);
        Assert.NotEmpty(ToolbarRules.FontSizes);
    }
}
