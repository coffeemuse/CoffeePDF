using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
            await FocusAsync(window.Toolbar.ZoomBox);
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

    /// <summary>
    /// Puts the focus in <paramref name="box"/> and waits until it stays there. The viewer takes the focus back in a job
    /// of its own after a fit mode is chosen (as the headless run shows), so a focus given before that job runs is lost.
    /// </summary>
    private static Task FocusAsync(TextBox box) => TestWindows.WaitForAsync(() =>
    {
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        return box.IsKeyboardFocusWithin;
    });

    [PdfViewerFact]
    public async Task The_page_box_follows_the_viewer_while_focused_and_leaving_it_unedited_keeps_the_page()
    {
        // The final review's I1: a focused box didn't follow the viewer, and leaving it committed its stale page, so a
        // scroll while the box had the focus jumped back when the focus left.
        var path = Fixture(pages: 3);

        var (focused, shown, pageAfter, textAfter, scrollBefore, scrollAfter) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var (v, t) = (window.Viewer, window.Toolbar);
            await TestWindows.WaitForAsync(() => HasPage(v));
            var scroller = v.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PART_PagesContainer")
                .GetVisualAncestors().OfType<ScrollViewer>().First();
            await FocusAsync(t.PageBox);
            v.GoToPage(3); // as a scroll would move it
            await TestWindows.WaitForAsync(() => v.CurrentPage == 3);
            var (focused, shown, scrollBefore) = (t.PageBox.IsKeyboardFocusWithin, t.PageBox.Text, scroller.Offset.Y);
            v.FocusViewer();
            Dispatcher.UIThread.RunJobs();
            return (focused, shown, v.CurrentPage, t.PageBox.Text, scrollBefore, scroller.Offset.Y);
        });

        Assert.True(focused, "The page box lost the focus before the viewer moved");
        Assert.Equal("3", shown);
        Assert.Equal(3, pageAfter);
        Assert.Equal("3", textAfter);
        Assert.Equal(scrollBefore, scrollAfter);
    }

    [PdfViewerFact]
    public async Task The_zoom_box_follows_the_viewer_while_focused_and_leaving_it_unedited_keeps_the_zoom()
    {
        // I1 again, for the zoom box: ⌘+, a pinch, or a fit mode's re-fit while the box has the focus.
        var path = Fixture();

        var (focused, shown, level, textAfter) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var (v, t) = (window.Viewer, window.Toolbar);
            await TestWindows.WaitForAsync(() => HasPage(v));
            await FocusAsync(t.ZoomBox);
            v.ZoomLevel = 1.5;
            await TestWindows.WaitForAsync(() => Math.Abs(v.ZoomLevel - 1.5) < 1e-6);
            var (focused, shown) = (t.ZoomBox.IsKeyboardFocusWithin, t.ZoomBox.Text);
            v.FocusViewer();
            Dispatcher.UIThread.RunJobs();
            return (focused, shown, v.ZoomLevel, t.ZoomBox.Text);
        });

        Assert.True(focused, "The zoom box lost the focus before the zoom changed");
        Assert.Equal("150%", shown);
        Assert.Equal(1.5, level, precision: 6);
        Assert.Equal("150%", textAfter);
    }

    [PdfViewerFact]
    public async Task A_fit_modes_re_fit_while_the_zoom_box_is_focused_keeps_the_fit_mode()
    {
        var path = Fixture();

        var (focused, mode, refit, level, text, expected) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var (v, t) = (window.Viewer, window.Toolbar);
            await TestWindows.WaitForAsync(() => HasPage(v));
            v.FitWidth();
            await TestWindows.WaitForAsync(() => v.ZoomMode == PdfZoomMode.FitWidth);
            var before = v.ZoomLevel;
            await FocusAsync(t.ZoomBox);
            window.Width += 200; // the viewer re-fits the page to its new width
            await TestWindows.WaitForAsync(() => Math.Abs(v.ZoomLevel - before) > 0.01);
            var (focused, refit) = (t.ZoomBox.IsKeyboardFocusWithin, v.ZoomLevel);
            v.FocusViewer();
            Dispatcher.UIThread.RunJobs();
            return (focused, v.ZoomMode, refit, v.ZoomLevel, t.ZoomBox.Text, ToolbarRules.ZoomText(refit));
        });

        Assert.True(focused, "The zoom box lost the focus before the re-fit");
        Assert.Equal(PdfZoomMode.FitWidth, mode);
        Assert.Equal(refit, level);
        Assert.Equal(expected, text);
    }

    [PdfViewerFact]
    public async Task Escape_in_the_page_box_puts_back_the_page()
    {
        var path = Fixture(pages: 3);

        var (page, text) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            Type(window, window.Toolbar.PageBox, "2", enter: false);
            Press(window, Key.Escape);
            return (window.Viewer.CurrentPage, window.Toolbar.PageBox.Text);
        });

        Assert.Equal(1, page);
        Assert.Equal("1", text);
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

    private static readonly PdfViewerTool[] ToolsInOrder =
    [
        PdfViewerTool.Highlight, PdfViewerTool.Underline, PdfViewerTool.Strikeout, PdfViewerTool.Squiggly, PdfViewerTool.Draw,
        PdfViewerTool.Text, PdfViewerTool.Shape, PdfViewerTool.Note, PdfViewerTool.Link, PdfViewerTool.Stamp,
    ];

    private static bool ShowsArmed(Control button) => button switch
    {
        ToggleButton toggle => toggle.IsChecked == true,
        ToggleSplitButton split => split.IsChecked,
        _ => false,
    };

    [PdfViewerFact]
    public async Task Each_tool_button_arms_its_tool_and_a_second_click_disarms_it()
    {
        var path = Fixture();

        var wrong = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            // Wide enough that no tool overflows, even with the Text tool's three format menus showing.
            window.Width = 1400;
            await TestWindows.WaitForAsync(() => window.Toolbar.Bounds.Width >= 1400 && window.Toolbar.Tools.OverflowedChildren.Count == 0);
            var wrong = new List<string>();
            foreach (var tool in ToolsInOrder)
            {
                var button = window.Toolbar.ButtonFor(tool);
                Click(window, button, across: 0.3);
                if (window.Viewer.ActiveTool != tool || !ShowsArmed(button))
                    wrong.Add($"{tool}: armed {window.Viewer.ActiveTool}, shown {ShowsArmed(button)}");
                Click(window, button, across: 0.3);
                if (window.Viewer.ActiveTool != PdfViewerTool.None || ShowsArmed(button))
                    wrong.Add($"{tool} again: armed {window.Viewer.ActiveTool}, shown {ShowsArmed(button)}");
            }
            return wrong;
        });

        Assert.Empty(wrong);
    }

    [PdfViewerFact]
    public async Task The_tools_are_in_the_Tools_menus_order_and_follow_it()
    {
        var path = Fixture();

        var (order, noteShown, highlightShown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var order = window.Toolbar.Tools.Children.Where(c => c.Classes.Contains("tool")).Select(c => (string)c.Tag!).ToList();
            window.OnTool(window.MenuItem("Note"), EventArgs.Empty);
            return (order, ShowsArmed(window.Toolbar.ButtonFor(PdfViewerTool.Note)), ShowsArmed(window.Toolbar.ButtonFor(PdfViewerTool.Highlight)));
        });

        Assert.Equal(ToolsInOrder.Select(t => t.ToString()), order);
        Assert.True(noteShown);
        Assert.False(highlightShown);
    }

    [PdfViewerFact]
    public async Task Read_only_hides_the_tools_and_disarms_an_armed_one()
    {
        var path = Fixture();

        var (shown, tool) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Toolbar.Arm(PdfViewerTool.Highlight);
            window.Viewer.IsReadOnly = true; // as ApplyMode does for a read-only copy (v1 spec §6.1)
            await TestWindows.WaitForAsync(() => !window.Toolbar.Tools.IsVisible);
            return (window.Toolbar.Tools.IsVisible, window.Viewer.ActiveTool);
        });

        Assert.False(shown);
        Assert.Equal(PdfViewerTool.None, tool);
    }

    [PdfViewerFact]
    public async Task The_Shapes_menu_arms_the_Shape_tool_with_the_shape_chosen()
    {
        var path = Fixture();

        var (headers, shape, tool) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var items = window.Toolbar.ShapesMenu.Items.OfType<MenuItem>().ToList();
            items.Single(i => (string?)i.Header == "Star").Command!.Execute(null);
            return (items.Select(i => (string?)i.Header).ToList(), window.Viewer.SelectedShapeType, window.Viewer.ActiveTool);
        });

        Assert.Equal(["Line", "Arrow", "Rectangle", "Circle", "Polygon", "Star"], headers);
        Assert.Equal(ShapeType.Star, shape);
        Assert.Equal(PdfViewerTool.Shape, tool);
    }

    [PdfViewerFact]
    public async Task The_Stamp_menu_arms_the_Stamp_tool_with_the_label_chosen_first()
    {
        var path = Fixture();

        var (labels, chosen, offered, tool) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var offered = window.Toolbar.StampLabelList.Items.OfType<string>().ToList();
            window.Toolbar.StampLabelList.SelectedItem = offered[2];
            return (window.Viewer.StampLabels, offered[2], offered, window.Viewer.ActiveTool);
        });

        Assert.Equal(chosen, labels[0]);
        Assert.Equal(offered.Count, labels.Count);
        Assert.Equal(PdfViewerTool.Stamp, tool);
    }

    [PdfViewerFact]
    public async Task A_stamp_lands_with_the_label_chosen()
    {
        // Probe 3's answer, kept: nothing public chooses the label, so the toolbar puts it first in StampLabels.
        var path = Fixture();

        var (saved, name) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var label = window.Toolbar.StampLabelList.Items.OfType<string>().ElementAt(3);
            await TestWindows.WaitForAsync(() => HasPage(window.Viewer));
            window.Toolbar.ChooseStampLabel(label);
            var added = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Viewer.AnnotationAdded += (_, _) => added.TrySetResult();
            Click(window, OnPage(window.Viewer, window, 306, 400));
            await added.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var bytes = new MemoryStream();
            await window.Viewer.SaveDocumentAsync(bytes);
            return (System.Text.Encoding.Latin1.GetString(bytes.ToArray()), PdfViewer.StampNameForLabel(label));
        });

        Assert.Matches($@"/Name\s*/{name}\b", saved);
    }

    [PdfViewerFact]
    public async Task The_Stamp_menu_sets_the_style_and_the_date()
    {
        var path = Fixture();

        var (style, date, wasDate) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var wasDate = window.Viewer.IncludeStampDate;
            window.Toolbar.StampStyleBox.SelectedIndex = Array.IndexOf(Enum.GetValues<PdfStampStyle>(), PdfStampStyle.Legal);
            window.Toolbar.StampDateBox.IsChecked = !wasDate;
            return (window.Viewer.StampStyle, window.Viewer.IncludeStampDate, wasDate);
        });

        Assert.Equal(PdfStampStyle.Legal, style);
        Assert.Equal(!wasDate, date);
    }

    [PdfViewerFact]
    public async Task A_narrow_window_moves_tools_into_the_More_menu_and_they_still_arm()
    {
        var path = Fixture();

        var (overflowed, items, armed, expected) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Width = window.MinWidth;
            await TestWindows.WaitForAsync(() => window.Toolbar.Tools.OverflowedChildren.Count > 0 && window.Toolbar.MoreMenu.Items.Count > 0);
            var plain = window.Toolbar.MoreMenu.Items.OfType<MenuItem>().First(i => i.Command is not null);
            var expected = window.Toolbar.Tools.OverflowedChildren.First(c => (string?)AutomationProperties.GetName(c) == (string?)plain.Header);
            plain.Command!.Execute(null);
            return (window.Toolbar.Tools.OverflowedChildren.Count(c => c.Classes.Contains("tool")), window.Toolbar.MoreMenu.Items.Count,
                window.Viewer.ActiveTool.ToString(), (string)expected.Tag!);
        });

        Assert.Equal(overflowed, items);
        Assert.Equal(expected, armed);
    }

    /// <summary>
    /// Whether <paramref name="control"/> shows in the row: drawn, taking clicks, with it or one of its parts taking the
    /// keyboard focus, and read by VoiceOver.
    /// </summary>
    private static bool Shown(Control control) =>
        control.Opacity > 0 && control.IsHitTestVisible && control.Bounds is { Width: > 0, Height: > 0 } && CanFocus(control) && Read(control);

    /// <summary>Whether a screen reader reads <paramref name="control"/>: it's a control element of the accessibility tree.</summary>
    private static bool Read(Control control) => ControlAutomationPeer.CreatePeerForElement(control).IsControlElement();

    /// <summary>Whether <paramref name="control"/>, or a part of its template, can take the keyboard focus.</summary>
    private static bool CanFocus(Control control) =>
        control.GetVisualDescendants().Prepend(control).OfType<InputElement>().Any(e => e.Focusable && e.IsEffectivelyEnabled);

    /// <summary>Whether <paramref name="control"/> is out of the row: not drawn, taking no clicks or focus, and not read.</summary>
    private static bool Hidden(Control control) => control.Opacity == 0 && !control.IsHitTestVisible && !CanFocus(control) && !Read(control);

    /// <summary>
    /// Waits until the window's last drawn frame has <paramref name="control"/> where its layout puts it. A hit test asks
    /// the frame, which a resize leaves behind the layout until the next one is drawn; the right group moves with the
    /// window's width, so a click on its Print button landing there says the frame has caught up.
    /// </summary>
    private static Task DrawnAsync(Window window, Control control) =>
        TestWindows.WaitForAsync(() => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) is { } at
            && window.InputHitTest(at) is Visual hit && (hit == control || control.IsVisualAncestorOf(hit)));

    /// <summary>The tool, or the named control of the toolbar's own, that <paramref name="hit"/> is part of.</summary>
    private static string Describe(Visual? hit) =>
        hit?.GetVisualAncestors().Prepend(hit).OfType<Control>()
            .FirstOrDefault(c => c.Classes.Contains("tool") || c.Name is { } name && !name.StartsWith("PART_")) is { } c
            ? (string?)c.Tag ?? c.Name!
            : hit?.GetType().Name ?? "nothing";

    [PdfViewerFact]
    public async Task At_its_minimum_width_each_tool_shown_takes_its_own_clicks_and_the_overflowed_ones_are_hidden()
    {
        // The final review's C1: an overflowed child is arranged in an empty rect, and a split button keeps its minimum
        // height there, so overflowed Shapes and Stamp sat over Highlight's top half and took its clicks.
        var path = Fixture();

        var (wrong, armed) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            window.Width = window.MinWidth;
            await TestWindows.WaitForAsync(() => t.Tools.OverflowedChildren.Count(c => c.Classes.Contains("tool")) >= 2
                && t.MoreMenu.Items.Count > 0 && t.MoreButton.Bounds.Width > 0);
            await DrawnAsync(window, t.PrintButton);
            var overflowed = t.Tools.OverflowedChildren.ToHashSet();
            var wrong = new List<string>();
            foreach (var tool in ToolsInOrder)
            {
                var button = t.ButtonFor(tool);
                if (overflowed.Contains(button))
                {
                    if (!Hidden(button))
                        wrong.Add($"{tool} overflowed but not hidden: opacity {button.Opacity}, hit-testable {button.IsHitTestVisible}, focusable {CanFocus(button)}, read {Read(button)}");
                    continue;
                }
                if (!Shown(button))
                    wrong.Add($"{tool} on the row but not shown");
                foreach (var down in new[] { 0.1, 0.5 })
                {
                    var at = button.TranslatePoint(new Point(button.Bounds.Width * 0.3, button.Bounds.Height * down), window)!.Value;
                    if (window.InputHitTest(at) is not Visual hit || (hit != button && !button.IsVisualAncestorOf(hit)))
                        wrong.Add($"{tool} at {down} of its height hits {Describe(window.InputHitTest(at) as Visual)}");
                }
            }
            if (!Shown(t.MoreButton))
                wrong.Add("More isn't shown while tools overflow");
            var highlight = t.ButtonFor(PdfViewerTool.Highlight);
            Click(window, highlight.TranslatePoint(new Point(highlight.Bounds.Width / 2, highlight.Bounds.Height * 0.2), window)!.Value);
            return (wrong, window.Viewer.ActiveTool);
        });

        Assert.Empty(wrong);
        Assert.Equal(PdfViewerTool.Highlight, armed); // a click on Highlight's top arms Highlight, not an overflowed tool
    }

    [PdfViewerFact]
    public async Task Tools_that_overflowed_come_back_when_the_window_widens()
    {
        // The final review's C2: a panel centred in its column is only ever given its desired width, and once its More
        // button has shown it reserves room for it there, so Stamp never came back.
        var path = Fixture();

        var (overflowed, moreHidden, stampShown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            window.Width = window.MinWidth;
            await TestWindows.WaitForAsync(() => t.Tools.OverflowedChildren.Count > 0 && t.MoreMenu.Items.Count > 0);
            window.Width = 1400;
            await TestWindows.WaitForAsync(() => t.Bounds.Width >= 1400);
            // The panel tells which children overflowed after its arrange, in a job of its own, so a test that waited for
            // none would wait ten seconds before failing; let that job run instead, then look.
            Dispatcher.UIThread.RunJobs();
            await TestWindows.WaitForAsync(() => t.Tools.Bounds.Width > 0);
            Dispatcher.UIThread.RunJobs();
            return (t.Tools.OverflowedChildren.Select(c => (string?)c.Tag ?? c.Name).ToList(), Hidden(t.MoreButton),
                Shown(t.ButtonFor(PdfViewerTool.Stamp)));
        });

        Assert.Empty(overflowed);
        Assert.True(moreHidden, "More still shows with nothing overflowed");
        Assert.True(stampShown, "Stamp isn't back on the row");
    }

    [PdfViewerFact]
    public async Task The_tools_sit_in_the_middle_of_their_column()
    {
        // The panel takes its whole column (C2's fix), and centres the tools in it itself.
        var path = Fixture();

        var (before, after) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            window.Width = 1400;
            await TestWindows.WaitForAsync(() => t.Bounds.Width >= 1400 && t.Tools.OverflowedChildren.Count == 0);
            var (first, last) = (t.ButtonFor(PdfViewerTool.Highlight), t.ButtonFor(PdfViewerTool.Stamp));
            var start = first.TranslatePoint(default, t.Tools)!.Value.X;
            var end = last.TranslatePoint(new Point(last.Bounds.Width, 0), t.Tools)!.Value.X;
            return (start, t.Tools.Bounds.Width - end);
        });

        Assert.True(Math.Abs(before - after) <= 1, $"The tools start {before} in and end {after} short of their column's end");
    }

    [PdfViewerFact]
    public async Task With_nothing_overflowed_the_More_button_is_hidden()
    {
        var path = Fixture();

        var (overflowed, moreHidden) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Width = 1400;
            await TestWindows.WaitForAsync(() => window.Toolbar.Bounds.Width >= 1400);
            Dispatcher.UIThread.RunJobs();
            return (window.Toolbar.Tools.OverflowedChildren.Count, Hidden(window.Toolbar.MoreButton));
        });

        Assert.Equal(0, overflowed);
        Assert.True(moreHidden, "More shows, or takes the focus, with nothing overflowed");
    }

    [PdfViewerFact]
    public async Task At_its_minimum_width_everything_but_the_tools_fits()
    {
        var path = Fixture();

        var (leftEnd, toolsStart, toolsEnd, rightStart, rightEnd, width) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Width = window.MinWidth;
            await TestWindows.WaitForAsync(() => window.Toolbar.Bounds.Width <= window.MinWidth);
            var t = window.Toolbar;
            double X(Control c, double at) => c.TranslatePoint(new Point(at, 0), t)!.Value.X;
            return (X(t.LeftGroup, t.LeftGroup.Bounds.Width), X(t.Tools, 0), X(t.Tools, t.Tools.Bounds.Width),
                X(t.RightGroup, 0), X(t.RightGroup, t.RightGroup.Bounds.Width), t.Bounds.Width);
        });

        Assert.True(leftEnd <= toolsStart, $"The left group ends at {leftEnd}, past the tools' start at {toolsStart}");
        Assert.True(toolsEnd <= rightStart, $"The tools end at {toolsEnd}, past the right group's start at {rightStart}");
        Assert.True(rightEnd <= width, $"The right group ends at {rightEnd}, past the toolbar's width {width}");
    }

    [PdfViewerFact]
    public async Task Every_button_has_a_name_and_a_tooltip_with_its_shortcut()
    {
        var path = Fixture();

        var (missing, tips) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            // The logical tree: the controls the toolbar declares, not the parts of their templates (a split button's
            // template holds two unnamed buttons of its own).
            var controls = window.Toolbar.GetLogicalDescendants().OfType<Control>()
                .Where(c => c is Button or SplitButton or TextBox).ToList();
            var missing = controls.Where(c => string.IsNullOrEmpty(AutomationProperties.GetName(c)) || ToolTip.GetTip(c) is null)
                .Select(c => c.Name ?? c.GetType().Name).ToList();
            var tips = ToolsInOrder.ToDictionary(t => t, t => ToolTip.GetTip(window.Toolbar.ButtonFor(t)) as string);
            return (missing, tips);
        });

        Assert.Empty(missing);
        Assert.Equal("Highlight (⌃⌘H)", tips[PdfViewerTool.Highlight]);
        Assert.Equal("Underline (⌃⌘U)", tips[PdfViewerTool.Underline]);
        Assert.Equal("Strikethrough (⌃⌘S)", tips[PdfViewerTool.Strikeout]);
        Assert.Equal("Text (⌃⌘T)", tips[PdfViewerTool.Text]);
        Assert.Equal("Note (⌃⌘N)", tips[PdfViewerTool.Note]);
    }

    [PdfViewerFact]
    public async Task Clicking_the_label_already_shown_as_current_arms_Stamp_with_it()
    {
        var path = Fixture();

        var (tool, first, current, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var current = window.Viewer.StampLabels[0];
            window.Toolbar.StampFlyout.ShowAt(window.Toolbar.StampButton);
            // Laid out and under the pointer: the popup is placed a pass after it opens.
            await TestWindows.WaitForAsync(() => window.Toolbar.StampLabelList.GetVisualDescendants().OfType<ListBoxItem>()
                .FirstOrDefault(i => (string?)i.Content == current) is { IsEffectivelyVisible: true, Bounds.Width: > 0 } i
                && i.TranslatePoint(new Point(i.Bounds.Width / 2, i.Bounds.Height / 2), window) is { } at
                && window.InputHitTest(at) is Visual hit && (hit == i || i.IsVisualAncestorOf(hit)));
            var item = window.Toolbar.StampLabelList.GetVisualDescendants().OfType<ListBoxItem>().First(i => (string?)i.Content == current);
            Assert.Equal(current, window.Toolbar.StampLabelList.SelectedItem); // shown as current, so no SelectionChanged follows
            Click(window, item);
            return (window.Viewer.ActiveTool, window.Viewer.StampLabels[0], current, window.Toolbar.StampFlyout.IsOpen);
        });

        Assert.Equal(PdfViewerTool.Stamp, tool);
        Assert.Equal(current, first);
        Assert.False(shown);
    }

    [PdfViewerFact]
    public async Task The_format_menus_follow_the_armed_tool()
    {
        var path = Fixture();

        var wrong = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            var wrong = new List<string>();
            foreach (var tool in ToolsInOrder)
            {
                t.Arm(tool);
                var expected = (tool, Colour: tool != PdfViewerTool.Link, Line: tool is PdfViewerTool.Draw or PdfViewerTool.Shape or PdfViewerTool.Text, Text: tool == PdfViewerTool.Text);
                var shown = (tool, Colour: t.FormatGroup.IsVisible && t.ColourButton.IsVisible, Line: t.FormatGroup.IsVisible && t.LineButton.IsVisible, Text: t.FormatGroup.IsVisible && t.TextButton.IsVisible);
                if (shown != expected)
                    wrong.Add($"{shown}, not {expected}");
                t.Arm(tool);
            }
            if (t.FormatGroup.IsVisible)
                wrong.Add("With no tool armed, the format menus still show");
            return wrong;
        });

        Assert.Empty(wrong);
    }

    [PdfViewerFact]
    public async Task The_Colour_menu_sets_the_armed_tools_colour_from_a_swatch_or_a_typed_one()
    {
        var path = Fixture();

        var before = default(PdfAnnotationColor);
        var (fromSwatch, typed, swatchShown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            t.Arm(PdfViewerTool.Highlight);
            t.ShowColourMenu();
            before = window.Viewer.HighlightColor;
            t.Colours.Choose(CoffeePdf.Toolbar.ToolbarRules.Swatches[2]);
            var fromSwatch = window.Viewer.HighlightColor;
            t.Colours.ChooseHex("#112233");
            var shown = (t.ColourSwatch.Fill as Avalonia.Media.ISolidColorBrush)?.Color;
            return (fromSwatch, window.Viewer.HighlightColor, shown);
        });

        // The highlight's colour is half opaque by default, and a new colour keeps that.
        Assert.NotEqual(255, before.A);
        Assert.Equal(new PdfAnnotationColor(0, 122, 255, before.A), fromSwatch);
        Assert.Equal(new PdfAnnotationColor(0x11, 0x22, 0x33, before.A), typed);
        Assert.Equal(Avalonia.Media.Color.FromArgb(before.A, 0x11, 0x22, 0x33), swatchShown);
    }

    [PdfViewerFact]
    public async Task A_colour_chosen_for_a_slot_with_no_colour_is_opaque()
    {
        var path = Fixture();

        var fill = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            t.Arm(PdfViewerTool.Shape);
            window.Viewer.ShapeFillColor = null;
            t.ShowColourMenu();
            t.Colours.Choose(CoffeePdf.Toolbar.ToolbarRules.Swatches[2]);
            return window.Viewer.ShapeFillColor;
        });

        Assert.Equal(new PdfAnnotationColor(0, 122, 255, 255), fill);
    }

    [PdfViewerFact]
    public async Task A_shapes_Colour_menu_has_fill_and_stroke_and_No_Colour_clears_the_fill()
    {
        var path = Fixture();

        var (names, fill) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            t.Arm(PdfViewerTool.Shape);
            window.Viewer.ShapeFillColor = new PdfAnnotationColor(255, 0, 0, 255);
            t.ShowColourMenu();
            t.Colours.Choose(null); // the first tab, Fill
            return (t.ColourSlots().Select(s => s.Name).ToList(), window.Viewer.ShapeFillColor);
        });

        Assert.Equal(new[] { "Fill", "Stroke" }, names);
        Assert.Null(fill);
    }

    [PdfViewerFact]
    public async Task The_Line_menu_sets_the_armed_tools_width()
    {
        var path = Fixture();

        var (draw, shape) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            t.Arm(PdfViewerTool.Draw);
            t.ShowLineMenu();
            t.LineWidths.Choose(4);
            t.Arm(PdfViewerTool.Shape);
            t.ShowLineMenu();
            t.LineWidths.Choose(7.5);
            return (window.Viewer.DrawStrokeWidth, window.Viewer.ShapeStrokeWidth);
        });

        Assert.Equal(4f, draw);
        Assert.Equal(7.5f, shape);
    }

    [PdfViewerFact]
    public async Task The_Text_menu_sets_the_text_boxs_format()
    {
        var path = Fixture();

        var (attributes, align, size, font) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            t.Arm(PdfViewerTool.Text);
            t.ShowTextMenu();
            t.TextFormat.BoldButton.IsChecked = true;
            t.TextFormat.ItalicButton.IsChecked = true;
            // The flyout isn't open, so its button isn't in the window to click: raise its Click, which its handler takes.
            t.TextFormat.AlignCentreButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            t.TextFormat.SizeBox.SelectedItem = 18f;
            t.TextFormat.FontBox.SelectedItem = CoffeePdf.Toolbar.ToolbarRules.Fonts[^1];
            var v = window.Viewer;
            return (v.TextAnnotationFontAttributes, v.TextAnnotationTextAlign, v.TextAnnotationFontSize, v.TextAnnotationFont);
        });

        Assert.Equal(FontAttributes.Bold | FontAttributes.Italic, attributes);
        Assert.Equal(TextAnnotationAlignment.Center, align);
        Assert.Equal(18f, size);
        Assert.Equal(CoffeePdf.Toolbar.ToolbarRules.Fonts[^1], font);
    }

    [PdfViewerFact]
    public async Task A_selected_shape_shows_its_format_menus_until_the_selection_ends()
    {
        var path = Fixture();

        var (whileSelected, afterwards) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var (v, t) = (window.Viewer, window.Toolbar);
            await TestWindows.WaitForAsync(() => HasPage(v));
            t.ChooseShape(ShapeType.Rectangle);
            v.ShapeFillColor = new PdfAnnotationColor(255, 0, 0, 255); // so the click can land on its centre, not its edge
            await DragUntilAnnotatedAsync(window, v, OnPage(v, window, 200, 500), OnPage(v, window, 300, 420), PdfAnnotationType.Square);
            v.ActiveTool = PdfViewerTool.None; // so what shows is the selection's, not the tool's
            Click(window, OnPage(v, window, 250, 460));
            await TestWindows.WaitForAsync(() => t.Selection() == CoffeePdf.Toolbar.PageSelection.Shape);
            var whileSelected = (t.ColourButton.IsVisible, t.LineButton.IsVisible, t.TextButton.IsVisible);
            Click(window, OnPage(v, window, 450, 150));
            await TestWindows.WaitForAsync(() => !t.FormatGroup.IsVisible);
            return (whileSelected, t.FormatGroup.IsVisible);
        });

        Assert.Equal((true, true, false), whileSelected);
        Assert.False(afterwards);
    }

    [PdfViewerFact]
    public async Task At_its_minimum_width_the_text_tools_format_menus_still_fit()
    {
        var path = Fixture();

        var (formatEnd, rightStart, shown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.Width = window.MinWidth;
            window.Toolbar.Arm(PdfViewerTool.Text);
            await TestWindows.WaitForAsync(() => window.Toolbar.FormatGroup.IsEffectivelyVisible && window.Toolbar.FormatGroup.Bounds.Width > 0);
            var t = window.Toolbar;
            double X(Control c, double at) => c.TranslatePoint(new Avalonia.Point(at, 0), t)!.Value.X;
            return (X(t.FormatGroup, t.FormatGroup.Bounds.Width), X(t.RightGroup, 0), t.TextButton.IsEffectivelyVisible);
        });

        Assert.True(formatEnd <= rightStart, $"The format menus end at {formatEnd}, past the right group's start at {rightStart}");
        Assert.True(shown);
    }

    [PdfViewerFact]
    public async Task A_text_box_being_edited_shows_all_three_format_menus()
    {
        var path = Fixture();

        var (shown, names) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var (v, t) = (window.Viewer, window.Toolbar);
            await TestWindows.WaitForAsync(() => HasPage(v));
            t.Arm(PdfViewerTool.Text);
            await DragUntilAnnotatedAsync(window, v, OnPage(v, window, 250, 350), OnPage(v, window, 420, 290), PdfAnnotationType.FreeText);
            window.KeyTextInput("abc");
            Dispatcher.UIThread.RunJobs();
            await TestWindows.WaitForAsync(() => t.Selection() == CoffeePdf.Toolbar.PageSelection.TextBox);
            v.ActiveTool = PdfViewerTool.None; // so what shows is the selection's, not the tool's
            return ((t.ColourButton.IsVisible, t.LineButton.IsVisible, t.TextButton.IsVisible), t.ColourSlots().Select(s => s.Name).ToList());
        });

        Assert.Equal((true, true, true), shown);
        Assert.Equal(new[] { "Fill", "Border", "Text" }, names);
    }

    /// <summary>
    /// Drags with the armed tool until the viewer has the annotation it draws. Now and then (about one drag in six, in
    /// the headless run) the viewer drops a drag, as probe 5 saw, and a test that waits for what the drag made would wait
    /// for nothing; so a drag that made nothing is made again.
    /// </summary>
    private static async Task DragUntilAnnotatedAsync(Avalonia.Controls.Window window, PdfViewer viewer, Avalonia.Point from, Avalonia.Point to, PdfAnnotationType type)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Drag(window, from, to);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
            while (DateTime.UtcNow < deadline)
            {
                if ((await viewer.GetAnnotationsAsync(0, CancellationToken.None)).Any(a => a.Type == type))
                    return;
                await Task.Delay(1);
                Dispatcher.UIThread.RunJobs();
            }
        }
        throw new TimeoutException($"No {type} annotation came from five drags");
    }
}
