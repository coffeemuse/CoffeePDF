using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoffeePdf.Toolbar;

/// <summary>The Text menu (toolbar spec §3): a text box's font, size, bold, italic and underline, and its alignment.</summary>
public sealed class TextFormatPicker : UserControl
{
    private PdfViewer? _viewer;
    private bool _showing; // the controls are being set from the viewer, not by a person

    public TextFormatPicker()
    {
        AutomationProperties.SetName(FontBox, "Font");
        AutomationProperties.SetName(SizeBox, "Size");
        FontBox.SelectionChanged += (_, _) => Set(v => { if (FontBox.SelectedItem is string font) v.TextAnnotationFont = font; });
        SizeBox.SelectionChanged += (_, _) => Set(v => { if (SizeBox.SelectedItem is float size) v.TextAnnotationFontSize = size; });
        foreach (var style in new[] { BoldButton, ItalicButton, UnderlineButton })
            style.IsCheckedChanged += (_, _) => Set(v => v.TextAnnotationFontAttributes = Attributes());
        AlignLeftButton.Click += (_, _) => Align(TextAnnotationAlignment.Left);
        AlignCentreButton.Click += (_, _) => Align(TextAnnotationAlignment.Center);
        AlignRightButton.Click += (_, _) => Align(TextAnnotationAlignment.Right);
        Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { FontBox, SizeBox } },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 2,
                    Children = { BoldButton, ItalicButton, UnderlineButton, new Border { Width = 8 }, AlignLeftButton, AlignCentreButton, AlignRightButton },
                },
            },
        };
    }

    internal ComboBox FontBox { get; } = new() { Width = 180 };
    internal ComboBox SizeBox { get; } = new() { Width = 80 };
    internal ToggleButton BoldButton { get; } = Toggle("Icon.Bold", "Bold");
    internal ToggleButton ItalicButton { get; } = Toggle("Icon.Italic", "Italic");
    internal ToggleButton UnderlineButton { get; } = Toggle("Icon.Underline", "Underline");
    internal ToggleButton AlignLeftButton { get; } = Toggle("Icon.AlignLeft", "Align Left");
    internal ToggleButton AlignCentreButton { get; } = Toggle("Icon.AlignCentre", "Centre");
    internal ToggleButton AlignRightButton { get; } = Toggle("Icon.AlignRight", "Align Right");

    /// <summary>Shows <paramref name="viewer"/>'s text format, which, with a text box selected, is that box's (spec §3).</summary>
    internal void Show(PdfViewer viewer)
    {
        _viewer = viewer;
        _showing = true;
        var font = viewer.TextAnnotationFont;
        FontBox.ItemsSource = ToolbarRules.Fonts.Contains(font) ? ToolbarRules.Fonts : [font, .. ToolbarRules.Fonts];
        FontBox.SelectedItem = font;
        var size = viewer.TextAnnotationFontSize;
        SizeBox.ItemsSource = ToolbarRules.FontSizes.Contains(size) ? ToolbarRules.FontSizes : [size, .. ToolbarRules.FontSizes];
        SizeBox.SelectedItem = size;
        BoldButton.IsChecked = viewer.TextAnnotationFontAttributes.HasFlag(FontAttributes.Bold);
        ItalicButton.IsChecked = viewer.TextAnnotationFontAttributes.HasFlag(FontAttributes.Italic);
        UnderlineButton.IsChecked = viewer.TextAnnotationFontAttributes.HasFlag(FontAttributes.Underline);
        ShowAlignment(viewer.TextAnnotationTextAlign);
        _showing = false;
    }

    private void Set(Action<PdfViewer> set)
    {
        if (!_showing && _viewer is { } viewer)
            set(viewer);
    }

    private FontAttributes Attributes() =>
        (BoldButton.IsChecked == true ? FontAttributes.Bold : FontAttributes.None)
        | (ItalicButton.IsChecked == true ? FontAttributes.Italic : FontAttributes.None)
        | (UnderlineButton.IsChecked == true ? FontAttributes.Underline : FontAttributes.None);

    private void Align(TextAnnotationAlignment alignment)
    {
        Set(v => v.TextAnnotationTextAlign = alignment);
        ShowAlignment(alignment);
    }

    private void ShowAlignment(TextAnnotationAlignment alignment)
    {
        AlignLeftButton.IsChecked = alignment == TextAnnotationAlignment.Left;
        AlignCentreButton.IsChecked = alignment == TextAnnotationAlignment.Center;
        AlignRightButton.IsChecked = alignment == TextAnnotationAlignment.Right;
    }

    private static ToggleButton Toggle(string icon, string name)
    {
        var button = new ToggleButton
        {
            Padding = new Thickness(5),
            Content = new PathIcon { Data = Application.Current?.FindResource(icon) as Geometry, Width = 16, Height = 16 },
        };
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        return button;
    }
}
