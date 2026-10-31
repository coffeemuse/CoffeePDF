using System.Text.Json.Nodes;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.4, §6.5 and §6.9: a copy's save, followed to Healdan, and Save as New Version.</summary>
[Trait("Category", "PdfViewer")]
public sealed class HealdanSaveTests : IDisposable
{
    private readonly FakeHelper _helper = new();

    public void Dispose() => _helper.Dispose();

    /// <summary>The copy's state with Healdan at <paramref name="version"/>.</summary>
    private CopyState Version(string copy, string version) =>
        _helper.StateOf(copy) with { Title = $"Settlement Agreement #1234 v{version}", DocRef = $"#1234 v{version}" };

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
        await window.CloseWithPromptAsync();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [PdfViewerFact]
    public async Task A_copys_save_is_followed_until_Healdan_has_it()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = 0;
        _helper.Answer(ChannelRoutes.Saved, async _ =>
        {
            if (Interlocked.Increment(ref asked) == 1)
            {
                await held.Task; // the first answer waits, so the test sees the save on its way
                return FakeHelper.Json(_helper.StateOf(copy) with { Pending = true });
            }
            return FakeHelper.Json(Version(copy, "2.2"));
        });

        var (during, after, pauses) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            var taken = new List<TimeSpan>();
            window.Pause = (pause, _) =>
            {
                taken.Add(pause);
                return Task.CompletedTask;
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            var onItsWay = window.Title;
            held.SetResult();
            await TestWindows.WaitForAsync(() => window.Title == "Settlement Agreement #1234 v2.2");
            var done = window.Title;
            await CloseAsync(window);
            return (onItsWay, done, taken);
        });

        Assert.Equal("Settlement Agreement #1234 v2.1 — Saving to Healdan…", during);
        Assert.Equal("Settlement Agreement #1234 v2.2", after);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, pauses);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
    }

    [PdfViewerFact]
    public async Task A_refused_save_makes_the_copy_read_only_and_offers_Save_as_New_Document()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        // As spec §6.9 has it: the Helper ends the check-out, and the copy's state says read-only (Phase 2's plan, question 3).
        _helper.Answer(ChannelRoutes.Saved, _ => Task.FromResult<JsonNode>(FakeHelper.Json(_helper.StateOf(copy) with { ReadOnly = true })));

        var (mode, readOnly, bar, action) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            await TestWindows.WaitForAsync(() => window.Document?.Mode == DocumentMode.ReadOnly);
            var state = (window.Document?.Mode, window.Viewer.IsReadOnly, window.BarText.Text, window.Document?.Bar?.Action);
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(DocumentMode.ReadOnly, mode);
        Assert.True(readOnly);
        Assert.Equal(WindowBar.Refused, bar);
        Assert.Equal(BarAction.SaveAsNewDocument, action);
    }

    [PdfViewerFact]
    public async Task With_the_Helper_away_a_save_stays_on_this_Mac_and_goes_once_it_answers()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (barWhileAway, pauses, title, barAfter, notes) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            _helper.GoAway();
            var taken = new List<TimeSpan>();
            string? shown = null;
            window.Pause = (pause, _) =>
            {
                taken.Add(pause);
                if (taken.Count == 2)
                {
                    shown = window.BarText.Text;
                    _helper.SetState(Version(copy, "2.2")); // the Helper, back, has sent the save
                    _helper.ComeBack();
                }
                return Task.CompletedTask;
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            await TestWindows.WaitForAsync(() => window.Title == "Settlement Agreement #1234 v2.2");
            var state = (shown, taken, window.Title, window.Bar.IsVisible, PdfAnnotations.Read(File.ReadAllBytes(copy), 0).Count);
            await CloseAsync(window);
            return state;
        });

        Assert.Equal(WindowBar.HelperAway, barWhileAway);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) }, pauses);
        Assert.Equal("Settlement Agreement #1234 v2.2", title);
        Assert.False(barAfter);
        Assert.Equal(1, notes);
    }

    [PdfViewerFact]
    public async Task Save_as_New_Version_asks_for_a_version_then_saves()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (saved, routes, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveAsNewVersionAsync();
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.Saved).Count == 1);
            await CloseAsync(window);
            return (result, _helper.Routes, errorList.ToList());
        });

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal(new[] { "state", "new-version", "saved", "closed" }, routes);
        Assert.Equal(new[] { copy }, _helper.CallsTo(ChannelRoutes.NewVersion).Select(b => (string?)b["path"]));
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
    }

    [PdfViewerFact]
    public async Task Save_as_New_Version_with_nothing_to_save_says_the_next_save_goes_as_the_version()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (asked, errors, title) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(copy, _helper.Link());
            var result = await window.SaveAsNewVersionAsync();
            var state = (result, errorList.ToList(), window.Title);
            await CloseAsync(window);
            return state;
        });

        Assert.True(asked);
        Assert.Equal("The next save goes to Healdan as a new version. There are no changes to save yet.", Assert.Single(errors));
        Assert.Equal("Settlement Agreement #1234 v2.1", title); // no save is on its way
        Assert.Equal(new[] { "state", "new-version", "closed" }, _helper.Routes);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(copy));
    }

    [PdfViewerFact]
    public async Task Save_as_New_Version_with_the_Helper_away_says_so_and_keeps_the_changes()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (saved, errors, unsaved, mode) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            _helper.GoAway();
            var result = await window.SaveAsNewVersionAsync();
            var state = (result, errorList.ToList(), window.HasUnsavedChanges, window.Document?.Mode);
            await CloseAsync(window);
            return state;
        });

        Assert.False(saved);
        Assert.Equal("“Settlement Agreement” couldn’t be saved as a new version. Healdan Helper isn’t running. Try again once it is.", Assert.Single(errors));
        Assert.True(unsaved);
        Assert.Equal(DocumentMode.HelperAway, mode);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(copy));
    }

    [PdfViewerFact]
    public async Task A_follow_that_stops_on_an_error_stops_saying_the_save_is_on_its_way()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        _helper.Answer(ChannelRoutes.Saved, FakeHelper.Json(_helper.StateOf(copy) with { Pending = true }));

        var (title, log) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            window.Pause = (_, _) => throw new InvalidOperationException("Not an error the Helper answered");
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            await TestWindows.WaitForAsync(() => window.Title == "Settlement Agreement #1234 v2.1");
            var state = (window.Title, File.ReadAllText(window.Healdan.Log.FilePath));
            await CloseAsync(window);
            return state;
        });

        Assert.Equal("Settlement Agreement #1234 v2.1", title);
        Assert.Contains("Following a save stopped (InvalidOperationException)", log);
        Assert.DoesNotContain("Not an error", log); // by its type: its message could quote a file
    }

    [PdfViewerFact]
    public async Task A_save_still_writing_as_the_window_closes_is_not_followed()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var (saved, title) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var held = TestWindows.HoldWrites(window);
            var saving = window.SaveAsync();
            try
            {
                await TestWindows.WaitForAsync(() => held.Reached);
                window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
                await window.CloseWithPromptAsync(); // the window lets the copy go once the save is written
            }
            finally
            {
                held.Release();
            }
            // The save ends here, and a follow would have said so in the title as it began.
            var state = (await saving, window.Title);
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return state;
        });

        Assert.True(saved);
        Assert.DoesNotContain("Saving to Healdan", title);
        Assert.Empty(_helper.CallsTo(ChannelRoutes.Saved));
    }

    [PdfViewerFact]
    public async Task Save_as_New_Version_saves_nothing_if_the_window_closes_while_the_Helper_answers()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _helper.Answer(ChannelRoutes.NewVersion, async _ =>
        {
            asked.SetResult();
            await answer.Task; // the Helper's answer waits, so the window closes before it comes
            return new JsonObject();
        });

        var saved = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(copy, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var asking = window.SaveAsNewVersionAsync();
            await asked.Task;
            await CloseAsync(window);
            answer.SetResult();
            return await asking;
        });

        Assert.False(saved);
        Assert.Equal(new[] { "state", "new-version", "closed" }, _helper.Routes);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(copy));
    }
}
