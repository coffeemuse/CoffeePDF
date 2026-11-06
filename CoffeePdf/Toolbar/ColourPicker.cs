using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoffeePdf.Toolbar;

/// <summary>
/// The Colour menu (toolbar spec §3): a tab for each colour the markup carries, the swatches, a button for none where
/// the colour may be none (<see cref="ColourSlot.None"/>), and a typed hex colour.
/// </summary>
public sealed class ColourPicker : UserControl
{
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly WrapPanel _swatches = new() { Width = 8 * 28 };
    private readonly Button _none = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _hex = new() { PlaceholderText = "#RRGGBB", Width = 110 };
    private IReadOnlyList<ColourSlot> _slots = [];
    private int _slot;

    public ColourPicker()
    {
        foreach (var (name, colour) in ToolbarRules.Swatches)
        {
            // The colour is drawn inside the button, as the toolbar's Colour button draws it, not as its Background,
            // which the theme replaces while the pointer is over it or presses it.
            var dot = new Ellipse { Width = 16, Height = 16, Fill = Brush(colour), StrokeThickness = 1 };
            dot.Bind(Shape.StrokeProperty, dot.GetResourceObservable("SystemControlForegroundBaseMediumBrush"));
            var swatch = new Button
            {
                Width = 24, Height = 24, Margin = new(2), Padding = new(0), Content = dot,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(swatch, name);
            ToolTip.SetTip(swatch, $"{name} ({ToolbarRules.Hex(colour)})");
            swatch.Click += (_, _) => Choose(colour);
            _swatches.Children.Add(swatch);
        }
        _none.Click += (_, _) => Choose(null);
        AutomationProperties.SetName(_hex, "Hex Colour");
        ToolTip.SetTip(_hex, "Hex Colour");
        _hex.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            ChooseHex(_hex.Text ?? "");
            e.Handled = true;
        };
        Content = new StackPanel { Spacing = 8, Children = { _tabs, _swatches, _none, _hex } };
    }

    /// <summary>The button for none, for the tests.</summary>
    internal Button NoneButton => _none;

    /// <summary>The swatches, in <see cref="ToolbarRules.Swatches"/>' order, for the tests.</summary>
    internal IReadOnlyList<Button> SwatchButtons => _swatches.Children.OfType<Button>().ToList();

    /// <summary>Raised after a colour is chosen, so the toolbar's button can show it.</summary>
    public event EventHandler? Chosen;

    /// <summary>Shows <paramref name="slots"/>, with the first one's tab chosen.</summary>
    internal void Show(IReadOnlyList<ColourSlot> slots)
    {
        _slots = slots;
        _tabs.Children.Clear();
        _tabs.IsVisible = slots.Count > 1;
        for (var i = 0; i < slots.Count; i++)
        {
            var index = i;
            var tab = new ToggleButton { Content = slots[i].Name };
            AutomationProperties.SetName(tab, slots[i].Name);
            ToolTip.SetTip(tab, slots[i].Name);
            tab.Click += (_, _) => ShowSlot(index);
            _tabs.Children.Add(tab);
        }
        ShowSlot(0);
    }

    /// <summary>Chooses the tab of the slot at <paramref name="index"/>.</summary>
    internal void ShowSlot(int index)
    {
        _slot = index;
        var tabs = _tabs.Children.OfType<ToggleButton>().ToList();
        for (var i = 0; i < tabs.Count; i++)
            tabs[i].IsChecked = i == index;
        var slot = _slots.ElementAtOrDefault(index);
        _none.IsVisible = slot?.None is not null;
        _none.Content = slot?.None;
        AutomationProperties.SetName(_none, slot?.None);
        ToolTip.SetTip(_none, slot?.None);
        _hex.Text = slot?.Get() is { } colour ? ToolbarRules.Hex(colour) : "";
    }

    /// <summary>
    /// Sets the chosen tab's colour; null is none, where the slot offers it. A colour keeps the opacity of the one it
    /// replaces (the highlight's default is half opaque, and an opaque swatch would hide the text under it), or is
    /// opaque when the slot has none.
    /// </summary>
    internal void Choose(PdfAnnotationColor? colour)
    {
        if (_slots.ElementAtOrDefault(_slot) is not { } slot || (colour is null && slot.None is null))
            return;
        if (colour is { } chosen)
            colour = new PdfAnnotationColor(chosen.R, chosen.G, chosen.B, slot.Get()?.A ?? 255);
        slot.Set(colour);
        ShowSlot(_slot);
        Chosen?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets the chosen tab's colour from typed text, or puts the box back when it isn't a colour.</summary>
    internal void ChooseHex(string text)
    {
        if (ToolbarRules.ParseHex(text) is { } colour)
            Choose(colour);
        else
            ShowSlot(_slot);
    }

    internal static IBrush Brush(PdfAnnotationColor colour) => new SolidColorBrush(Color.FromArgb(colour.A, colour.R, colour.G, colour.B));
}
