using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Tests.Windows;
using CoffeePdf.Toolbar;
using static CoffeePdf.Tests.Toolbar.ToolbarTesting;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>The toolbar spec's §3 to §5: CoffeePDF's own toolbar, in a real window with a real PDF.</summary>
[Trait("Category", "PdfViewer")]
public sealed class DocumentToolbarTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Fixture(int pages = 1)
    {
        var path = Path.Combine(_dir, "fixture.pdf");
        File.WriteAllBytes(path, pages == 1 ? MinimalPdf.Build() : PagesPdf.Build(pages));
        return path;
    }

    [PdfViewerFact]
    public async Task The_viewers_own_toolbar_is_hidden_and_ours_is_in_the_document_panel()
    {
        var path = Fixture();

        var (builtInShown, inPanel, oursShown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            return (window.Viewer.IsToolbarVisible, window.Toolbar.GetVisualAncestors().Contains(window.DocumentPanel), window.Toolbar.IsEffectivelyVisible);
        });

        Assert.False(builtInShown);
        Assert.True(inPanel); // so a move to Healdan's new copy blocks it with the panel (spec §5)
        Assert.True(oursShown);
    }

    [PdfViewerFact]
    public async Task Before_a_document_loads_only_the_sidebar_button_is_available()
    {
        var available = await Headless.RunAsync(() =>
        {
            var (window, _) = TestWindows.Show();
            var t = window.Toolbar;
            var states = (t.SidebarButton.IsEnabled, t.PageBox.IsEnabled, t.ZoomBox.IsEnabled, t.SearchBox.IsEnabled, t.PrintButton.IsEnabled);
            window.Close();
            return Task.FromResult(states);
        });

        Assert.Equal((true, false, false, false, false), available);
    }

    [PdfViewerFact]
    public async Task The_page_box_goes_to_a_typed_page_and_puts_back_anything_else()
    {
        var path = Fixture(pages: 3);

        var (count, page, afterBad, afterTooFar) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            Type(window, t.PageBox, "3");
            await TestWindows.WaitForAsync(() => window.Viewer.CurrentPage == 3);
            var page = window.Viewer.CurrentPage;
            Type(window, t.PageBox, "two");
            var afterBad = t.PageBox.Text;
            Type(window, t.PageBox, "9");
            return (t.PageCountText.Text, page, afterBad, t.PageBox.Text);
        });

        Assert.Equal("of 3", count);
        Assert.Equal(3, page);
        Assert.Equal("3", afterBad);
        Assert.Equal("3", afterTooFar);
    }

    [PdfViewerFact]
    public async Task The_zoom_box_sets_a_typed_zoom_and_puts_back_anything_else()
    {
        var path = Fixture();

        var (level, mode, afterBad) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            Type(window, t.ZoomBox, "150%");
            var level = window.Viewer.ZoomLevel;
            var mode = window.Viewer.ZoomMode;
            Type(window, t.ZoomBox, "big");
            return (level, mode, t.ZoomBox.Text);
        });

        Assert.Equal(1.5, level, precision: 6);
        Assert.Equal(PdfZoomMode.Manual, mode);
        Assert.Equal("150%", afterBad);
    }

    [PdfViewerFact]
    public async Task Leaving_the_zoom_box_without_typing_keeps_the_fit_mode()
    {
        var path = Fixture();

        var (mode, before, after) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Viewer.FitWidth();
            await TestWindows.WaitForAsync(() => window.Viewer.ZoomMode == PdfZoomMode.FitWidth);
            var before = window.Viewer.ZoomLevel;
            window.Toolbar.ZoomBox.Focus();
            Dispatcher.UIThread.RunJobs();
            window.Viewer.FocusViewer();
            Dispatcher.UIThread.RunJobs();
            return (window.Viewer.ZoomMode, before, window.Viewer.ZoomLevel);
        });

        Assert.Equal(PdfZoomMode.FitWidth, mode);
        Assert.Equal(before, after);
    }

    [PdfViewerFact]
    public async Task Escape_in_the_zoom_box_puts_back_the_zoom_and_keeps_the_fit_mode()
    {
        var path = Fixture();

        var (mode, before, after, text, expected) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Viewer.FitWidth();
            await TestWindows.WaitForAsync(() => window.Viewer.ZoomMode == PdfZoomMode.FitWidth);
            var before = window.Viewer.ZoomLevel;
            Type(window, window.Toolbar.ZoomBox, "300%", enter: false);
            Press(window, Key.Escape);
            return (window.Viewer.ZoomMode, before, window.Viewer.ZoomLevel, window.Toolbar.ZoomBox.Text, ToolbarRules.ZoomText(window.Viewer.ZoomLevel));
        });

        Assert.Equal(PdfZoomMode.FitWidth, mode);
        Assert.Equal(before, after);
        Assert.Equal(expected, text);
    }

    [PdfViewerFact]
    public async Task Leaving_the_page_box_without_typing_does_not_move_the_view()
    {
        var path = Fixture(pages: 3);

        var (pageBefore, pageAfter, scrollBefore, scrollAfter) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var scroller = window.Viewer.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PART_PagesContainer")
                .GetVisualAncestors().OfType<ScrollViewer>().First();
            var (pageBefore, scrollBefore) = (window.Viewer.CurrentPage, scroller.Offset.Y);
            window.Toolbar.PageBox.Focus();
            Dispatcher.UIThread.RunJobs();
            window.Viewer.FocusViewer();
            Dispatcher.UIThread.RunJobs();
            return (pageBefore, window.Viewer.CurrentPage, scrollBefore, scroller.Offset.Y);
        });

        Assert.Equal(pageBefore, pageAfter);
        Assert.Equal(scrollBefore, scrollAfter);
    }

    [PdfViewerFact]
    public async Task Return_in_the_page_box_and_the_zoom_box_gives_focus_back_to_the_viewer()
    {
        var path = Fixture(pages: 3);

        var (pageFocused, zoomFocused) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            Type(window, t.PageBox, "2");
            var pageFocused = t.PageBox.IsKeyboardFocusWithin;
            Type(window, t.ZoomBox, "150%");
            return (pageFocused, t.ZoomBox.IsKeyboardFocusWithin);
        });

        Assert.False(pageFocused);
        Assert.False(zoomFocused);
    }

    [PdfViewerFact]
    public async Task The_zoom_menu_has_the_fit_modes_and_the_presets()
    {
        var path = Fixture();

        var (headers, mode) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var items = window.Toolbar.ZoomMenu.Items.OfType<MenuItem>().ToList();
            items.Single(i => (string?)i.Header == "Fit Width").Command!.Execute(null);
            return (items.Select(i => (string?)i.Header).ToList(), window.Viewer.ZoomMode);
        });

        Assert.Equal(new[] { "Zoom In", "Zoom Out", "Actual Size", "Zoom to Fit", "Fit Width", "50%", "75%", "100%", "125%", "150%", "200%", "400%" }, headers);
        Assert.Equal(PdfZoomMode.FitWidth, mode);
    }

    [PdfViewerFact]
    public async Task The_sidebar_button_hides_the_sidebar_and_brings_back_its_tab()
    {
        var path = Fixture();

        var modes = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Viewer.SidebarMode = SidebarMode.TableOfContents;
            Click(window, window.Toolbar.SidebarButton);
            var hidden = window.Viewer.SidebarMode;
            Click(window, window.Toolbar.SidebarButton);
            return (hidden, window.Viewer.SidebarMode, window.Toolbar.SidebarButton.IsChecked);
        });

        Assert.Equal((SidebarMode.None, SidebarMode.TableOfContents, (bool?)true), modes);
    }

    [PdfViewerFact]
    public async Task Search_counts_its_matches_and_Escape_clears_it()
    {
        var path = Fixture();

        var (shown, count, afterEscape, query) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            Type(window, t.SearchBox, "fixture", enter: false);
            await TestWindows.WaitForAsync(() => window.Viewer.SearchResultCount == 1 && t.SearchCount.Text == "1 of 1");
            var (shown, count) = (t.SearchResults.IsVisible, t.SearchCount.Text);
            Press(window, Key.Escape);
            return (shown, count, t.SearchBox.Text, window.Viewer.SearchQuery);
        });

        Assert.True(shown);
        Assert.Equal("1 of 1", count);
        Assert.True(string.IsNullOrEmpty(afterEscape));
        Assert.True(string.IsNullOrEmpty(query));
    }

    [PdfViewerFact]
    public async Task The_search_options_set_the_viewers_search()
    {
        var path = Fixture();

        var (matchCase, wholeWord) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Toolbar.MatchCaseBox.IsChecked = true;
            window.Toolbar.WholeWordBox.IsChecked = true;
            return (window.Viewer.SearchMatchCase, window.Viewer.SearchMatchWholeWord);
        });

        Assert.True(matchCase);
        Assert.True(wholeWord);
    }

    [PdfViewerFact]
    public async Task Edit_Find_puts_the_cursor_in_the_search_box()
    {
        var path = Fixture();

        var focused = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.OnFind(window.MenuItem("Find…"), EventArgs.Empty);
            return window.FocusManager?.GetFocusedElement() == window.Toolbar.SearchBox;
        });

        Assert.True(focused);
    }

    [PdfViewerFact]
    public async Task Print_on_the_toolbar_goes_through_the_window_and_reports_a_failure()
    {
        var path = Fixture();

        var errors = await Headless.RunAsync(async () =>
        {
            var (window, errors) = await TestWindows.OpenAsync(path);
            window.Viewer.PrintService = new FailingPrinter();
            Click(window, window.Toolbar.PrintButton);
            await TestWindows.WaitForAsync(() => errors.Count == 1);
            return errors;
        });

        Assert.Contains("“fixture.pdf” couldn’t be printed.", Assert.Single(errors));
    }
}
