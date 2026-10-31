using System.Text.Json.Nodes;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.1, §6.3 and §6.7: the Healdan menu in each mode, ⌘O and ⇧⌘S through the Helper, and Properties.</summary>
[Trait("Category", "PdfViewer")]
public sealed class HealdanMenuTests : IDisposable
{
    private static readonly string[] Items = ["Save to Healdan…", "Save as New Version", "Save as New Document…", "Properties…"];

    private readonly FakeHelper _helper = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        _helper.Dispose();
    }

    private string Local()
    {
        var path = Path.Combine(_dir, $"Report {Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
        await window.CloseWithPromptAsync();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>The Healdan menu, shown or hidden, with a 1 or 0 for each of its items, and whether Save is on: "shown 1001 save".</summary>
    private static string Menu(DocumentWindow window) =>
        $"{(window.MenuItem("Healdan").IsVisible ? "shown" : "hidden")} " +
        $"{string.Concat(Items.Select(item => window.MenuItem(item).IsEnabled ? "1" : "0"))} " +
        $"{(window.MenuItem("Save").IsEnabled ? "save" : "no save")}";

    [PdfViewerFact]
    public async Task Each_mode_has_its_Healdan_items()
    {
        var local = Local();
        var editable = _helper.AddCopy("Editable", 1, "1");
        var readOnly = _helper.AddCopy("Held", 2, "1", readOnly: true);
        var away = _helper.AddCopy("Away", 3, "1");

        var menus = await Headless.RunAsync(async () =>
        {
            var found = new List<string>();
            foreach (var path in new[] { local, editable, readOnly })
            {
                var (window, _) = await TestWindows.OpenAsync(path, _helper.Link());
                found.Add(Menu(window));
                await CloseAsync(window);
            }
            _helper.GoAway();
            var (awayWindow, _) = await TestWindows.OpenAsync(away, _helper.Link());
            found.Add(Menu(awayWindow));
            await CloseAsync(awayWindow);
            return found;
        });

        // Local: Save to Healdan and Properties. Editable: everything. Read-only: Save as New Document. Helper away: none.
        Assert.Equal(new[] { "shown 1001 save", "shown 1111 save", "shown 0010 no save", "shown 0000 save" }, menus);
    }

    [PdfViewerFact]
    public async Task With_support_off_there_is_no_Healdan_menu_until_its_turned_on()
    {
        var link = _helper.Link(enabled: false);

        var (before, after) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), link);
            var hidden = Menu(window);
            link.Settings.HealdanEnabled = true;
            var shown = Menu(window);
            await CloseAsync(window);
            return (hidden, shown);
        });

        Assert.Equal("hidden 0000 save", before);
        Assert.Equal("shown 1001 save", after);
    }

    [PdfViewerFact]
    public async Task Open_shows_the_Helpers_Open_window_whose_way_back_is_the_Macs()
    {
        var answers = new Queue<string>(["opened", "cancel", "local"]);
        _helper.Answer(ChannelRoutes.OpenDialog, _ => Task.FromResult<JsonNode>(new JsonObject { ["action"] = answers.Dequeue() }));

        var (afterTwo, afterThree) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link());
            var panels = 0;
            window.PickFilesToOpen = () =>
            {
                panels++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            await window.OpenAsync(); // opened: the Helper opens what was chosen
            await window.OpenAsync(); // cancel
            var two = panels;
            await window.OpenAsync(); // local: the Mac's Open panel
            var three = panels;
            await CloseAsync(window);
            return (two, three);
        });

        Assert.Equal(0, afterTwo);
        Assert.Equal(1, afterThree);
        Assert.Equal(3, _helper.CallsTo(ChannelRoutes.OpenDialog).Count);
        Assert.All(_helper.CallsTo(ChannelRoutes.OpenDialog), body => Assert.Equal("CoffeePDF", (string?)body["app"]));
    }

    [PdfViewerFact]
    public async Task With_support_off_Open_and_Save_As_are_the_Macs_and_the_Helper_isnt_asked()
    {
        var (opened, saved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link(enabled: false));
            int open = 0, save = 0;
            window.PickFilesToOpen = () =>
            {
                open++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            window.PickSaveTarget = _ =>
            {
                save++;
                return Task.FromResult<string?>(null);
            };
            await window.OpenAsync();
            await window.SaveAsAsync();
            await CloseAsync(window);
            return (open, save);
        });

        Assert.Equal(1, opened);
        Assert.Equal(1, saved);
        Assert.Empty(_helper.Calls);
    }

    [PdfViewerFact]
    public async Task An_error_from_the_Open_window_is_shown_then_the_Macs_Open_panel()
    {
        _helper.Fail(ChannelRoutes.OpenDialog, 500, "internal-error");

        var (panels, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(Local(), _helper.Link());
            var shown = 0;
            window.PickFilesToOpen = () =>
            {
                shown++;
                return Task.FromResult<IReadOnlyList<string>>([]);
            };
            await window.OpenAsync();
            var state = (shown, errorList.ToList());
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(1, panels);
        Assert.Equal("Healdan Helper couldn’t show its Open window. Healdan Helper answered 500 (internal-error)", Assert.Single(errors));
    }

    [PdfViewerFact]
    public async Task Save_As_is_the_Helpers_Save_window_while_support_is_on()
    {
        var panel = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link());
            var shown = false;
            window.PickSaveTarget = _ =>
            {
                shown = true;
                return Task.FromResult<string?>(null);
            };
            await window.SaveAsAsync(); // the fake's Save window answers cancel
            await CloseAsync(window);
            return shown;
        });

        Assert.False(panel);
        Assert.Null(Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew))["path"]);
    }

    [PdfViewerFact]
    public async Task Properties_of_a_pdf_that_isnt_in_Healdan_runs_Save_to_Healdan()
    {
        _helper.Answer(ChannelRoutes.Properties, new JsonObject { ["action"] = "file" });

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(Local(), _helper.Link());
            await window.PropertiesAsync();
            await CloseAsync(window);
            return true;
        });

        Assert.Equal(new[] { "properties", "save-new" }, _helper.Routes);
    }

    [PdfViewerFact]
    public async Task Properties_saved_moves_the_title_on()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        _helper.Answer(ChannelRoutes.Properties, _ =>
        {
            // Renamed in the Helper's Properties window. The copy keeps its file name while it's open.
            _helper.SetState(_helper.StateOf(copy) with { Title = "Signed Settlement Agreement #1234 v2.1" });
            return Task.FromResult<JsonNode>(new JsonObject { ["action"] = "saved" });
        });

        var title = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.PropertiesAsync();
            var shown = window.Title;
            await CloseAsync(window);
            return shown;
        });

        Assert.Equal("Signed Settlement Agreement #1234 v2.1", title);
    }
}
