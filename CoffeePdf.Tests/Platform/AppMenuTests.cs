using Avalonia.Controls;

namespace CoffeePdf.Tests.Platform;

/// <summary>The CoffeePDF menu: About CoffeePDF first, as on every Mac app, then Settings… (the owner's check 2).</summary>
public sealed class AppMenuTests
{
    [Fact]
    public async Task The_app_menu_starts_with_About_CoffeePDF_then_Settings()
    {
        var headers = await Headless.RunAsync(() => Task.FromResult(NativeMenu.GetMenu(App.Current)!.Items
            .Select(i => i is NativeMenuItemSeparator ? "—" : ((NativeMenuItem)i).Header)
            .ToList()));

        var expected = OperatingSystem.IsMacOS()
            ? new string?[] { "About CoffeePDF", "—", "Settings…" }
            : new string?[] { "Settings…" };
        Assert.Equal(expected, headers);
    }
}
