using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Pdf.Services;
using Avalonia.Input;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §2.1: the PdfViewer's viewing, markup and printing, from a Mac menu bar with Preview's shortcuts.</summary>
[Trait("Category", "PdfViewer")]
public sealed class MenuTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Fixture()
    {
        var path = Path.Combine(_dir, "fixture.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    private static readonly (string Header, string Gesture)[] Shortcuts =
    [
        ("Open…", "Meta+O"), ("Close", "Meta+W"), ("Save", "Meta+S"), ("Save As…", "Meta+Shift+S"), ("Print…", "Meta+P"),
        ("Undo", "Meta+Z"), ("Redo", "Meta+Shift+Z"), ("Find Next", "Meta+G"), ("Find Previous", "Meta+Shift+G"),
        ("Actual Size", "Meta+D0"), ("Zoom to Fit", "Meta+D9"), ("Zoom In", "Meta+OemPlus"), ("Zoom Out", "Meta+OemMinus"),
        ("Hide Sidebar", "Alt+Meta+D1"), ("Thumbnails", "Alt+Meta+D2"), ("Table of Contents", "Alt+Meta+D3"),
        ("Bookmarks", "Alt+Meta+D4"), ("Previous Page", "Alt+Meta+Up"), ("Next Page", "Alt+Meta+Down"),
        ("Toggle Bookmark", "Meta+D"), ("Highlight", "Ctrl+Meta+H"), ("Underline", "Ctrl+Meta+U"),
        ("Strikethrough", "Ctrl+Meta+S"), ("Text", "Ctrl+Meta+T"), ("Note", "Ctrl+Meta+N"),
    ];

    [PdfViewerFact]
    public async Task Each_menu_item_has_Previews_shortcut()
    {
        var wrong = await Headless.RunAsync(() =>
        {
            var window = new DocumentWindow();
            var mismatched = Shortcuts.Where(s => !Equals(window.MenuItem(s.Header).Gesture, KeyGesture.Parse(s.Gesture)))
                .Select(s => s.Header).ToList();
            window.Close();
            return Task.FromResult(mismatched);
        });

        Assert.Empty(wrong);
    }

    [PdfViewerFact]
    public async Task The_tool_view_and_sidebar_items_drive_the_PdfViewer()
    {
        // With a document open: the PdfViewer keeps its annotation tools off until there's one (its 12.0.1 changelog).
        var path = Fixture();

        var (tool, view, sidebar) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.OnTool(window.MenuItem("Highlight"), EventArgs.Empty);
            window.OnViewMode(window.MenuItem("Two Pages"), EventArgs.Empty);
            window.OnSidebar(window.MenuItem("Table of Contents"), EventArgs.Empty);
            return (window.Viewer.ActiveTool, window.Viewer.ViewMode, window.Viewer.SidebarMode);
        });

        Assert.Equal(PdfViewerTool.Highlight, tool);
        Assert.Equal(PdfViewMode.TwoPages, view);
        Assert.Equal(SidebarMode.TableOfContents, sidebar);
    }

    [PdfViewerFact]
    public async Task Undo_and_Redo_follow_the_PdfViewer()
    {
        var path = Fixture();

        var states = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var seen = new List<(bool Undo, bool Redo)> { (window.MenuItem("Undo").IsEnabled, window.MenuItem("Redo").IsEnabled) };
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            seen.Add((window.MenuItem("Undo").IsEnabled, window.MenuItem("Redo").IsEnabled));
            window.OnUndo(window.MenuItem("Undo"), EventArgs.Empty);
            await TestWindows.WaitForAsync(() => window.MenuItem("Redo").IsEnabled);
            seen.Add((window.MenuItem("Undo").IsEnabled, window.MenuItem("Redo").IsEnabled));
            return seen;
        });

        Assert.Equal(new[] { (false, false), (true, false), (false, true) }, states);
    }

    [PdfViewerFact]
    public async Task Zoom_In_zooms_in()
    {
        var path = Fixture();

        var (before, after) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var zoom = window.Viewer.ZoomLevel;
            window.OnZoomIn(window.MenuItem("Zoom In"), EventArgs.Empty);
            return (zoom, window.Viewer.ZoomLevel);
        });

        Assert.True(after > before);
    }

    [PdfViewerFact]
    public async Task Print_sends_the_document_to_the_print_service_under_its_name()
    {
        var path = Fixture();
        var printer = new FakePrinter();

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Viewer.PrintService = printer;
            await window.PrintAsync();
            return true;
        });

        Assert.Equal("fixture.pdf", Assert.Single(printer.Titles));
    }

    private sealed class FakePrinter : IPrintService
    {
        public List<string?> Titles { get; } = [];
        public bool IsSupported => true;

        public Task PrintAsync(IPdfDocument document, PrintOptions? options = null, CancellationToken cancellationToken = default)
        {
            Titles.Add(options?.Title);
            return Task.CompletedTask;
        }

        public Task ShowPrintPreviewAsync(IPdfDocument document, PrintOptions? options = null) => Task.CompletedTask;
    }
}
