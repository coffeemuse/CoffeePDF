using Avalonia;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Tests.Windows;
using Xunit.Abstractions;
using static CoffeePdf.Tests.Toolbar.ToolbarTesting;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>
/// The toolbar spec's §6: what the PdfViewer does that its docs don't settle, printed for the spec's §10. Run with:
/// dotnet test CoffeePdf.sln --filter "FullyQualifiedName~ToolbarProbes" --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "PdfViewer")]
[Trait("Category", "Probe")]
public sealed class ToolbarProbes(ITestOutputHelper output) : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-probes-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Fixture()
    {
        var path = Path.Combine(_dir, "fixture.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    /// <summary>A viewer as the window will have it (its toolbar hidden, unless asked), loaded, in a window of its own.</summary>
    private static async Task<(Window Window, PdfViewer Viewer)> OpenAsync(string path, bool toolbar = false)
    {
        var viewer = new PdfViewer { IsToolbarVisible = toolbar, AllowAnnotationEditing = true, AutoSave = false, SidebarMode = SidebarMode.None };
        var window = new Window { Content = viewer, Width = 1000, Height = 760 };
        window.Show();
        await Viewers.ShowAndLoadAsync(viewer, () => viewer.Source = path);
        // Drawn, and laid out the same on two checks running: the page is fitted to the window after it first appears.
        var last = default(Rect);
        await TestWindows.WaitForAsync(() =>
        {
            if (!HasPage(viewer))
                return false;
            var now = FirstPage(viewer).Bounds;
            var settled = now == last;
            last = now;
            return settled;
        });
        return (window, viewer);
    }

    private void Print(IEnumerable<string> lines)
    {
        foreach (var line in lines)
            output.WriteLine(line);
    }

    private static string Describe(object? element) => element is Control c
        ? $"{c.GetType().Name} {c.Name ?? "(no name)"}, {(c.IsEffectivelyVisible ? "visible" : "hidden")}"
        : element?.GetType().Name ?? "nothing";

    private static string Hex(PdfAnnotationColor? c) => c is { } x ? $"#{x.R:X2}{x.G:X2}{x.B:X2}{x.A:X2}" : "none";

    /// <summary>Probe 1: what the viewer's keys do while its toolbar is hidden.</summary>
    [PdfViewerFact]
    public async Task What_the_viewers_keys_do_with_its_toolbar_hidden()
    {
        var path = Fixture();

        var lines = await Headless.RunAsync(async () =>
        {
            var (window, viewer) = await OpenAsync(path);
            var seen = new List<string> { $"Key bindings on the viewer: {string.Join(", ", viewer.KeyBindings.Select(b => b.Gesture?.ToString()))}" };
            foreach (var (name, key, modifiers) in new[]
                     {
                         ("⌘F", Key.F, RawInputModifiers.Meta), ("⌘+", Key.OemPlus, RawInputModifiers.Meta),
                         ("⌘0", Key.D0, RawInputModifiers.Meta), ("H", Key.H, RawInputModifiers.None),
                         ("N", Key.N, RawInputModifiers.None), ("Escape", Key.Escape, RawInputModifiers.None),
                     })
            {
                viewer.FocusViewer();
                Dispatcher.UIThread.RunJobs();
                var zoom = viewer.ZoomLevel;
                var tool = viewer.ActiveTool;
                Press(window, key, modifiers);
                seen.Add($"{name}: focus on {Describe(window.FocusManager?.GetFocusedElement())}; zoom {zoom:0.###} to {viewer.ZoomLevel:0.###}; tool {tool} to {viewer.ActiveTool}");
            }
            window.Close();
            return seen;
        });

        Print(lines);
    }

    /// <summary>Probe 2: whether a highlight already on the page shows as selected, and takes a new colour.</summary>
    [PdfViewerFact]
    public async Task Whether_a_highlight_on_the_page_can_be_recoloured()
    {
        var path = Fixture();

        var lines = await Headless.RunAsync(async () =>
        {
            var (window, viewer) = await OpenAsync(path);
            var seen = new List<string>();
            viewer.SelectAll();
            var index = await viewer.HighlightSelectionAsync(new PdfAnnotationColor(255, 212, 0, 255));
            viewer.ClearSelection();
            seen.Add($"HighlightSelectionAsync gave index {index}");
            var before = (await viewer.GetAnnotationsAsync(0, CancellationToken.None)).First(a => a.Type == PdfAnnotationType.Highlight);
            seen.Add($"The highlight's bounds: {before.Bounds.Left:0},{before.Bounds.Bottom:0} to {before.Bounds.Right:0},{before.Bounds.Top:0}");
            Click(window, OnPage(viewer, window, (before.Bounds.Left + before.Bounds.Right) / 2, (before.Bounds.Bottom + before.Bounds.Top) / 2));
            seen.Add($"After a click on it, the viewer's states: {States(viewer)}");
            viewer.HighlightColor = new PdfAnnotationColor(0, 122, 255, 255);
            Dispatcher.UIThread.RunJobs();
            var after = (await viewer.GetAnnotationsAsync(0, CancellationToken.None)).First(a => a.Type == PdfAnnotationType.Highlight);
            seen.Add($"Its colour was {Hex(before.Color)}; after HighlightColor was set to #007AFFFF it's {Hex(after.Color)}");
            window.Close();
            return seen;
        });

        Print(lines);
    }

    /// <summary>Probe 3: whether putting a label first in StampLabels makes the Stamp tool place it.</summary>
    [PdfViewerFact]
    public async Task Whether_StampLabels_chooses_the_label_a_stamp_gets()
    {
        var path = Fixture();

        var lines = await Headless.RunAsync(async () =>
        {
            var (window, viewer) = await OpenAsync(path);
            var labels = viewer.StampLabels;
            var chosen = labels[Math.Min(3, labels.Count - 1)];
            var seen = new List<string> { $"StampLabels: {string.Join(", ", labels)}", $"Chosen: {chosen}, named /{PdfViewer.StampNameForLabel(chosen)}" };
            viewer.StampLabels = [chosen, .. labels.Where(l => l != chosen)];
            viewer.ActiveTool = PdfViewerTool.Stamp;
            var added = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            viewer.AnnotationAdded += (_, _) => added.TrySetResult();
            Click(window, OnPage(viewer, window, 306, 400));
            await added.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var saved = new MemoryStream();
            await viewer.SaveDocumentAsync(saved);
            var names = Regex.Matches(Encoding.Latin1.GetString(saved.ToArray()), @"/Name\s*/(\w+)").Select(m => m.Groups[1].Value);
            seen.Add($"The saved stamps' /Name: {string.Join(", ", names)}");
            window.Close();
            return seen;
        });

        Print(lines);
    }

    /// <summary>Probe 4: how OverflowToolbarPanel collapses outside the viewer's template.</summary>
    [PdfViewerFact]
    public async Task How_OverflowToolbarPanel_collapses_on_its_own()
    {
        var lines = await Headless.RunAsync(() =>
        {
            var panel = new OverflowToolbarPanel { Spacing = 2 };
            for (var i = 0; i < 10; i++)
                panel.Children.Add(new Button { Width = 30, Height = 30, Content = $"{i}" });
            var more = new Button { Width = 30, Height = 30, Content = "more" };
            OverflowToolbarPanel.SetIsOverflowIndicator(more, true);
            panel.Children.Add(more);
            var pinned = new Button { Width = 60, Height = 30, Content = "pinned" };
            OverflowToolbarPanel.SetIsPinned(pinned, true);
            panel.Children.Add(pinned);
            var changes = 0;
            panel.OverflowChanged += (_, _) => changes++;
            var host = new Border { Child = panel, Width = 600 };
            var window = new Window { Content = host, Width = 800, Height = 100 };
            window.Show();
            var seen = new List<string>();
            foreach (var width in new[] { 600.0, 200.0, 600.0 })
            {
                host.Width = width;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var overflowed = string.Join(",", panel.OverflowedChildren.Select(c => (c as Button)?.Content));
                seen.Add($"At {width}: overflowed [{overflowed}], {panel.VisibleButtonCount} visible, the indicator at {more.Bounds}, the pinned one at {pinned.Bounds}, OverflowChanged {changes} times so far");
            }
            window.Close();
            return Task.FromResult(seen);
        });

        Print(lines);
    }

    /// <summary>Probe 5: what the viewer's states say about a shape and a text box, selected and not.</summary>
    [PdfViewerFact]
    public async Task What_the_viewers_states_say_about_a_selection()
    {
        var path = Fixture();

        var lines = await Headless.RunAsync(async () =>
        {
            var (window, viewer) = await OpenAsync(path);
            var seen = new List<string> { $"The first page is drawn by {Describe(FirstPage(viewer))}" };
            void Log(string what) => seen.Add($"{what}: [{States(viewer)}], tool {viewer.ActiveTool}, shape {viewer.SelectedShapeType}");
            Log("At the start");
            viewer.SelectedShapeType = ShapeType.Rectangle;
            viewer.ActiveTool = PdfViewerTool.Shape;
            Drag(window, OnPage(viewer, window, 200, 500), OnPage(viewer, window, 300, 420));
            Log("After drawing a rectangle");
            viewer.ActiveTool = PdfViewerTool.None;
            Log("After disarming");
            Click(window, OnPage(viewer, window, 200, 460));
            Log("After clicking the rectangle's left edge");
            Click(window, OnPage(viewer, window, 450, 150));
            Log("After clicking an empty part of the page");
            viewer.ActiveTool = PdfViewerTool.Text;
            Click(window, OnPage(viewer, window, 300, 300));
            Log("After clicking with the Text tool");
            seen.Add($"After that click: {viewer.GetVisualDescendants().OfType<TextBox>().Count()} text boxes in the viewer, focus on {Describe(window.FocusManager?.GetFocusedElement())}");
            window.KeyTextInput("Probe");
            Dispatcher.UIThread.RunJobs();
            Log("After typing");
            Click(window, OnPage(viewer, window, 450, 150));
            Log("After clicking away");
            viewer.ActiveTool = PdfViewerTool.None;
            Click(window, OnPage(viewer, window, 300, 300));
            Log("After clicking the text box with no tool");
            // A text box by dragging, with the Text tool and then with the Shape tool set to Text.
            void Try(string what, Action act)
            {
                try
                {
                    act();
                }
                catch (Exception e)
                {
                    seen.Add($"{what} threw {e.GetType().Name}");
                }
            }
            void LogFocus(string what) => seen.Add($"{what}: focus on {Describe(window.FocusManager?.GetFocusedElement())}");
            viewer.ActiveTool = PdfViewerTool.Text;
            Try("The Text-tool drag", () => Drag(window, OnPage(viewer, window, 250, 350), OnPage(viewer, window, 420, 290)));
            Log("After dragging a box with the Text tool");
            LogFocus("After that drag");
            window.KeyTextInput("Probe");
            Dispatcher.UIThread.RunJobs();
            Log("After typing into it");
            LogFocus("After typing");
            Try("The click on empty page after the Text-tool drag", () => Click(window, OnPage(viewer, window, 500, 100)));
            Log("After clicking an empty part of the page");
            viewer.ActiveTool = PdfViewerTool.None;
            Try("The click on the Text-tool box's edge", () => Click(window, OnPage(viewer, window, 250, 320)));
            Log("After clicking the dragged text box's left edge with no tool");
            LogFocus("After that click");
            seen.Add($"On the page so far: {string.Join(", ", (await viewer.GetAnnotationsAsync(0, CancellationToken.None)).Select(a => a.Type))}");
            Try("The click on empty page", () => Click(window, OnPage(viewer, window, 500, 100)));
            viewer.SelectedShapeType = ShapeType.Text;
            viewer.ActiveTool = PdfViewerTool.Shape;
            Try("The Shape-tool Text drag", () => Drag(window, OnPage(viewer, window, 250, 250), OnPage(viewer, window, 420, 190)));
            Log("After dragging a Text shape with the Shape tool");
            viewer.ActiveTool = PdfViewerTool.None;
            Log("After disarming");
            Try("The click on the Text shape's edge", () => Click(window, OnPage(viewer, window, 250, 220)));
            Log("After clicking that Text shape's left edge");
            LogFocus("After that click");
            var types = (await viewer.GetAnnotationsAsync(0, CancellationToken.None)).Select(a => a.Type);
            seen.Add($"On the page: {string.Join(", ", types)}");
            window.Close();
            return seen;
        });

        Print(lines);
    }

    /// <summary>
    /// Probe 6 (the plan's): what the built-in toolbar's format menus offer, the viewer's defaults, and how search counts,
    /// so CoffeePDF's menus offer the same.
    /// </summary>
    [PdfViewerFact]
    public async Task What_the_built_in_format_menus_and_search_offer()
    {
        var path = Fixture();

        var lines = await Headless.RunAsync(async () =>
        {
            Avalonia.Controls.INameScope? parts = null;
            var viewer = new PdfViewer { AllowAnnotationEditing = true, AutoSave = false };
            viewer.TemplateApplied += (_, e) => parts = e.NameScope;
            var window = new Window { Content = viewer, Width = 1000, Height = 760 };
            window.Show();
            await Viewers.ShowAndLoadAsync(viewer, () => viewer.Source = path);
            viewer.ActiveTool = PdfViewerTool.Text;
            Dispatcher.UIThread.RunJobs();
            var seen = new List<string>();
            string Items(string name) => parts?.Find<ItemsControl>(name) is { } list
                ? $"{list.ItemCount}: {string.Join(" | ", list.Items.Select(i => (i as ContentControl)?.Content ?? i))}"
                : "not found";
            static IBrush? Background(Control c) => c switch
            {
                Border border => border.Background,
                TemplatedControl templated => templated.Background,
                Panel panel => panel.Background,
                _ => null,
            };
            seen.Add($"Fonts ({Items("PART_TextFontComboBox")})");
            seen.Add($"Sizes ({Items("PART_TextFontSizeComboBox")})");
            if (parts?.Find<Panel>("PART_ColorSwatchPanel") is { } swatches)
                seen.Add("Swatches: " + string.Join(" ", swatches.Children.Select(c => Background(c) is ISolidColorBrush b ? b.Color.ToString() : c.GetType().Name)));
            else
                seen.Add("Swatches: not found");
            seen.Add($"Defaults: font {viewer.TextAnnotationFont}, size {viewer.TextAnnotationFontSize}, highlight {Hex(viewer.HighlightColor)}, " +
                     $"underline {Hex(viewer.UnderlineColor)}, strikeout {Hex(viewer.StrikeoutColor)}, squiggly {Hex(viewer.SquigglyColor)}, " +
                     $"note {Hex(viewer.NoteColor)}, draw {Hex(viewer.DrawStrokeColor)} {viewer.DrawStrokeWidth}, shape fill {Hex(viewer.ShapeFillColor)} " +
                     $"stroke {Hex(viewer.ShapeStrokeColor)} {viewer.ShapeStrokeWidth}, stamp {Hex(viewer.StampColor)}, zoom {viewer.MinZoom} to {viewer.MaxZoom}");
            viewer.SearchQuery = "fixture";
            await TestWindows.WaitForAsync(() => viewer.SearchResultCount > 0);
            seen.Add($"After SearchQuery = \"fixture\": {viewer.SearchResultCount} found, index {viewer.CurrentSearchResultIndex}");
            viewer.FindNext();
            Dispatcher.UIThread.RunJobs();
            seen.Add($"After FindNext: index {viewer.CurrentSearchResultIndex}");
            viewer.ClearSearch();
            seen.Add($"After ClearSearch: query \"{viewer.SearchQuery}\", {viewer.SearchResultCount} found, index {viewer.CurrentSearchResultIndex}");
            window.Close();
            return seen;
        });

        Print(lines);
    }
}
