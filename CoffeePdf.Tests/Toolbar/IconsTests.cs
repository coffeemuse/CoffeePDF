using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>The toolbar spec's §3: the toolbar's icons, each a shape on Fluent's 20-point grid.</summary>
public sealed class IconsTests
{
    [Fact]
    public async Task Every_icon_is_a_shape_on_the_20_point_grid_and_the_app_has_them()
    {
        var (count, wrong, inApp) = await Headless.RunAsync(() =>
        {
            var icons = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://CoffeePDF/Toolbar/Icons.axaml"));
            var wrong = new List<string>();
            foreach (var key in icons.Keys)
            {
                // The dictionary defers its values, so ask it for each one.
                icons.TryGetResource(key, null, out var value);
                var bounds = (value as StreamGeometry)?.Bounds;
                if (bounds is not { Width: > 0, Height: > 0, Left: >= 0, Top: >= 0, Right: <= 20, Bottom: <= 20 })
                    wrong.Add($"{key}: {bounds}");
            }
            var inApp = App.Current.TryFindResource("Icon.Stamp", out var stamp) && stamp is StreamGeometry;
            return Task.FromResult((icons.Count, wrong, inApp));
        });

        Assert.Equal(24, count);
        Assert.Empty(wrong);
        Assert.True(inApp);
    }
}
