using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Pdf.Services;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
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

    private static IEnumerable<NativeMenuItem> AllItems(NativeMenu menu)
    {
        foreach (var item in menu.Items.OfType<NativeMenuItem>())
        {
            yield return item;
            if (item.Menu is { } submenu)
                foreach (var inner in AllItems(submenu))
                    yield return inner;
        }
    }

    /// <summary>
    /// A CommandParameter is text that the handler turns into an enum with Enum.Parse, so a typo in one would throw
    /// only when the item is clicked. This clicks them all, and fails if an item takes a parameter that none of the
    /// three handlers is checked for here.
    /// </summary>
    [PdfViewerFact]
    public async Task Every_menu_parameter_is_a_value_its_handler_accepts()
    {
        var path = Fixture();
        string[] viewModes = ["Single Page", "Continuous", "Two Pages", "Two Pages Continuous"];
        string[] sidebars = ["Hide Sidebar", "Thumbnails", "Table of Contents", "Bookmarks"];

        var (counts, withParameter, failures) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            try
            {
                var items = AllItems(NativeMenu.GetMenu(window)!).Where(i => i.CommandParameter is not null).ToList();
                var tools = AllItems(window.MenuItem("Annotate").Menu!).Where(i => i.CommandParameter is not null).ToList();
                var modes = items.Where(i => viewModes.Contains(i.Header)).ToList();
                var panels = items.Where(i => sidebars.Contains(i.Header)).ToList();
                var failures = new List<string>();

                // Each is first set to a different value, so a handler that did nothing would show.
                void Click<T>(NativeMenuItem item, Action<object?, EventArgs> handler, Func<T> get, Action<T> set, T other) where T : struct, Enum
                {
                    var expected = Enum.Parse<T>((string)item.CommandParameter!);
                    set(EqualityComparer<T>.Default.Equals(expected, other) ? default : other);
                    handler(item, EventArgs.Empty);
                    if (!EqualityComparer<T>.Default.Equals(get(), expected))
                        failures.Add($"{item.Header}: {get()}, not {expected}");
                }

                var viewer = window.Viewer;
                foreach (var item in tools)
                    Click(item, window.OnTool, () => viewer.ActiveTool, v => viewer.ActiveTool = v, PdfViewerTool.Highlight);
                foreach (var item in modes)
                    Click(item, window.OnViewMode, () => viewer.ViewMode, v => viewer.ViewMode = v, PdfViewMode.TwoPages);
                foreach (var item in panels)
                    Click(item, window.OnSidebar, () => viewer.SidebarMode, v => viewer.SidebarMode = v, SidebarMode.Bookmarks);
                return ((tools.Count, modes.Count, panels.Count), items.Count, failures);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Empty(failures);
        Assert.Equal(withParameter, counts.Item1 + counts.Item2 + counts.Item3);
        Assert.Equal(4, counts.Item2);
        Assert.Equal(4, counts.Item3);
        Assert.NotEqual(0, counts.Item1);
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
    public async Task Undo_in_a_focused_text_field_undoes_the_typing_not_the_markup()
    {
        // The owner's check 10: Edit › Undo's ⌘Z reached the menu first and undid a note while the search box had focus.
        var path = Fixture();

        var (typed, afterUndo, markupStillUndoable) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var search = window.Viewer.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible);
            search.Focus();
            window.KeyTextInput("fox");
            await TestWindows.WaitForAsync(() => search.Text == "fox");
            var before = search.Text;
            window.OnUndo(window.MenuItem("Undo"), EventArgs.Empty);
            return (before, search.Text, window.Viewer.CanUndo);
        });

        Assert.Equal("fox", typed);
        Assert.NotEqual("fox", afterUndo);
        Assert.True(markupStillUndoable);
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

    [PdfViewerFact]
    public async Task A_print_that_fails_is_reported_and_does_not_escape()
    {
        var path = Fixture();

        var errors = await Headless.RunAsync(async () =>
        {
            var (window, errors) = await TestWindows.OpenAsync(path);
            window.Viewer.PrintService = new FailingPrinter();
            await window.PrintAndReportAsync();
            return errors;
        });

        var error = Assert.Single(errors);
        Assert.Contains("“fixture.pdf” couldn’t be printed.", error);
        Assert.Contains("No printer", error);
    }

    [PdfViewerFact]
    public async Task A_failure_to_show_the_print_error_does_not_escape_either()
    {
        var path = Fixture();

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Viewer.PrintService = new FailingPrinter();
            // As when the window closed while the print dialog was up: there's nothing to show the error over.
            window.ReportError = (_, _) => Task.FromException(new InvalidOperationException("The window has closed"));
            await window.PrintAndReportAsync();
            return true;
        });
    }

    private sealed class FailingPrinter : IPrintService
    {
        public bool IsSupported => true;

        public Task PrintAsync(IPdfDocument document, PrintOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No printer");

        public Task ShowPrintPreviewAsync(IPdfDocument document, PrintOptions? options = null) => Task.CompletedTask;
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
