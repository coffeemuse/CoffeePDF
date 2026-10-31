using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Locks;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fakes;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.6: Save to Healdan for a local PDF, and Save as New Document for a copy, against the fake Helper.</summary>
[Trait("Category", "PdfViewer")]
public sealed class SaveToHealdanTests : IDisposable
{
    private readonly FakeHelper _helper = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public SaveToHealdanTests() =>
        // The Helper's Save window, filled in and saved.
        _helper.Answer(ChannelRoutes.SaveNew, new JsonObject
        {
            ["action"] = "save",
            ["folderId"] = 42,
            ["name"] = "Report",
            ["format"] = "pdf",
            ["properties"] = new JsonObject { ["Client"] = "Acme" },
            ["tempPath"] = Path.Combine(_helper.Folders.Temp, "save-new.pdf"),
        });

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        _helper.Dispose();
    }

    /// <summary>The copy the fake Helper files the document as.</summary>
    private string Filed => Path.Combine(_helper.Folders.Copies, "Report #1 v1.pdf");

    private string Local()
    {
        var path = Path.Combine(_dir, "Report.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    private static async Task CloseAsync(DocumentWindow window)
    {
        window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
        await window.CloseWithPromptAsync();
        await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static T? Visible<T>(Window window, Func<T, bool> match) where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.IsEffectivelyVisible && match(c));

    [PdfViewerFact]
    public async Task Save_to_Healdan_files_a_local_pdf_and_the_window_moves_to_its_copy()
    {
        var local = Local();

        var (saved, shown, mode, title, locked, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, window.FilePath, window.Document?.Mode, window.Title, File.Exists(CopyLock.PathFor(Filed)), window.HasUnsavedChanges);
            await CloseAsync(window);
            return state;
        });

        Assert.True(saved);
        Assert.Equal(Filed, shown);
        Assert.Equal(DocumentMode.Editable, mode);
        Assert.Equal("Report #1 v1", title);
        Assert.True(locked);
        Assert.False(unsaved);
        Assert.Equal(new[] { "save-new", "file", "adopted", "closed" }, _helper.Routes);
        var asked = Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew));
        Assert.Equal("Report", (string?)asked["name"]);
        Assert.Null(asked["path"]);
        var filing = Assert.Single(_helper.CallsTo(ChannelRoutes.File));
        Assert.Equal(42, (int)filing["folderId"]!);
        Assert.Equal("Acme", (string?)filing["properties"]!["Client"]);
        Assert.Null(filing["replaces"]);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(local));
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(Filed), 0)).Author);
    }

    [PdfViewerFact]
    public async Task Save_as_New_Document_files_a_copys_markups_and_lets_the_old_copy_go()
    {
        var old = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        bool? oldLocked = null, newLocked = null;
        _helper.Answer(ChannelRoutes.Closed, _ =>
        {
            if (oldLocked is null) // the old copy's, during the move
                (oldLocked, newLocked) = (File.Exists(CopyLock.PathFor(old)), File.Exists(CopyLock.PathFor(Filed)));
            return Task.FromResult<JsonNode>(new JsonObject());
        });

        var shown = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(old, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveNewAsync();
            var path = window.FilePath;
            await CloseAsync(window);
            return path;
        });

        Assert.Equal(Filed, shown);
        Assert.Equal(new[] { "state", "save-new", "file", "closed", "adopted", "closed" }, _helper.Routes);
        var asked = Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew));
        Assert.Equal(old, (string?)asked["path"]);
        Assert.Equal("Settlement Agreement", (string?)asked["name"]);
        Assert.Equal(old, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.File))["replaces"]);
        Assert.Equal(new[] { old, Filed }, _helper.CallsTo(ChannelRoutes.Closed).Select(b => (string?)b["path"]));
        Assert.False(oldLocked);
        Assert.True(newLocked);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(old));
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(Filed), 0)).Author);
    }

    [PdfViewerFact]
    public async Task The_Helpers_way_back_shows_the_Macs_Save_panel()
    {
        _helper.Answer(ChannelRoutes.SaveNew, new JsonObject { ["action"] = "local" });
        var local = Local();
        var elsewhere = Path.Combine(_dir, "Elsewhere.pdf");

        var (saved, suggested, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            string? name = null;
            window.PickSaveTarget = start =>
            {
                name = start;
                return Task.FromResult<string?>(elsewhere);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, name, window.FilePath);
            await CloseAsync(window);
            return state;
        });

        Assert.True(saved);
        Assert.Equal("Report.pdf", suggested);
        Assert.Equal(elsewhere, shown);
        Assert.Equal(new[] { "save-new" }, _helper.Routes);
    }

    [PdfViewerFact]
    public async Task Cancel_leaves_the_window_and_its_changes()
    {
        _helper.Answer(ChannelRoutes.SaveNew, new JsonObject { ["action"] = "cancel" });
        var local = Local();

        var (saved, panel, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, shownPanel, window.HasUnsavedChanges);
            await CloseAsync(window);
            return state;
        });

        Assert.False(saved);
        Assert.False(panel);
        Assert.True(unsaved);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(local));
    }

    [PdfViewerFact]
    public async Task With_the_Helper_away_its_the_Macs_Save_panel_with_nothing_to_say()
    {
        _helper.GoAway();
        var local = Local();

        var (panel, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.SaveNewAsync();
            var state = (shownPanel, errorList.ToList());
            await CloseAsync(window);
            return state;
        });

        Assert.True(panel);
        Assert.Empty(errors);
    }

    [PdfViewerFact]
    public async Task An_error_from_the_Helper_is_shown_then_the_Macs_Save_panel()
    {
        _helper.Fail(ChannelRoutes.SaveNew, 500, "internal-error");
        var local = Local();

        var (panel, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.SaveNewAsync();
            var state = (shownPanel, errorList.ToList());
            await CloseAsync(window);
            return state;
        });

        Assert.True(panel);
        Assert.Equal("Healdan Helper couldn’t show its Save window. Healdan Helper answered 500 (internal-error)", Assert.Single(errors));
    }

    [PdfViewerFact]
    public async Task A_filing_that_fails_keeps_the_window_on_its_file_with_its_changes()
    {
        _helper.Fail(ChannelRoutes.File, 500, "internal-error");
        var local = Local();

        var (saved, shown, unsaved, panel) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, window.FilePath, window.HasUnsavedChanges, shownPanel);
            await CloseAsync(window);
            return state;
        });

        Assert.False(saved);
        Assert.Equal(local, shown);
        Assert.True(unsaved);
        Assert.True(panel);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(local));
    }

    [PdfViewerFact]
    public async Task The_read_only_bar_offers_Save_as_New_Document()
    {
        var old = _helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true, holdBy: "Zoë Q. Reviewer");

        var (label, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(old, _helper.Link());
            var button = window.BarButton.IsVisible ? window.BarButton.Content as string : null;
            window.BarButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // The window has the new copy's path as soon as the move begins, and takes input again once it's done.
            await TestWindows.WaitForAsync(() => window.FilePath == Filed && window.DocumentPanel.IsEnabled);
            var path = window.FilePath;
            await CloseAsync(window);
            return (button, path);
        });

        Assert.Equal("Save as New Document…", label);
        Assert.Equal(Filed, shown);
        Assert.Equal(old, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.SaveNew))["path"]);
    }

    [PdfViewerFact]
    public async Task A_close_during_a_failed_filing_asks_once_and_the_next_save_to_Healdan_doesnt_close_the_window()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var filings = 0;
        _helper.Answer(ChannelRoutes.File, async body =>
        {
            if (Interlocked.Increment(ref filings) == 1)
            {
                await gate.Task; // the first filing waits, so the close comes during it, and then fails
                throw new InvalidOperationException();
            }
            var temp = (string)body["tempPath"]!; // later ones file as the fake does
            var path = _helper.AddCopy("Report", 1, "1", pdf: File.ReadAllBytes(temp));
            File.Delete(temp);
            return FakeHelper.Json(_helper.StateOf(path));
        });
        var local = Local();

        var (first, asked, panel, stillOpen, second, shown, openAfterSecond) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            var (questions, shownPanel) = (0, false);
            window.AskToSave = () =>
            {
                questions++;
                return Task.FromResult(SaveChoice.Cancel);
            };
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var flow = window.SaveNewAsync();
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.File).Count == 1);
            window.Close(); // during the filing: it waits until the filing is over
            gate.SetResult();
            var firstResult = await flow;
            var (stayed, asks, panelShown) = (window.IsVisible, questions, shownPanel);
            var secondResult = await window.SaveNewAsync();
            var state = (firstResult, asks, panelShown, stayed, secondResult, window.FilePath, window.IsVisible);
            await CloseAsync(window);
            return state;
        });

        Assert.False(first);
        Assert.Equal(1, asked); // the deferred close asks, since the window has changes its file lacks
        Assert.False(panel);    // and the Mac's Save panel isn't shown over that question
        Assert.True(stillOpen); // Cancel left the window open
        Assert.True(second);
        Assert.Equal(Filed, shown);
        Assert.True(openAfterSecond); // the close was used up: a later save doesn't close the window
    }

    [PdfViewerFact]
    public async Task A_close_during_a_refused_filing_closes_the_window_with_no_Save_panel_after_it()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = _helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true, holdBy: "Zoë Q. Reviewer");
        _helper.Answer(ChannelRoutes.File, async _ =>
        {
            await gate.Task;
            throw new InvalidOperationException();
        });

        var (saved, panel, open) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(old, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            var flow = window.SaveNewAsync();
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.File).Count == 1);
            window.Close();
            gate.SetResult();
            var result = await flow;
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return (result, shownPanel, window.IsVisible);
        });

        Assert.False(saved);
        Assert.False(panel);
        Assert.False(open);
        Assert.Equal(old, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.Closed))["path"]); // told once, by the close
    }

    [PdfViewerFact]
    public async Task A_window_closed_while_the_Helpers_Save_window_was_open_shows_no_Save_panel_when_the_Helper_fails()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _helper.Answer(ChannelRoutes.SaveNew, async _ =>
        {
            await gate.Task;
            throw new InvalidOperationException();
        });
        var local = Local();

        var (saved, panel) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            var flow = window.SaveNewAsync();
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.SaveNew).Count == 1);
            window.Close();
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            gate.SetResult();
            return (await flow, shownPanel);
        });

        Assert.False(saved);
        Assert.False(panel);
    }

    [PdfViewerFact]
    public async Task An_encrypted_pdf_keeps_its_password_across_the_move_so_the_new_copys_markups_get_the_author()
    {
        var local = Path.Combine(_dir, "Report.pdf");
        File.WriteAllBytes(local, EncryptedPdf.Build());

        var (saved, asked, resaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = TestWindows.Show();
            window.Healdan = _helper.Link();
            window.Load(local);
            TextBox? box = null;
            await TestWindows.WaitForAsync(() => (box = Visible<TextBox>(window, b => b.Name == "PART_PasswordTextBox")) is not null);
            box!.Text = EncryptedPdf.UserPassword;
            Visible<Button>(window, b => Equals(b.Content, "Unlock"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await TestWindows.WaitForAsync(() => window.Viewer.HasDocument);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            // The PdfViewer's prompt would hold the move for ever, so it's limited: the test fails instead of hanging.
            var moved = await window.SaveNewAsync().WaitAsync(TimeSpan.FromSeconds(15));
            var prompted = Visible<TextBox>(window, b => b.Name == "PART_PasswordTextBox") is not null;
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "After the move");
            var second = await window.SaveAsync();
            await CloseAsync(window);
            return (moved, prompted, second);
        });

        Assert.True(saved);
        Assert.False(asked); // the filed copy opened without the PdfViewer's password prompt
        Assert.True(resaved);
        var notes = PdfAnnotations.Read(File.ReadAllBytes(Filed), 0, EncryptedPdf.UserPassword);
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(TestWindows.Author, n.Author));
    }

    [PdfViewerFact]
    public async Task A_close_during_a_move_that_waits_on_a_password_prompt_ends_the_wait_and_closes_the_window()
    {
        // The Helper files an encrypted copy of a plain PDF, so the window has no password for it and the PdfViewer asks.
        _helper.Answer(ChannelRoutes.File, body =>
        {
            File.Delete((string)body["tempPath"]!);
            var path = _helper.AddCopy("Report", 1, "1", pdf: EncryptedPdf.Build());
            return Task.FromResult<JsonNode>(FakeHelper.Json(_helper.StateOf(path)));
        });
        var local = Local();

        var (filed, asking, open) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            var flow = window.SaveNewAsync();
            await TestWindows.WaitForAsync(() => Visible<TextBox>(window, b => b.Name == "PART_PasswordTextBox") is not null);
            var waiting = !window.DocumentPanel.IsEnabled;
            window.Close();
            var result = await flow.WaitAsync(TimeSpan.FromSeconds(10));
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return (result, waiting, window.IsVisible);
        });

        Assert.True(filed);
        Assert.True(asking); // the move was waiting on the prompt, with the window taking no input
        Assert.False(open);
        Assert.Equal(new[] { "save-new", "file", "closed" }, _helper.Routes);
        Assert.Equal(Filed, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.Closed))["path"]);
    }

    [PdfViewerFact]
    public async Task A_filing_the_Helper_doesnt_answer_may_have_gone_through_so_no_Save_panel_and_the_file_stays()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        _helper.Answer(ChannelRoutes.File, async _ =>
        {
            await gate.Task; // not answered within the limit below
            return new JsonObject();
        });
        var waits = new Dictionary<ChannelWait, TimeSpan>(HelperChannel.DefaultWaits) { [ChannelWait.Filing] = TimeSpan.FromMilliseconds(300) };
        var temp = Path.Combine(_helper.Folders.Temp, "save-new.pdf");

        var (saved, shown, unsaved, mode, panel, kept, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(old, _helper.Link(waits: waits));
            var shownPanel = false;
            window.PickSaveTarget = _ =>
            {
                shownPanel = true;
                return Task.FromResult<string?>(null);
            };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var result = await window.SaveNewAsync();
            var state = (result, window.FilePath, window.HasUnsavedChanges, window.Document?.Mode, shownPanel, File.Exists(temp), errorList.ToList());
            await CloseAsync(window);
            gate.SetResult();
            return state;
        });

        Assert.False(saved);
        Assert.Equal(old, shown);
        Assert.True(unsaved);
        Assert.Equal(DocumentMode.HelperAway, mode);
        Assert.False(panel);
        Assert.True(kept); // the Helper may have filed it, and deletes it itself
        Assert.Equal("Healdan Helper didn’t answer while filing the document. It may be in Healdan already. Check there before you save it again.",
            Assert.Single(errors));
        Assert.Equal(old, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.Closed))["path"]);
    }

    [PdfViewerFact]
    public async Task A_window_closed_while_its_copy_is_written_for_Healdan_files_nothing_and_deletes_the_copy()
    {
        var local = Local();
        var temp = Path.Combine(_helper.Folders.Temp, "save-new.pdf");

        var (saved, left, log) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var held = TestWindows.HoldWrites(window);
            var flow = window.SaveNewAsync();
            try
            {
                await TestWindows.WaitForAsync(() => held.Reached);
                window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
                await window.CloseWithPromptAsync(); // Don't Save, while the copy is being written
            }
            finally
            {
                held.Release();
            }
            var result = await flow;
            await window.Released.WaitAsync(TimeSpan.FromSeconds(10));
            return (result, File.Exists(temp), File.ReadAllText(window.Healdan.Log.FilePath));
        });

        Assert.False(saved);
        Assert.False(left); // the Helper was never handed it, so the window deletes it
        Assert.Equal(new[] { "save-new" }, _helper.Routes);
        Assert.Contains("Save to Healdan stopped: the window closed", log);
    }

    [PdfViewerFact]
    public async Task A_save_waiting_behind_the_copy_written_for_Healdan_leaves_the_window_on_the_new_copy()
    {
        var local = Local();

        var (saved, shown, locked) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(local, _helper.Link());
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            // The copy written for Healdan is held, so a ⌘S waits behind it; a write after that one is held until the
            // window has moved, as a slow one would be.
            var (copyWritten, laterWritten) = (new ManualResetEventSlim(), new ManualResetEventSlim());
            var writes = 0;
            window.WriteFile = (target, pdf) =>
            {
                (Interlocked.Increment(ref writes) == 1 ? copyWritten : laterWritten).Wait();
                AtomicFile.Write(target, pdf);
            };
            Task<bool> saving;
            try
            {
                var flow = window.SaveNewAsync();
                await TestWindows.WaitForAsync(() => Volatile.Read(ref writes) == 1);
                saving = window.SaveAsync(); // ⌘S, waiting behind the copy's write
                // The copy's save ends by carrying Save to Healdan on into the filing, so the window is moving by the
                // time the ⌘S, whose turn comes on a later dispatcher job, resumes.
                copyWritten.Set();
                Assert.True(await flow.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                copyWritten.Set();
                laterWritten.Set();
            }
            var state = (await saving, window.FilePath, File.Exists(CopyLock.PathFor(Filed)));
            await CloseAsync(window);
            return state;
        });

        Assert.False(saved);
        Assert.Equal(Filed, shown);
        Assert.True(locked);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(local));
        Assert.Equal(new[] { "save-new", "file", "adopted", "closed" }, _helper.Routes);
        Assert.Equal(Filed, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.Closed))["path"]);
    }

    [PdfViewerFact]
    public async Task A_window_moving_to_the_new_copy_opens_nothing_and_shows_no_Save_panel()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _helper.Answer(ChannelRoutes.File, async body =>
        {
            await gate.Task; // the filing waits, so the window is moving while the commands below come
            var temp = (string)body["tempPath"]!;
            var path = _helper.AddCopy("Report", 1, "1", pdf: File.ReadAllBytes(temp));
            File.Delete(temp);
            return FakeHelper.Json(_helper.StateOf(path));
        });
        var local = Local();

        var (panels, saveAs, filed) = await Headless.RunAsync(async () =>
        {
            var link = _helper.Link();
            var (window, _) = await TestWindows.OpenAsync(local, link);
            var (open, save) = (0, 0);
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
            var flow = window.SaveNewAsync();
            await TestWindows.WaitForAsync(() => _helper.CallsTo(ChannelRoutes.File).Count == 1);
            await window.OpenAsync(); // support on: not even the Helper's Open window
            link.Settings.HealdanEnabled = false; // support off: the Mac's panels would be next
            await window.OpenAsync();
            var asked = await window.SaveAsAsync();
            var shown = open + save;
            link.Settings.HealdanEnabled = true;
            gate.SetResult();
            var result = await flow;
            await CloseAsync(window);
            return (shown, asked, result);
        });

        Assert.Equal(0, panels);
        Assert.False(saveAs);
        Assert.True(filed);
        Assert.Empty(_helper.CallsTo(ChannelRoutes.OpenDialog));
    }
}
