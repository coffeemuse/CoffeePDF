using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CoffeePdf.Core.Documents;
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
}
