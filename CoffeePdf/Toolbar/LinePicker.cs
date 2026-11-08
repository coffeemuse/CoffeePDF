using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoffeePdf.Toolbar;

/// <summary>The Line menu (toolbar spec §3): the width, 0.5 to 20 points, the viewer's own range, with a preview.</summary>
public sealed class LinePicker : UserControl
{
    private readonly Slider _width = new() { Minimum = 0.5, Maximum = 20, TickFrequency = 0.5, IsSnapToTickEnabled = true, Width = 180 };
    private readonly TextBlock _label = new();
    private readonly Border _preview = new() { Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Gray };
    private Action<float>? _set;
    private bool _showing; // the slider is being set from the viewer, not by a person

    public LinePicker()
    {
        AutomationProperties.SetName(_width, "Line Width");
        _width.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty)
                return;
            if (!_showing)
                _set?.Invoke((float)_width.Value);
            ShowPreview();
        };
        Content = new StackPanel { Spacing = 8, Children = { _width, _label, _preview } };
    }

    /// <summary>Shows the width <paramref name="get"/> reads, and sets new ones through <paramref name="set"/>.</summary>
    internal void Show(Func<float> get, Action<float> set)
    {
        _set = set;
        _showing = true;
        _width.Value = get();
        _showing = false;
        ShowPreview();
    }

    /// <summary>Sets <paramref name="width"/>, as the slider does.</summary>
    internal void Choose(double width) => _width.Value = width;

    private void ShowPreview()
    {
        _label.Text = $"{_width.Value:0.#} pt";
        _preview.Height = Math.Max(1, _width.Value);
    }
}
