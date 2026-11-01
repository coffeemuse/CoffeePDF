using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Input;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Settings;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Platform;
using CoffeePdf.Tests.Windows;

namespace CoffeePdf.Tests.Platform;

/// <summary>
/// The menu bar while no document window is in front: the CoffeePDF menu, then File and Healdan, so ⌘O and Open Recent
/// work with no window open. Each test makes a bar of its own, with a fake Helper's link, its own Open Recent, and the
/// files it opens collected instead of shown.
/// </summary>
public sealed class AppMenuBarTests : IDisposable
{
    private readonly FakeHelper _helper = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;
    private readonly List<string> _opened = [];
    private readonly List<string> _errors = [];

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        _helper.Dispose();
    }

    private AppMenuBar Bar(HealdanLink link, RecentDocuments? recent = null) =>
        new(new NativeMenu(), link, recent ?? new RecentDocuments(_dir), _opened.Add)
        {
            ReportError = (heading, detail) =>
            {
                _errors.Add($"{heading} {detail}");
                return Task.CompletedTask;
            },
        };

    /// <summary>A menu's items, each with "on" or "off", and "—" for a separator.</summary>
    private static List<string> Items(NativeMenu menu) =>
        menu.Items.Select(i => i is NativeMenuItemSeparator ? "—" : $"{((NativeMenuItem)i).Header} {(((NativeMenuItem)i).IsEnabled ? "on" : "off")}").ToList();

    private static List<string?> Headers(NativeMenu menu) =>
        menu.Items.Select(i => i is NativeMenuItemSeparator ? "—" : ((NativeMenuItem)i).Header).ToList();

    private string File(string name)
    {
        var path = Path.Combine(_dir, name);
        System.IO.File.WriteAllBytes(path, []);
        return path;
    }

    [Fact]
    public async Task The_app_puts_File_and_Healdan_after_the_CoffeePDF_menu()
    {
        var (appMenuFirst, after) = await Headless.RunAsync(() =>
        {
            // macOS shows the menu that holds the app's while no window with a menu of its own is in front
            // (Avalonia.Native's AvaloniaNativeMenuExporter).
            var appMenu = NativeMenu.GetMenu(App.Current)!;
            var bar = appMenu.Parent?.Parent;
            return Task.FromResult((
                bar?.Items.FirstOrDefault() is NativeMenuItem first && first.Menu == appMenu,
                bar is null ? [] : Headers(bar).Skip(1).ToList()));
        });

        Assert.True(appMenuFirst);
        Assert.Equal(new[] { "File", "Healdan" }, after);
    }

    [Fact]
    public async Task File_has_a_windows_items_with_only_Open_and_Open_Recent_on()
    {
        var items = await Headless.RunAsync(() => Task.FromResult(Items(Bar(_helper.Link()).MenuItem("File").Menu!)));

        Assert.Equal(new[] { "Open… on", "Open Recent on", "—", "Close off", "Save off", "Save As… off", "—", "Print… off" }, items);
    }

    [Fact]
    public async Task Files_items_have_a_windows_shortcuts()
    {
        (string Header, string Gesture)[] shortcuts =
            [("Open…", "Meta+O"), ("Close", "Meta+W"), ("Save", "Meta+S"), ("Save As…", "Meta+Shift+S"), ("Print…", "Meta+P")];

        var wrong = await Headless.RunAsync(() =>
        {
            var bar = Bar(_helper.Link());
            return Task.FromResult(shortcuts.Where(s => !Equals(bar.MenuItem(s.Header).Gesture, KeyGesture.Parse(s.Gesture)))
                .Select(s => s.Header).ToList());
        });

        Assert.Empty(wrong);
    }

    [Fact]
    public async Task Open_Recent_lists_the_recent_files_newest_first_follows_them_and_opens_one()
    {
        var a = File("a.pdf");
        var b = File("b.pdf");
        var recent = new RecentDocuments(_dir);
        recent.Add(a);

        var (before, after) = await Headless.RunAsync(() =>
        {
            var bar = Bar(_helper.Link(), recent);
            var first = Headers(bar.MenuItem("Open Recent").Menu!);
            recent.Add(b);
            var second = Headers(bar.MenuItem("Open Recent").Menu!);
            bar.MenuItem("a.pdf").Command!.Execute(null);
            return Task.FromResult((first, second));
        });

        Assert.Equal(new[] { "a.pdf", "—", "Clear Menu" }, before);
        Assert.Equal(new[] { "b.pdf", "a.pdf", "—", "Clear Menu" }, after);
        Assert.Equal(new[] { a }, _opened);
    }

    [Fact]
    public async Task Clear_Menu_empties_Open_Recent()
    {
        var recent = new RecentDocuments(_dir);
        recent.Add(File("a.pdf"));

        var (items, paths) = await Headless.RunAsync(() =>
        {
            var bar = Bar(_helper.Link(), recent);
            bar.MenuItem("Clear Menu").Command!.Execute(null);
            var menu = bar.MenuItem("Open Recent").Menu!;
            var clear = (NativeMenuItem)menu.Items.Single();
            return Task.FromResult(($"{clear.Header} {(clear.Command!.CanExecute(null) ? "on" : "off")}", recent.Paths));
        });

        Assert.Equal("Clear Menu off", items);
        Assert.Empty(paths);
    }

    [Fact]
    public async Task The_Healdan_menu_shows_while_support_is_on_with_its_items_off()
    {
        var link = _helper.Link(enabled: false);

        var (off, on, offAgain, items) = await Headless.RunAsync(() =>
        {
            var bar = Bar(link);
            var hidden = bar.MenuItem("Healdan").IsVisible;
            link.Settings.HealdanEnabled = true;
            var shown = bar.MenuItem("Healdan").IsVisible;
            var healdanItems = Items(bar.MenuItem("Healdan").Menu!);
            link.Settings.HealdanEnabled = false;
            return Task.FromResult((hidden, shown, bar.MenuItem("Healdan").IsVisible, healdanItems));
        });

        Assert.False(off);
        Assert.True(on);
        Assert.False(offAgain);
        Assert.Equal(new[] { "Save to Healdan… off", "Save as New Version off", "Save as New Document… off", "—", "Properties… off" }, items);
    }

    [Fact]
    public async Task With_support_off_Open_is_the_Macs_Open_panel_and_opens_each_file_chosen()
    {
        var panels = await Headless.RunAsync(async () =>
        {
            var bar = Bar(_helper.Link(enabled: false));
            var shown = 0;
            bar.PickFilesToOpen = () =>
            {
                shown++;
                return Task.FromResult<IReadOnlyList<string>>(["/chosen/a.pdf", "/chosen/b.pdf"]);
            };
            await bar.OpenAsync();
            return shown;
        });

        Assert.Equal(1, panels);
        Assert.Equal(new[] { "/chosen/a.pdf", "/chosen/b.pdf" }, _opened);
        Assert.Empty(_helper.Calls);
    }

    [Fact]
    public async Task With_support_on_Open_is_the_Helpers_Open_window_whose_way_back_is_the_Macs()
    {
        var answers = new Queue<string>(["opened", "cancel", "local"]);
        _helper.Answer(ChannelRoutes.OpenDialog, _ => Task.FromResult<JsonNode>(new JsonObject { ["action"] = answers.Dequeue() }));

        var (afterTwo, afterThree) = await Headless.RunAsync(async () =>
        {
            var bar = Bar(_helper.Link());
            var panels = 0;
            bar.PickFilesToOpen = () =>
            {
                panels++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            await bar.OpenAsync(); // opened: the Helper opens what was chosen
            await bar.OpenAsync(); // cancel
            var two = panels;
            await bar.OpenAsync(); // local: the Mac's Open panel
            return (two, panels);
        });

        Assert.Equal(0, afterTwo);
        Assert.Equal(1, afterThree);
        Assert.Equal(3, _helper.CallsTo(ChannelRoutes.OpenDialog).Count);
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task With_the_Helper_away_Open_is_the_Macs_Open_panel()
    {
        _helper.GoAway();

        var panels = await Headless.RunAsync(async () =>
        {
            var bar = Bar(_helper.Link());
            var shown = 0;
            bar.PickFilesToOpen = () =>
            {
                shown++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            await bar.OpenAsync();
            return shown;
        });

        Assert.Equal(1, panels);
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task An_error_from_the_Helpers_Open_window_is_shown_then_the_Macs_Open_panel()
    {
        _helper.Fail(ChannelRoutes.OpenDialog, 500, "internal-error");

        var panels = await Headless.RunAsync(async () =>
        {
            var bar = Bar(_helper.Link());
            var shown = 0;
            bar.PickFilesToOpen = () =>
            {
                shown++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            await bar.OpenAsync();
            return shown;
        });

        Assert.Equal(1, panels);
        Assert.Equal("Healdan Helper couldn’t show its Open window. Healdan Helper answered 500 (internal-error)", Assert.Single(_errors));
    }

    [Fact]
    public async Task The_Open_item_opens()
    {
        var panels = await Headless.RunAsync(async () =>
        {
            var bar = Bar(_helper.Link(enabled: false));
            var shown = 0;
            bar.PickFilesToOpen = () =>
            {
                shown++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            bar.MenuItem("Open…").Command!.Execute(null);
            await TestWindows.WaitForAsync(() => shown == 1);
            return shown;
        });

        Assert.Equal(1, panels);
    }
}
