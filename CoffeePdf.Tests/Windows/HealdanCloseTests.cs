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

    [PdfViewerFact]
    public async Task Quitting_waits_for_the_Helper_to_hear_of_an_edited_copy_its_prompt_closed()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var heard = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _helper.Answer(ChannelRoutes.Closed, _ => heard.Task);

        var (doneBeforeHeard, told) = await Headless.RunAsync(async () =>
        {
            try
            {
                var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
                window.AskToSave = () => Task.FromResult(SaveChoice.Save);
                await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
                // As the app keeps them: the windows still open, and the Released of each that has closed.
                List<DocumentWindow> open = [window];
                List<Task> releases = [];
                window.Closed += (_, _) =>
                {
                    open.Remove(window);
                    releases.Add(window.Released);
                };

                var quit = App.CloseAllAndWaitAsync(() => open, () => [.. releases]);
                await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.Closed).Count == 1);
                var early = quit.IsCompleted;
                heard.SetResult(new JsonObject());
                var closedAll = await quit.WaitAsync(TimeSpan.FromSeconds(10));
                return (early, closedAll);
            }
            finally
            {
                heard.TrySetResult(new JsonObject()); // a failing test mustn't leave the fake Helper holding a call
            }
        });

        Assert.False(doneBeforeHeard);
        Assert.True(told);
        Assert.Equal(new[] { copy }, ClosedPaths);
    }

    [PdfViewerFact]
    public async Task A_close_during_a_save_lets_the_copy_go_only_once_the_save_is_written()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        int? notesWhenTold = null;
        _helper.Answer(ChannelRoutes.Closed, _ =>
        {
            notesWhenTold = PdfAnnotations.Read(File.ReadAllBytes(copy), 0).Count;
            return Task.FromResult<JsonNode>(new JsonObject());
        });

        var (lockedWhileWriting, releasedWhileWriting, saved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            var held = TestWindows.HoldWrites(window);
            var saving = window.SaveAsync();
            bool locked, early;
            try
            {
                await TestWindows.WaitForAsync(() => held.Reached);
                await window.CloseWithPromptAsync(); // Don't Save, while the save is writing the copy
                await TestWindows.WaitForAsync(() => closed);
                (locked, early) = (File.Exists(CopyLock.PathFor(copy)), window.Released.IsCompleted);
            }
            finally
            {
                held.Release();
            }
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return (locked, early, await saving);
        });

        Assert.True(lockedWhileWriting);
        Assert.False(releasedWhileWriting);
        Assert.True(saved);
        Assert.Equal(1, notesWhenTold); // the Helper heard only once the save was in the copy
        Assert.Equal(new[] { copy }, ClosedPaths);
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
    }

    [PdfViewerFact]
    public async Task A_close_during_a_save_to_a_local_file_lets_the_copy_go_once()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var local = Path.Combine(_dir, "Kept here.pdf");

        var saved = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            var held = TestWindows.HoldWrites(window);
            var saving = window.SaveToAsync(local); // as the Mac's Save panel does
            try
            {
                await TestWindows.WaitForAsync(() => held.Reached);
                await window.CloseWithPromptAsync(); // Don't Save, while the save is writing the local file
            }
            finally
            {
                held.Release();
            }
            var result = await saving;
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            // Every copy the window began letting go is let go by now, so a second /v1/closed would have been heard.
            await TestWindows.WaitForAsync(() => !window.HoldsCopy);
            return result;
        });

        Assert.True(saved);
        Assert.Equal(new[] { copy }, ClosedPaths);
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(local), 0)).Author);
    }

    [PdfViewerFact]
    public async Task A_save_still_waiting_when_the_window_closes_writes_nothing()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (first, second, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            var held = TestWindows.HoldWrites(window);
            var saving = window.SaveAsync();
            Task<bool> waiting;
            try
            {
                await TestWindows.WaitForAsync(() => held.Reached);
                await window.Viewer.AddStickyNoteAsync(0, 200, 200, "After the save began");
                waiting = window.SaveAsync(); // ⌘S again, waiting behind the first
                await window.CloseWithPromptAsync(); // Don't Save
            }
            finally
            {
                held.Release();
            }
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return (await saving, await waiting, errorList.ToList());
        });

        Assert.True(first);
        Assert.False(second);
        Assert.Empty(errors); // it didn't try, so there's no failure to show
        Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)); // the note made after the first save began isn't written
        Assert.Equal(new[] { copy }, ClosedPaths);
    }

    [PdfViewerFact]
    public async Task Save_at_the_prompt_of_a_copy_whose_save_was_refused_saves_it_as_a_new_document()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var filed = Path.Combine(_helper.Folders.Copies, "Settlement Agreement #1 v1.pdf");
        _helper.Answer(ChannelRoutes.SaveNew, new JsonObject
        {
            ["action"] = "save",
            ["folderId"] = 42,
            ["name"] = "Settlement Agreement",
            ["format"] = "pdf",
            ["tempPath"] = Path.Combine(_helper.Folders.Temp, "save-new.pdf"),
        });

        var (mode, closed) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            // Healdan refused a save (spec §6.9): the Helper answers the copy read-only now, and the note is unsaved.
            _helper.SetState(_helper.StateOf(copy) with { ReadOnly = true });
            await window.AskHelperAsync();
            var refused = window.Document?.Mode;
            window.AskToSave = () => Task.FromResult(SaveChoice.Save);
            var result = await window.CloseWithPromptAsync();
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return (refused, result);
        });

        Assert.Equal(DocumentMode.ReadOnly, mode);
        Assert.True(closed);
        Assert.Equal(new[] { "state", "state", "save-new", "file", "closed", "adopted", "closed" }, _helper.Routes);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(filed), 0)).Author);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(copy));
        Assert.Equal(new[] { copy, filed }, ClosedPaths);
    }

    [PdfViewerFact]
    public async Task A_window_that_moved_off_a_copy_is_released_only_once_the_Helper_has_heard()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var local = Path.Combine(_dir, "Kept here.pdf");
        var heard = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _helper.Answer(ChannelRoutes.Closed, _ => heard.Task);

        var (holdingWhileTold, doneBeforeHeard, holdingAfter) = await Headless.RunAsync(async () =>
        {
            try
            {
                var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
                await window.SaveToAsync(local);
                await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.Closed).Count == 1);
                var holding = window.HoldsCopy;
                var closed = false;
                window.Closed += (_, _) => closed = true;
                window.Close();
                await TestWindows.WaitForAsync(() => closed);
                var early = window.Released.IsCompleted;
                heard.SetResult(new JsonObject());
                await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
                return (holding, early, window.HoldsCopy);
            }
            finally
            {
                heard.TrySetResult(new JsonObject()); // a failing test mustn't leave the fake Helper holding a call
            }
        });

        Assert.True(holdingWhileTold);
        Assert.False(doneBeforeHeard);
        Assert.False(holdingAfter);
        Assert.Equal(new[] { copy }, ClosedPaths);
    }
}
