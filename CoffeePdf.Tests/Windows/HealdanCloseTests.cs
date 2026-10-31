using System.Text.Json.Nodes;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Locks;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.8: a copy's window lets the copy go when it closes or moves, and quitting waits for that.</summary>
[Trait("Category", "PdfViewer")]
public sealed class HealdanCloseTests : IDisposable
{
    private readonly FakeHelper _helper = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        _helper.Dispose();
    }

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.Close();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private IEnumerable<string?> ClosedPaths => _helper.CallsTo(ChannelRoutes.Closed).Select(b => (string?)b["path"]);

    [PdfViewerFact]
    public async Task Closing_a_copy_removes_its_lock_then_tells_the_Helper()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        bool? lockedWhenTold = null;
        _helper.Answer(ChannelRoutes.Closed, _ =>
        {
            lockedWhenTold = File.Exists(CopyLock.PathFor(copy));
            return Task.FromResult<JsonNode>(new JsonObject());
        });

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await CloseAsync(window);
            return true;
        });

        Assert.False(File.Exists(CopyLock.PathFor(copy)));
        Assert.False(lockedWhenTold);
        Assert.Equal(new[] { copy }, ClosedPaths);
    }

    [PdfViewerFact]
    public async Task Closing_with_changes_asks_saves_and_then_lets_the_copy_go()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            window.AskToSave = () => Task.FromResult(SaveChoice.Save);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.CloseWithPromptAsync();
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return true;
        });

        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
        Assert.Equal(new[] { copy }, ClosedPaths);
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
    }

    [PdfViewerFact]
    public async Task A_read_only_copy_is_let_go_too()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true);

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await CloseAsync(window);
            return true;
        });

        Assert.Equal(new[] { copy }, ClosedPaths);
    }

    [PdfViewerFact]
    public async Task With_support_off_the_Helper_hears_nothing()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link(enabled: false));
            await CloseAsync(window);
            return true;
        });

        Assert.Empty(_helper.Calls);
    }

    [PdfViewerFact]
    public async Task Saving_a_copy_as_a_local_file_lets_the_copy_go()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var local = Path.Combine(_dir, "Kept here.pdf");

        var (mode, title, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveToAsync(local);
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.Closed).Count == 1);
            var state = (window.Document?.Mode, window.Title, window.FilePath);
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(DocumentMode.Local, mode);
        Assert.Equal("Kept here.pdf", title);
        Assert.Equal(local, shown);
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
        Assert.Equal(new[] { copy }, ClosedPaths);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(copy));
    }

    [PdfViewerFact]
    public async Task Quitting_closes_every_copy_and_waits_until_the_Helper_has_heard()
    {
        string first = _helper.AddCopy("First", 1, "1"), second = _helper.AddCopy("Second", 2, "1");

        var (held, told) = await Headless.RunAsync(async () =>
        {
            var (a, _) = await TestWindows.OpenAsync(first, _helper.Link());
            var (b, _) = await TestWindows.OpenAsync(second, _helper.Link());
            var holding = a.HoldsCopy && b.HoldsCopy;
            await App.CloseAndLetGoAsync([a, b]);
            return (holding, ClosedPaths.ToList());
        });

        Assert.True(held);
        Assert.Equal(new[] { first, second }.Order(), told.Order());
    }
}
