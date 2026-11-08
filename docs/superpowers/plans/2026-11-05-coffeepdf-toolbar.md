# CoffeePDF's own toolbar Implementation Plan

Written 2026-11-05 from `docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md`, which the owner approved the
same day. Its code hasn't been built yet. The PdfViewer API it uses was read from the 12.0.1 assembly by reflection
beforehand, with throwaway tools outside the repository: every property, field, method and event named below exists
with the type given. So do Avalonia 12.1.4's `ToggleSplitButton`, `TextBox.InnerLeftContent` and `InnerRightContent`,
`PopupFlyoutBase.Opening`, `ToggleButton.IsCheckedChanged`, and the headless `KeyPress`, `MouseDown`, `MouseMove` and
`MouseUp` overloads used. What the docs don't settle is Task 1's to find out.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The PdfViewer's own toolbar is hidden, and a row of CoffeePDF's own, under the title bar, drives the viewer
through its public API, with every tool and format control the built-in toolbar had that CoffeePDF uses.

**Architecture:** `CoffeePdf/Toolbar/DocumentToolbar.axaml` is a `UserControl` that `DocumentWindow` docks at the top of
`DocumentPanel` and hands its PdfViewer once (`Attach`). It shows the viewer's properties as they change, and its
pseudo-classes (`:shape-selected`, `:editing-text`) for a selection, and sets them as the person acts. The tools sit
in the viewer's public `OverflowToolbarPanel`, with the format menus pinned. The small rules are static methods in
`ToolbarRules`, tested without a window. The icons are Fluent UI System Icons' shapes in `Toolbar/Icons.axaml`, written
by `Toolbar/make-icons.sh`.

**Tech Stack:** .NET 10, C#, Avalonia 12.1.4 (Fluent theme; headless with Skia for the tests), Avalonia's PdfViewer
12.0.1, xUnit, Fluent UI System Icons 1.1.344 (`@fluentui/svg-icons`, MIT).

**Spec:** `docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md` (read all of it first; the section numbers
below are its). The v1 spec, `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md`, has the window's modes (§6.1)
and the testing rules (§8).

## Global Constraints

- `TreatWarningsAsErrors` is on: a warning fails the build.
- `CoffeePdf.Core` references no Avalonia assembly (`ArchitectureTests`). The toolbar's rules speak the PdfViewer's
  types, so they live in the app, in `CoffeePdf/Toolbar/ToolbarRules.cs` (spec §4).
- The toolbar touches the PdfViewer only through its public API. App code never looks up a `PART_` of the viewer's
  template. Tests may, to find the page they click on, as the password tests already find `PART_PasswordTextBox`.
- The PdfViewer's `IsToolbarVisible`, `AutoSave` and `AllowDocumentSaving` stay off. Every save goes through
  `DocumentWindow.SaveToAsync`. The toolbar never saves.
- Every button has an accessible name (`AutomationProperties.Name`) and a tooltip (`ToolTip.Tip`). A tooltip names the
  shortcut where the menu bar has one: Highlight (⌃⌘H), Underline (⌃⌘U), Strikethrough (⌃⌘S), Text (⌃⌘T), Note (⌃⌘N),
  Search (⌘F), Find Previous (⇧⌘G), Find Next (⌘G), Print (⌘P).
- Icons come only from `CoffeePdf/Toolbar/Icons.axaml`, which `make-icons.sh` writes; nobody edits it by hand.
- Tests that need the PdfViewer use `[PdfViewerFact]` in a class with `[Trait("Category", "PdfViewer")]`; probes add
  `[Trait("Category", "Probe")]`. Tests wait for conditions, never for fixed times
  (`TestWindows.WaitForAsync`, or `Dispatcher.UIThread.RunJobs()` after input).
- Tests save with the made-up author `Test Reviewer`. No PDF is ever committed, and no personal detail goes into a file
  or a commit (the hooks in `.githooks` refuse them).
- The Avalonia licence key is never printed or logged. Never build with `-v:d`, `-v:diag` or `-bl`.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Never push, tag or open a pull request
  without the owner's go-ahead.
- A separate change may add `THIRD-PARTY-NOTICES.md` for what v1.0 already bundles (spec §8). Whichever lands first
  creates the file; the other adds its section.

---

### Before Task 1: check the hooks and the key

The spec and this plan are committed already, on this branch.

- [ ] **Step 1: Check the hooks are on, and the key is set (without printing it)**

```bash
git config core.hooksPath   # expect a path ending in .githooks; if empty, run: git config core.hooksPath .githooks
[ -n "${AVALONIA_LICENSE_KEY:-}" ] && echo "key set" || echo "key NOT set: the PdfViewer's tests and the probes will skip"
```

Expected: a hooks path, and "key set". Without the key, stop and ask the owner for it: Tasks 1 and 4 to 6 need it.

---

### Task 1: the probes (spec §6)

Five questions the docs don't settle, and a sixth this plan needs: what the built-in format menus and search offer, so
CoffeePDF's match them. They print what they find; their answers go in the spec's §10 before any toolbar code is
written.

**Files:**
- Create: `CoffeePdf.Tests/Toolbar/ToolbarTesting.cs`
- Create: `CoffeePdf.Tests/Toolbar/ToolbarProbes.cs`
- Modify: `docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md` (§10)

**Interfaces:**
- Produces: `ToolbarTesting.FirstPage(PdfViewer)`, `ToolbarTesting.HasPage(PdfViewer)`,
  `ToolbarTesting.OnPage(PdfViewer, Window, double pdfX, double pdfY)`,
  `ToolbarTesting.Click(Window, Point)`, `ToolbarTesting.Click(Window, Control, double across = 0.5)`,
  `ToolbarTesting.Drag(Window, Point, Point)`, `ToolbarTesting.Press(Window, Key, RawInputModifiers = None)`,
  `ToolbarTesting.Type(Window, TextBox, string, bool enter = true)`, `ToolbarTesting.States(PdfViewer)`, used by every
  later task's tests.

- [ ] **Step 1: Write the input helpers**

`CoffeePdf.Tests/Toolbar/ToolbarTesting.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>
/// Pointer and keyboard input for the toolbar's probes and tests, given as a person gives it, and where a Letter page's
/// points are in the window. Call these on the UI thread.
/// </summary>
internal static class ToolbarTesting
{
    /// <summary>
    /// The control that draws the first page: the smallest Letter-shaped control in the viewer's page host.
    /// PART_PagesContainer is the page host in the PdfViewer 12.0.1 template; the thumbnails, which are Letter-shaped
    /// too, are outside it.
    /// </summary>
    public static Control FirstPage(PdfViewer viewer)
    {
        var host = viewer.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "PART_PagesContainer")
            ?? throw new InvalidOperationException("The viewer has no PART_PagesContainer: has its template changed?");
        return host.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 100 && Math.Abs(c.Bounds.Height / c.Bounds.Width - 792.0 / 612) < 0.01)
            .MinBy(c => c.Bounds.Width) ?? throw new InvalidOperationException("No page is drawn yet");
    }

    /// <summary>Whether the first page is drawn yet, so a test can wait for it before clicking on it.</summary>
    public static bool HasPage(PdfViewer viewer)
    {
        try
        {
            return FirstPage(viewer).Bounds.Width > 100;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The point in <paramref name="window"/> over (<paramref name="pdfX"/>, <paramref name="pdfY"/>) on a Letter page.</summary>
    public static Point OnPage(PdfViewer viewer, Window window, double pdfX, double pdfY)
    {
        var page = FirstPage(viewer);
        var scale = page.Bounds.Width / 612;
        return page.TranslatePoint(new Point(pdfX * scale, (792 - pdfY) * scale), window)
            ?? throw new InvalidOperationException("The page isn't in the window");
    }

    public static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Clicks <paramref name="control"/> <paramref name="across"/> of the way across it. A split button's main part is on
    /// its left, so its tests click at 0.3.
    /// </summary>
    public static void Click(Window window, Control control, double across = 0.5) =>
        Click(window, control.TranslatePoint(new Point(control.Bounds.Width * across, control.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException($"{control.Name ?? control.GetType().Name} isn't in the window"));

    public static void Drag(Window window, Point from, Point to)
    {
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), RawInputModifiers.LeftMouseButton);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    public static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var physical = key switch
        {
            Key.Enter => PhysicalKey.Enter,
            Key.Escape => PhysicalKey.Escape,
            Key.F => PhysicalKey.F,
            Key.H => PhysicalKey.H,
            Key.N => PhysicalKey.N,
            Key.D0 => PhysicalKey.Digit0,
            Key.OemPlus => PhysicalKey.Equal,
            _ => PhysicalKey.None,
        };
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Puts the cursor in <paramref name="box"/>, replaces its text with <paramref name="text"/>, and presses Return when
    /// <paramref name="enter"/>.
    /// </summary>
    public static void Type(Window window, TextBox box, string text, bool enter = true)
    {
        box.Focus();
        box.SelectAll();
        window.KeyTextInput(text);
        if (enter)
            Press(window, Key.Enter);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The viewer's states, such as <c>:shape-selected</c>, as they stand.</summary>
    public static string States(PdfViewer viewer) => string.Join(' ', viewer.Classes.Where(c => c.StartsWith(':')).Order());
}
```

- [ ] **Step 2: Write the probes**

`CoffeePdf.Tests/Toolbar/ToolbarProbes.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
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
        await TestWindows.WaitForAsync(() => HasPage(viewer));
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
            window.KeyTextInput("Probe");
            Dispatcher.UIThread.RunJobs();
            Log("After typing");
            Click(window, OnPage(viewer, window, 450, 150));
            Log("After clicking away");
            viewer.ActiveTool = PdfViewerTool.None;
            Click(window, OnPage(viewer, window, 300, 300));
            Log("After clicking the text box with no tool");
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
```

If probe 6's `WaitForAsync` times out, setting `SearchQuery` doesn't search by itself: say so in its line, and try
`await viewer.SearchAsync("fixture", new SearchOptions())` in its place, printing what that returns.

- [ ] **Step 3: Build, and run the probes with the key**

The key has to be in `AVALONIA_LICENSE_KEY` (never print it).

```bash
dotnet build CoffeePdf.sln
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~ToolbarProbes" --logger "console;verbosity=detailed"
```

Expected: six passing probes, each printing its lines. A probe that throws (no page drawn, a part not found) is a
finding too: record what it says.

- [ ] **Step 4: Record the answers in the spec's §10**

Add a bullet to §10 of `docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md` for each probe, headed
"**Probe N (observed on the run's date, keyed run).**", with what it printed in plain words: the keys and where ⌘F put the
focus; whether the highlight showed a state and took the colour; the saved stamp's `/Name`; the overflow panel's
counts at each width and where the indicator and pinned button were; the states at each step and what told the text
box from the shape; the fonts, sizes, swatches, defaults and search indices.

- [ ] **Step 5: Check the answers against the plan, and stop if one changes the design**

Each answer has a place below that depends on it:

| Probe | The plan expects | If not |
|---|---|---|
| 1 | ⌘F does nothing harmful with the toolbar hidden, so Edit › Find… (Task 4) has the key | If ⌘F moved focus to a hidden box, keep Edit › Find…: the menu bar takes key equivalents first. Record it. |
| 2 | Either answer | If a selected highlight shows a state and takes the colour, add that state to `Selection()` in Task 6, with a `PageSelection.Markup` that gets `FormatMenus.Colour`. If not, §10 records the limit and nothing changes. |
| 3 | The chosen label's name is among the saved `/Name`s | Stop: tell the owner, since the fallback (`AddStampAsync` at the clicked point) is more code than the spec planned. |
| 4 | Ten buttons, at 200 wide, overflow some; the indicator shows only then; the pinned one stays | Stop: tell the owner, since CoffeePDF would need an overflow panel of its own. |
| 5 | `:shape-selected` for a selected rectangle, and something telling a text box from a shape | Task 6's `Selection()` uses `:editing-text`, or `:shape-selected` with `SelectedShapeType == ShapeType.Text`. Change it there to what the probe found. |
| 6 | A list of fonts and sizes | Tasks 2 and 6 use its fonts in `ToolbarRules.Fonts` and its sizes in `ToolbarRules.FontSizes`, in the order printed. Its search index decides `SearchCountText` in Task 4. |

- [ ] **Step 6: Commit**

```bash
git add CoffeePdf.Tests/Toolbar/ToolbarTesting.cs CoffeePdf.Tests/Toolbar/ToolbarProbes.cs docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md
git commit -m "Probe what the PdfViewer does with its toolbar hidden, and record it" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: the toolbar's rules (spec §3, §4, §5)

**Files:**
- Create: `CoffeePdf/Toolbar/ToolbarRules.cs`
- Test: `CoffeePdf.Tests/Toolbar/ToolbarRulesTests.cs`

**Interfaces:**
- Produces: `enum FormatMenus { None, Colour, Line, Text }` (flags), `enum PageSelection { None, Shape, TextBox }`,
  `ToolbarRules.For(PdfViewerTool, PageSelection) → FormatMenus`, `ParseZoom(string?, double min, double max) → double?`,
  `ZoomText(double) → string`, `ParsePage(string?, int count) → int?`,
  `WithLabelFirst(IReadOnlyList<string>, string) → IReadOnlyList<string>`, `ParseHex(string?) → PdfAnnotationColor?`,
  `Hex(PdfAnnotationColor) → string`, and the lists `ZoomPresets`, `FontSizes`, `Fonts`, `Swatches`, `Shapes`. All
  `internal`, in namespace `CoffeePdf.Toolbar`.

- [ ] **Step 1: Write the failing tests**

These need no window and no licence key: they only use the PdfViewer's enums and colour struct. xUnit's methods are
public, so the internal `FormatMenus` and `PageSelection` arrive as names.

`CoffeePdf.Tests/Toolbar/ToolbarRulesTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using CoffeePdf.Toolbar;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>The toolbar spec's §3 and §5: the toolbar's rules, without a window.</summary>
public sealed class ToolbarRulesTests
{
    [Theory]
    [InlineData(PdfViewerTool.Highlight, "None", "Colour")]
    [InlineData(PdfViewerTool.Underline, "None", "Colour")]
    [InlineData(PdfViewerTool.Strikeout, "None", "Colour")]
    [InlineData(PdfViewerTool.Squiggly, "None", "Colour")]
    [InlineData(PdfViewerTool.Note, "None", "Colour")]
    [InlineData(PdfViewerTool.Stamp, "None", "Colour")]
    [InlineData(PdfViewerTool.Draw, "None", "Colour, Line")]
    [InlineData(PdfViewerTool.Shape, "None", "Colour, Line")]
    [InlineData(PdfViewerTool.Text, "None", "Colour, Line, Text")]
    [InlineData(PdfViewerTool.Link, "None", "None")]
    [InlineData(PdfViewerTool.None, "None", "None")]
    [InlineData(PdfViewerTool.None, "Shape", "Colour, Line")]
    [InlineData(PdfViewerTool.None, "TextBox", "Colour, Line, Text")]
    [InlineData(PdfViewerTool.Highlight, "Shape", "Colour, Line")]
    public void A_tool_or_selection_gets_the_specs_format_menus(PdfViewerTool tool, string selection, string menus) =>
        Assert.Equal(Enum.Parse<FormatMenus>(menus), ToolbarRules.For(tool, Enum.Parse<PageSelection>(selection)));

    [Theory]
    [InlineData("135%", 1.35)]
    [InlineData("135", 1.35)]
    [InlineData(" 50 % ", 0.5)]
    [InlineData("100000", 8)]
    [InlineData("1", 0.25)]
    public void A_typed_zoom_is_read_and_clamped(string typed, double level) =>
        Assert.Equal(level, ToolbarRules.ParseZoom(typed, min: 0.25, max: 8)!.Value, precision: 6);

    [Theory]
    [InlineData("")]
    [InlineData("big")]
    [InlineData("%")]
    [InlineData(null)]
    public void Anything_else_is_no_zoom(string? typed) => Assert.Null(ToolbarRules.ParseZoom(typed, 0.25, 8));

    [Theory]
    [InlineData(1.0, "100%")]
    [InlineData(1.356, "136%")]
    [InlineData(0.5, "50%")]
    public void A_zoom_shows_as_a_whole_percentage(double level, string shown) => Assert.Equal(shown, ToolbarRules.ZoomText(level));

    [Theory]
    [InlineData("2", 12, 2)]
    [InlineData(" 12 ", 12, 12)]
    [InlineData("40", 12, 12)]
    [InlineData("0", 12, 1)]
    public void A_typed_page_is_read_and_clamped(string typed, int count, int page) =>
        Assert.Equal(page, ToolbarRules.ParsePage(typed, count));

    [Theory]
    [InlineData("two", 12)]
    [InlineData("-3", 12)]
    [InlineData("", 12)]
    [InlineData("1", 0)]
    public void Anything_else_is_no_page(string typed, int count) => Assert.Null(ToolbarRules.ParsePage(typed, count));

    [Fact]
    public void A_chosen_stamp_label_goes_first_and_the_rest_keep_their_order() =>
        Assert.Equal(new[] { "Draft", "Approved", "Final", "Void" }, ToolbarRules.WithLabelFirst(["Approved", "Draft", "Final", "Void"], "Draft"));

    [Fact]
    public void A_hex_colour_is_read_with_or_without_its_hash()
    {
        Assert.Equal(new PdfAnnotationColor(0x11, 0x22, 0xAB, 255), ToolbarRules.ParseHex("#1122ab"));
        Assert.Equal(new PdfAnnotationColor(0x11, 0x22, 0xAB, 255), ToolbarRules.ParseHex(" 1122AB "));
        Assert.Null(ToolbarRules.ParseHex("#12345"));
        Assert.Null(ToolbarRules.ParseHex("#GG0000"));
        Assert.Equal("#1122AB", ToolbarRules.Hex(new PdfAnnotationColor(0x11, 0x22, 0xAB, 255)));
    }

    [Fact]
    public void The_lists_are_the_specs()
    {
        Assert.Equal(new[] { 0.5, 0.75, 1, 1.25, 1.5, 2, 4 }, ToolbarRules.ZoomPresets);
        Assert.Equal(new[] { ShapeType.Line, ShapeType.Arrow, ShapeType.Rectangle, ShapeType.Circle, ShapeType.Polygon, ShapeType.Star }, ToolbarRules.Shapes);
        Assert.Equal(8, ToolbarRules.Swatches.Count);
        Assert.NotEmpty(ToolbarRules.Fonts);
        Assert.NotEmpty(ToolbarRules.FontSizes);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~ToolbarRulesTests"
```

Expected: the build fails: `CoffeePdf.Toolbar` and `ToolbarRules` don't exist.

- [ ] **Step 3: Write the rules**

`CoffeePdf/Toolbar/ToolbarRules.cs`. `Fonts` and `FontSizes` are probe 6's lists, in the order it printed them; the
values below are what to use only if it printed none.

```csharp
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;

namespace CoffeePdf.Toolbar;

/// <summary>The format menus the toolbar shows beside its tools (toolbar spec §3's table).</summary>
[Flags]
internal enum FormatMenus
{
    None = 0,
    Colour = 1,
    Line = 2,
    Text = 4,
}

/// <summary>What's selected on a page, as far as the format menus go.</summary>
internal enum PageSelection
{
    None,
    Shape,
    TextBox,
}

/// <summary>
/// The toolbar's small rules (toolbar spec §4). They hold no Avalonia state, so their tests need no window. The core
/// can't hold them, since they speak the PdfViewer's types.
/// </summary>
internal static class ToolbarRules
{
    /// <summary>The zoom menu's levels, 50 to 400% (spec §3).</summary>
    public static readonly IReadOnlyList<double> ZoomPresets = [0.5, 0.75, 1, 1.25, 1.5, 2, 4];

    /// <summary>The Shapes menu, in the spec's order.</summary>
    public static readonly IReadOnlyList<ShapeType> Shapes =
        [ShapeType.Line, ShapeType.Arrow, ShapeType.Rectangle, ShapeType.Circle, ShapeType.Polygon, ShapeType.Star];

    /// <summary>The Text menu's fonts: the built-in toolbar's (the toolbar spec's §10, probe 6).</summary>
    public static readonly IReadOnlyList<string> Fonts = ["Helvetica", "Times-Roman", "Courier"];

    /// <summary>The Text menu's sizes, in points: the built-in toolbar's (§10, probe 6).</summary>
    public static readonly IReadOnlyList<float> FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 24, 36, 48, 72];

    /// <summary>The Colour menu's swatches: yellow, green, blue, pink, purple, red, orange and black.</summary>
    public static readonly IReadOnlyList<PdfAnnotationColor> Swatches =
    [
        new(255, 212, 0, 255), new(52, 199, 89, 255), new(0, 122, 255, 255), new(255, 45, 85, 255),
        new(175, 82, 222, 255), new(255, 59, 48, 255), new(255, 149, 0, 255), new(0, 0, 0, 255),
    ];

    /// <summary>The format menus for <paramref name="tool"/>, armed, or for <paramref name="selection"/>, which wins.</summary>
    public static FormatMenus For(PdfViewerTool tool, PageSelection selection) => selection switch
    {
        PageSelection.Shape => FormatMenus.Colour | FormatMenus.Line,
        PageSelection.TextBox => FormatMenus.Colour | FormatMenus.Line | FormatMenus.Text,
        _ => tool switch
        {
            PdfViewerTool.Highlight or PdfViewerTool.Underline or PdfViewerTool.Strikeout or PdfViewerTool.Squiggly
                or PdfViewerTool.Note or PdfViewerTool.Stamp => FormatMenus.Colour,
            PdfViewerTool.Draw or PdfViewerTool.Shape => FormatMenus.Colour | FormatMenus.Line,
            PdfViewerTool.Text => FormatMenus.Colour | FormatMenus.Line | FormatMenus.Text,
            _ => FormatMenus.None,
        },
    };

    /// <summary>
    /// A typed zoom, "135%" or "135", as a zoom level (1 is 100%) clamped to [<paramref name="min"/>,
    /// <paramref name="max"/>], or null when it isn't a number (spec §5).
    /// </summary>
    public static double? ParseZoom(string? text, double min, double max)
    {
        var digits = text?.Trim().TrimEnd('%').Trim();
        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && double.IsFinite(percent)
            ? Math.Clamp(percent / 100, min, max)
            : null;
    }

    /// <summary>A zoom level as the zoom box shows it: a whole percentage.</summary>
    public static string ZoomText(double level) =>
        $"{Math.Round(level * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";

    /// <summary>A typed page number clamped to 1 to <paramref name="count"/>, or null when it isn't one (spec §5).</summary>
    public static int? ParsePage(string? text, int count) =>
        count > 0 && int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var page)
            ? Math.Clamp(page, 1, count)
            : null;

    /// <summary>
    /// <paramref name="labels"/> with <paramref name="label"/> first. The Stamp tool places the first label in
    /// <c>StampLabels</c> unless its own menu chose one, and nothing public chooses one (spec §6, probe 3).
    /// </summary>
    public static IReadOnlyList<string> WithLabelFirst(IReadOnlyList<string> labels, string label) =>
        [label, .. labels.Where(l => l != label)];

    /// <summary>A typed colour, "#RRGGBB" or "RRGGBB", opaque; null when it isn't one.</summary>
    public static PdfAnnotationColor? ParseHex(string? text)
    {
        var hex = text?.Trim().TrimStart('#');
        return hex is { Length: 6 } && int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
            ? new PdfAnnotationColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255)
            : null;
    }

    /// <summary>A colour as the Colour menu's box shows it: "#RRGGBB".</summary>
    public static string Hex(PdfAnnotationColor colour) => $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
}
```

- [ ] **Step 4: Run the tests to see them pass**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~ToolbarRulesTests"
```

Expected: all pass, with or without the key.

- [ ] **Step 5: Commit**

```bash
git add CoffeePdf/Toolbar/ToolbarRules.cs CoffeePdf.Tests/Toolbar/ToolbarRulesTests.cs
git commit -m "Add the toolbar's rules: its format menus, typed zoom and page, and stamp labels" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: the icons, and their notice (spec §3, §8)

**Files:**
- Create: `CoffeePdf/Toolbar/make-icons.sh` (executable)
- Create: `CoffeePdf/Toolbar/Icons.axaml` (written by the script)
- Modify: `CoffeePdf/App.axaml`
- Create or modify: `THIRD-PARTY-NOTICES.md`
- Modify: `packaging/mac/package.sh`
- Test: `CoffeePdf.Tests/Toolbar/IconsTests.cs`

**Interfaces:**
- Produces: the app-wide resources `Icon.PanelLeft`, `Icon.Highlight`, `Icon.Underline`, `Icon.Strikethrough`,
  `Icon.Squiggly`, `Icon.Draw`, `Icon.Text`, `Icon.Shapes`, `Icon.Note`, `Icon.Link`, `Icon.Stamp`, `Icon.Search`,
  `Icon.Print`, `Icon.More`, `Icon.ChevronDown`, `Icon.ChevronUp`, `Icon.Colour`, `Icon.Line`, `Icon.TextFormat`,
  `Icon.Bold`, `Icon.Italic`, `Icon.AlignLeft`, `Icon.AlignCentre`, `Icon.AlignRight`, `Icon.Dismiss`, `Icon.Options`:
  26 `StreamGeometry`s on a 20-point grid.

- [ ] **Step 1: Write the failing test**

`CoffeePdf.Tests/Toolbar/IconsTests.cs`. It needs no PdfViewer, so it's a plain `[Fact]`, run headless for the app's
resources.

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>The toolbar spec's §3: the toolbar's icons, each a shape on Fluent's 20-point grid.</summary>
public sealed class IconsTests
{
    [Fact]
    public async Task Every_icon_is_a_shape_on_the_20_point_grid_and_the_app_has_them()
    {
        var (count, wrong, inApp) = await Headless.RunAsync(() =>
        {
            var icons = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://CoffeePDF/Toolbar/Icons.axaml"));
            var wrong = new List<string>();
            foreach (var (key, value) in icons)
            {
                var bounds = (value as StreamGeometry)?.Bounds;
                if (bounds is not { Width: > 0, Height: > 0, Left: >= 0, Top: >= 0, Right: <= 20, Bottom: <= 20 })
                    wrong.Add($"{key}: {bounds}");
            }
            var inApp = App.Current.TryFindResource("Icon.Stamp", out var stamp) && stamp is StreamGeometry;
            return Task.FromResult((icons.Count, wrong, inApp));
        });

        Assert.Equal(26, count);
        Assert.Empty(wrong);
        Assert.True(inApp);
    }
}
```

- [ ] **Step 2: Run it to see it fail**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~IconsTests"
```

Expected: FAIL: `avares://CoffeePDF/Toolbar/Icons.axaml` isn't found.

- [ ] **Step 3: Write the script that writes the icons**

`CoffeePdf/Toolbar/make-icons.sh`. It runs only when the list changes, on a developer's Mac, and its output is
committed. The two icons Fluent lacks are drawn here in Fluent's style: a 20-point grid, with lines a point thick drawn
as outlines. The squiggle keeps Fluent's underline letter and swaps its bar for a wave.

```bash
#!/bin/bash
# Writes Icons.axaml, the toolbar's icons (toolbar spec §3, §4): Fluent UI System Icons' regular 20-point shapes at a
# pinned version (MIT, © Microsoft Corporation; THIRD-PARTY-NOTICES.md), and the two CoffeePDF draws itself. Run it
# after changing the list below, and commit Icons.axaml. Needs curl and python3.
set -euo pipefail

version=1.1.344
here=$(cd "$(dirname "$0")" && pwd)
out="$here/Icons.axaml"

# The key in the dictionary, then Fluent's name for the icon.
icons=(
  "Icon.PanelLeft panel_left"
  "Icon.Highlight highlight"
  "Icon.Underline text_underline"
  "Icon.Strikethrough text_strikethrough"
  "Icon.Draw pen"
  "Icon.Text text_t"
  "Icon.Shapes shapes"
  "Icon.Note note"
  "Icon.Link link"
  "Icon.Search search"
  "Icon.Print print"
  "Icon.More more_horizontal"
  "Icon.ChevronDown chevron_down"
  "Icon.ChevronUp chevron_up"
  "Icon.Colour color"
  "Icon.Line line_thickness"
  "Icon.TextFormat text_font"
  "Icon.Bold text_bold"
  "Icon.Italic text_italic"
  "Icon.AlignLeft text_align_left"
  "Icon.AlignCentre text_align_center"
  "Icon.AlignRight text_align_right"
  "Icon.Dismiss dismiss"
  "Icon.Options options"
)

# Fluent's underline letter, with a wave of a point's thickness in place of its bar.
squiggly="M 6 3.5 a .5 .5 0 0 0 -1 0 v 6.45 A 5 5 0 0 0 10 15 c 2.77 0 5 -2.26 5 -5.05 V 3.5 a .5 .5 0 0 0 -1 0 v 6.45 C 14 12.2 12.2 14 10 14 s -4 -1.8 -4 -4.05 z M 5 16.9 L 6.5 15.9 L 8 16.9 L 9.5 15.9 L 11 16.9 L 12.5 15.9 L 14 16.9 L 15 16.23 L 15 17.43 L 14 18.1 L 12.5 17.1 L 11 18.1 L 9.5 17.1 L 8 18.1 L 6.5 17.1 L 5 18.1 Z"
# A rubber stamp: a ring for the knob, the neck, the block, and the line it prints; even-odd, so the outlines are hollow.
stamp="F0 M 7 5 A 3 3 0 1 0 13 5 A 3 3 0 1 0 7 5 Z M 8 5 A 2 2 0 1 0 12 5 A 2 2 0 1 0 8 5 Z M 8.5 8 H 9.5 V 11 H 8.5 Z M 10.5 8 H 11.5 V 11 H 10.5 Z M 4 11 H 16 V 15 H 4 Z M 5 12 V 14 H 15 V 12 Z M 3 16.5 H 17 V 17.5 H 3 Z"

# Fluent's single path, spaced out: an SVG path may run numbers and an arc's flags together ("a1 1 0 011 1"), which
# Avalonia's path parser can misread.
path_of() {
  python3 -I -c '
import re, sys
paths = re.findall(r"<path[^>]*\sd=\"([^\"]+)\"", sys.stdin.read())
if len(paths) != 1:
    sys.exit(f"expected one path, found {len(paths)}")
d = paths[0]
commands = set("MmLlHhVvCcSsQqTtAaZz")
number = re.compile(r"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")
out, i, command, argument = [], 0, "", 0
while i < len(d):
    c = d[i]
    if c.isspace() or c == ",":
        i += 1
    elif c in commands:
        command, argument = c, 0
        out.append(c)
        i += 1
    elif command in "Aa" and argument % 7 in (3, 4):
        out.append(c)  # an arc flag is one digit, 0 or 1
        argument += 1
        i += 1
    else:
        m = number.match(d, i)
        if not m:
            sys.exit(f"cannot read the path at {i}: {d[i:i+12]}")
        out.append(m.group())
        argument += 1
        i = m.end()
print(" ".join(out))'
}

{
  echo "<!-- Written by make-icons.sh from Fluent UI System Icons $version (MIT, © Microsoft Corporation; THIRD-PARTY-NOTICES.md), with Icon.Squiggly and Icon.Stamp drawn there. Don't edit it by hand. -->"
  echo '<ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">'
  for entry in "${icons[@]}"; do
    key=${entry% *}
    name=${entry#* }
    d=$(curl -fsSL "https://cdn.jsdelivr.net/npm/@fluentui/svg-icons@$version/icons/${name}_20_regular.svg" | path_of)
    printf '  <StreamGeometry x:Key="%s">%s</StreamGeometry>\n' "$key" "$d"
  done
  printf '  <StreamGeometry x:Key="Icon.Squiggly">%s</StreamGeometry>\n' "$squiggly"
  printf '  <StreamGeometry x:Key="Icon.Stamp">%s</StreamGeometry>\n' "$stamp"
  echo '</ResourceDictionary>'
} > "$out.tmp"
mv "$out.tmp" "$out"
echo "Wrote $out"
```

- [ ] **Step 4: Run it**

```bash
chmod +x CoffeePdf/Toolbar/make-icons.sh
CoffeePdf/Toolbar/make-icons.sh
grep -c '<StreamGeometry' CoffeePdf/Toolbar/Icons.axaml
```

Expected: "Wrote …/Icons.axaml" and a count of 26. The `.axaml` is an `AvaloniaResource` by the SDK's default, so it's
compiled into the app with no change to the project.

- [ ] **Step 5: Give the app the icons**

In `CoffeePdf/App.axaml`, after `</Application.Styles>`:

```xml
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <!-- The toolbar's icons (toolbar spec §4), written by Toolbar/make-icons.sh. -->
        <ResourceInclude Source="avares://CoffeePDF/Toolbar/Icons.axaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
```

- [ ] **Step 6: Run the test to see it pass**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~IconsTests"
```

Expected: PASS. If an icon is out of bounds or fails to parse, its key is in the failure: fix the script, not the file.

- [ ] **Step 7: Add Fluent's notice, and ship the notices in the bundle**

If `THIRD-PARTY-NOTICES.md` exists at the repository's root (the separate notices change has landed), add this
section in its layout. If it doesn't, create it with a heading `# Third-party notices`, the line "What CoffeePDF ships
that others wrote, and their licences.", and this section. Check the licence text against
<https://github.com/microsoft/fluentui-system-icons/blob/main/LICENSE>: the npm package carries none.

````markdown
## Fluent UI System Icons

- **Used for:** the toolbar's icons, copied as shapes into `CoffeePdf/Toolbar/Icons.axaml` by
  `CoffeePdf/Toolbar/make-icons.sh`.
- **Version:** 1.1.344 (`@fluentui/svg-icons`).
- **Source:** <https://github.com/microsoft/fluentui-system-icons>
- **Licence:** MIT.

```text
MIT License

Copyright (c) 2020 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
````

If `packaging/mac/package.sh` doesn't copy the file yet, add, after its line
`cp "$here/CoffeePDF.icns" "$app/Contents/Resources/"`:

```bash
# The notices of what the app carries that others wrote (toolbar spec §8), beside the icon; never in Contents/MacOS.
[ -f "$repo/THIRD-PARTY-NOTICES.md" ] || die "THIRD-PARTY-NOTICES.md is missing: the bundle must carry its notices"
cp "$repo/THIRD-PARTY-NOTICES.md" "$app/Contents/Resources/"
```

and add `"$app/Contents/Resources/THIRD-PARTY-NOTICES.md"` to the `chmod 644` line after it.

- [ ] **Step 8: Check the script still parses**

```bash
bash -n packaging/mac/package.sh && echo ok
```

Expected: `ok`. (Task 7 builds a package, which checks the rest.)

- [ ] **Step 9: Commit**

```bash
git add CoffeePdf/Toolbar/make-icons.sh CoffeePdf/Toolbar/Icons.axaml CoffeePdf/App.axaml CoffeePdf.Tests/Toolbar/IconsTests.cs THIRD-PARTY-NOTICES.md packaging/mac/package.sh
git commit -m "Add the toolbar's icons from Fluent UI System Icons, with their notice in the bundle" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: the row, in the window (spec §3 left and right, §4, §5)

The toolbar with its left group (sidebar, page, zoom) and right group (search, Print), in the window, with the
PdfViewer's own toolbar hidden and Edit › Find…. The centre's tools come in Task 5.

**Files:**
- Create: `CoffeePdf/Toolbar/DocumentToolbar.axaml`
- Create: `CoffeePdf/Toolbar/DocumentToolbar.axaml.cs`
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml`
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml.cs`
- Create: `CoffeePdf.Tests/Fixtures/PagesPdf.cs`
- Modify: `CoffeePdf.Tests/Windows/MenuTests.cs`
- Test: `CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs`

**Interfaces:**
- Consumes: `ToolbarRules.ParsePage`, `ParseZoom`, `ZoomText`, `ZoomPresets` (Task 2); the `Icon.*` resources (Task 3);
  `ToolbarTesting` (Task 1); `ActionCommand` (`CoffeePdf.Windows`, existing).
- Produces: `public partial class DocumentToolbar : UserControl` in `CoffeePdf.Toolbar`, with
  `public void Attach(PdfViewer viewer)`, `internal Func<Task> Print { get; set; }`, `internal void FocusSearch()`, and
  the named parts the tests use: `SidebarButton`, `PageBox`, `PageCountText`, `ZoomBox`, `ZoomMenuButton`, `ZoomMenu`,
  `Tools` (an empty `OverflowToolbarPanel`), `LeftGroup`, `RightGroup`, `SearchBox`, `SearchResults`, `SearchCount`,
  `PreviousMatchButton`, `NextMatchButton`, `SearchOptionsButton`, `MatchCaseBox`, `WholeWordBox`, `PrintButton`.
  `DocumentWindow` gets the field `Toolbar` and `internal void OnFind(object?, EventArgs)`.

- [ ] **Step 1: Write the multi-page fixture**

`CoffeePdf.Tests/Fixtures/PagesPdf.cs`:

```csharp
namespace CoffeePdf.Tests.Fixtures;

/// <summary>A PDF of several Letter pages, each with the minimal fixture's text, for the page box's tests.</summary>
internal static class PagesPdf
{
    public static byte[] Build(int pages)
    {
        // Objects 1 to 5 are the minimal fixture's: the catalog, the page tree, its page, the text and the font; 6, its
        // note, is on no page. The other pages (7 on) are copies of its page, sharing the text and the font.
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: [], acroForm: null);
        var page = objects[2];
        var kids = new List<string> { "3 0 R" };
        for (var i = 1; i < pages; i++)
        {
            objects.Add(page);
            kids.Add($"{objects.Count} 0 R");
        }
        objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', kids)}] /Count {pages} >>";
        return MinimalPdf.Write(objects);
    }
}
```

- [ ] **Step 2: Write the failing tests**

`CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs`. Tasks 5 and 6 add to it.

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Pdf.Services;
using Avalonia.Input;
using Avalonia.VisualTree;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Tests.Windows;
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

    private sealed class FailingPrinter : IPrintService
    {
        public bool IsSupported => true;

        public Task PrintAsync(IPdfDocument document, PrintOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No printer");

        public Task ShowPrintPreviewAsync(IPdfDocument document, PrintOptions? options = null) => Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Run them to see them fail**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~DocumentToolbarTests"
```

Expected: the build fails: `DocumentWindow` has no `Toolbar` and no `OnFind`.

- [ ] **Step 4: Write the toolbar's XAML**

`CoffeePdf/Toolbar/DocumentToolbar.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:pdfc="using:Avalonia.Controls.Pdf.Controls"
             x:Class="CoffeePdf.Toolbar.DocumentToolbar">
  <!-- CoffeePDF's own toolbar (toolbar spec §3, §4), in place of the PdfViewer's: it drives the window's viewer through
       its public API only. The tools, in the middle, collapse into the More button when the window is narrow. -->
  <UserControl.Styles>
    <Style Selector=":is(Button).bar, SplitButton.bar">
      <Setter Property="Background" Value="Transparent" />
      <Setter Property="BorderThickness" Value="0" />
      <Setter Property="Padding" Value="5" />
      <Setter Property="MinWidth" Value="30" />
      <Setter Property="VerticalAlignment" Value="Center" />
    </Style>
    <Style Selector=":is(Button).bar PathIcon, SplitButton.bar PathIcon">
      <Setter Property="Width" Value="18" />
      <Setter Property="Height" Value="18" />
    </Style>
  </UserControl.Styles>
  <Border Padding="8,4" BorderThickness="0,0,0,1" BorderBrush="{DynamicResource SystemControlForegroundBaseLowBrush}">
    <Grid ColumnDefinitions="Auto,*,Auto">
      <StackPanel x:Name="LeftGroup" Orientation="Horizontal" Spacing="4">
        <ToggleButton x:Name="SidebarButton" Classes="bar" ToolTip.Tip="Sidebar" AutomationProperties.Name="Sidebar" Click="OnSidebar">
          <PathIcon Data="{StaticResource Icon.PanelLeft}" />
        </ToggleButton>
        <TextBox x:Name="PageBox" Width="48" VerticalAlignment="Center" TextAlignment="Right"
                 ToolTip.Tip="Page" AutomationProperties.Name="Page" KeyDown="OnPageKeyDown" LostFocus="OnPageLostFocus" />
        <TextBlock x:Name="PageCountText" VerticalAlignment="Center" />
        <TextBox x:Name="ZoomBox" Width="64" Margin="8,0,0,0" VerticalAlignment="Center" TextAlignment="Right"
                 ToolTip.Tip="Zoom" AutomationProperties.Name="Zoom" KeyDown="OnZoomKeyDown" LostFocus="OnZoomLostFocus" />
        <DropDownButton x:Name="ZoomMenuButton" Classes="bar" ToolTip.Tip="Zoom Options" AutomationProperties.Name="Zoom Options">
          <DropDownButton.Flyout>
            <MenuFlyout x:Name="ZoomMenu" Placement="BottomEdgeAlignedLeft" />
          </DropDownButton.Flyout>
        </DropDownButton>
      </StackPanel>
      <pdfc:OverflowToolbarPanel x:Name="Tools" Grid.Column="1" Spacing="2" Margin="12,0" HorizontalAlignment="Center" />
      <StackPanel x:Name="RightGroup" Grid.Column="2" Orientation="Horizontal" Spacing="4">
        <TextBox x:Name="SearchBox" Width="210" VerticalAlignment="Center" PlaceholderText="Search"
                 ToolTip.Tip="Search (⌘F)" AutomationProperties.Name="Search"
                 KeyDown="OnSearchKeyDown" TextChanged="OnSearchTextChanged">
          <TextBox.InnerLeftContent>
            <PathIcon Data="{StaticResource Icon.Search}" Width="14" Height="14" Margin="8,0,0,0" />
          </TextBox.InnerLeftContent>
          <TextBox.InnerRightContent>
            <StackPanel x:Name="SearchResults" Orientation="Horizontal" IsVisible="False">
              <TextBlock x:Name="SearchCount" VerticalAlignment="Center" Margin="0,0,2,0" />
              <Button x:Name="PreviousMatchButton" Classes="bar" ToolTip.Tip="Find Previous (⇧⌘G)" AutomationProperties.Name="Find Previous" Click="OnPreviousMatch">
                <PathIcon Data="{StaticResource Icon.ChevronUp}" />
              </Button>
              <Button x:Name="NextMatchButton" Classes="bar" ToolTip.Tip="Find Next (⌘G)" AutomationProperties.Name="Find Next" Click="OnNextMatch">
                <PathIcon Data="{StaticResource Icon.ChevronDown}" />
              </Button>
            </StackPanel>
          </TextBox.InnerRightContent>
        </TextBox>
        <Button x:Name="SearchOptionsButton" Classes="bar" ToolTip.Tip="Search Options" AutomationProperties.Name="Search Options">
          <PathIcon Data="{StaticResource Icon.Options}" />
          <Button.Flyout>
            <Flyout Placement="BottomEdgeAlignedRight" Opening="OnSearchOptionsOpening">
              <StackPanel Spacing="4">
                <CheckBox x:Name="MatchCaseBox" Content="Match Case" IsCheckedChanged="OnMatchCaseChanged" />
                <CheckBox x:Name="WholeWordBox" Content="Whole Word" IsCheckedChanged="OnWholeWordChanged" />
              </StackPanel>
            </Flyout>
          </Button.Flyout>
        </Button>
        <Button x:Name="PrintButton" Classes="bar" ToolTip.Tip="Print (⌘P)" AutomationProperties.Name="Print" Click="OnPrint">
          <PathIcon Data="{StaticResource Icon.Print}" />
        </Button>
      </StackPanel>
    </Grid>
  </Border>
</UserControl>
```

- [ ] **Step 5: Write the toolbar's code**

`CoffeePdf/Toolbar/DocumentToolbar.axaml.cs`. If probe 6 found `CurrentSearchResultIndex` counting from 1, not 0, drop
the `+ 1` in `SearchCountText`.

```csharp
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CoffeePdf.Windows;

namespace CoffeePdf.Toolbar;

/// <summary>
/// CoffeePDF's own toolbar (toolbar spec §3, §4), in place of the PdfViewer's, which stays hidden. It touches the viewer
/// only through its public API: it shows the viewer's properties as they change, and sets them as the person acts.
/// </summary>
public partial class DocumentToolbar : UserControl
{
    private PdfViewer? _viewer;
    private SidebarMode _sidebar = SidebarMode.Thumbnails; // the tab the sidebar button brings back (spec §3)
    private bool _showingSearchOptions;                    // the boxes are being set from the viewer, not by a person

    public DocumentToolbar()
    {
        InitializeComponent();
        FillZoomMenu();
    }

    /// <summary>Print, as the window does it, so a failure is reported, not lost (spec §4).</summary>
    internal Func<Task> Print { get; set; } = () => Task.CompletedTask;

    private PdfViewer Viewer => _viewer ?? throw new InvalidOperationException("The toolbar has no viewer yet");

    /// <summary>Drives <paramref name="viewer"/> from now on. The window calls it once, as it's built.</summary>
    public void Attach(PdfViewer viewer)
    {
        _viewer = viewer;
        viewer.PropertyChanged += OnViewerPropertyChanged;
        ShowSidebar();
        ShowSearch();
        ShowAvailability();
    }

    /// <summary>Puts the cursor in the search box, for Edit › Find… (spec §4).</summary>
    internal void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PdfViewer.CurrentPageProperty || e.Property == PdfViewer.PageCountProperty)
            ShowPage();
        else if (e.Property == PdfViewer.ZoomLevelProperty)
            ShowZoom();
        else if (e.Property == PdfViewer.SidebarModeProperty)
            ShowSidebar();
        else if (e.Property == PdfViewer.SearchResultCountProperty || e.Property == PdfViewer.CurrentSearchResultIndexProperty)
            ShowSearch();
        else if (e.Property == PdfViewer.HasDocumentProperty || e.Property == PdfViewer.CanEditAnnotationsProperty)
            ShowAvailability();
    }

    /// <summary>Until a document loads, only the sidebar button is available (spec §5).</summary>
    private void ShowAvailability()
    {
        var open = Viewer.HasDocument;
        PageBox.IsEnabled = ZoomBox.IsEnabled = ZoomMenuButton.IsEnabled = open;
        SearchBox.IsEnabled = SearchOptionsButton.IsEnabled = PrintButton.IsEnabled = open;
        ShowPage();
        ShowZoom();
    }

    // --- The sidebar --------------------------------------------------------------------------------------------------

    private void ShowSidebar()
    {
        if (Viewer.SidebarMode != SidebarMode.None)
            _sidebar = Viewer.SidebarMode;
        SidebarButton.IsChecked = Viewer.SidebarMode != SidebarMode.None;
    }

    private void OnSidebar(object? sender, RoutedEventArgs e)
    {
        Viewer.SidebarMode = Viewer.SidebarMode == SidebarMode.None ? _sidebar : SidebarMode.None;
        ShowSidebar(); // the button flipped itself; the viewer's mode decides what it shows
    }

    // --- The page box -------------------------------------------------------------------------------------------------

    private void ShowPage()
    {
        PageCountText.Text = Viewer.HasDocument ? $"of {Viewer.PageCount}" : "";
        if (!PageBox.IsKeyboardFocusWithin) // never over what's being typed
            PageBox.Text = Viewer.HasDocument ? Viewer.CurrentPage.ToString(CultureInfo.InvariantCulture) : "";
    }

    /// <summary>Goes to the typed page, clamped, or puts back the page shown before (spec §5).</summary>
    private void CommitPage()
    {
        var page = ToolbarRules.ParsePage(PageBox.Text, Viewer.PageCount);
        if (page is { } typed)
            Viewer.GoToPage(typed);
        PageBox.Text = (page ?? Viewer.CurrentPage).ToString(CultureInfo.InvariantCulture);
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CommitPage();
        else if (e.Key == Key.Escape)
        {
            PageBox.Text = Viewer.CurrentPage.ToString(CultureInfo.InvariantCulture); // so LostFocus commits nothing new
            Viewer.FocusViewer();
        }
        else
            return;
        e.Handled = true;
    }

    private void OnPageLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_viewer is { HasDocument: true })
            CommitPage();
    }

    // --- The zoom box and its menu --------------------------------------------------------------------------------------

    private void ShowZoom()
    {
        if (!ZoomBox.IsKeyboardFocusWithin)
            ZoomBox.Text = Viewer.HasDocument ? ToolbarRules.ZoomText(Viewer.ZoomLevel) : "";
    }

    private void SetZoom(double level)
    {
        Viewer.ZoomMode = PdfZoomMode.Manual;
        Viewer.ZoomLevel = level;
    }

    /// <summary>Sets the typed zoom, clamped, or puts back the zoom shown before (spec §5).</summary>
    private void CommitZoom()
    {
        if (ToolbarRules.ParseZoom(ZoomBox.Text, Viewer.MinZoom, Viewer.MaxZoom) is { } level)
            SetZoom(level);
        ZoomBox.Text = ToolbarRules.ZoomText(Viewer.ZoomLevel);
    }

    private void OnZoomKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CommitZoom();
        else if (e.Key == Key.Escape)
        {
            ZoomBox.Text = ToolbarRules.ZoomText(Viewer.ZoomLevel); // so LostFocus commits nothing new
            Viewer.FocusViewer();
        }
        else
            return;
        e.Handled = true;
    }

    private void OnZoomLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_viewer is { HasDocument: true })
            CommitZoom();
    }

    private void FillZoomMenu()
    {
        ZoomMenu.Items.Add(Item("Zoom In", () => Viewer.ZoomIn()));
        ZoomMenu.Items.Add(Item("Zoom Out", () => Viewer.ZoomOut()));
        ZoomMenu.Items.Add(new Separator());
        ZoomMenu.Items.Add(Item("Actual Size", () => Viewer.ResetZoom()));
        ZoomMenu.Items.Add(Item("Zoom to Fit", () => Viewer.FitPage()));
        ZoomMenu.Items.Add(Item("Fit Width", () => Viewer.FitWidth()));
        ZoomMenu.Items.Add(new Separator());
        foreach (var level in ToolbarRules.ZoomPresets)
            ZoomMenu.Items.Add(Item(ToolbarRules.ZoomText(level), () => SetZoom(level)));
    }

    private static MenuItem Item(string header, Action act) => new() { Header = header, Command = new ActionCommand(act) };

    // --- Search ---------------------------------------------------------------------------------------------------------

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewer is null)
            return; // the box is being built, before Attach
        if (string.IsNullOrEmpty(SearchBox.Text))
            Viewer.ClearSearch();
        else
            Viewer.SearchQuery = SearchBox.Text;
        ShowSearch();
    }

    private void ShowSearch()
    {
        SearchResults.IsVisible = !string.IsNullOrEmpty(SearchBox.Text);
        SearchCount.Text = SearchCountText(Viewer.SearchResultCount, Viewer.CurrentSearchResultIndex);
        PreviousMatchButton.IsEnabled = NextMatchButton.IsEnabled = Viewer.SearchResultCount > 0;
    }

    /// <summary>"3 of 17", or "None"; the viewer counts its index from 0 (the toolbar spec's §10, probe 6).</summary>
    private static string SearchCountText(int count, int index) => count == 0 ? "None" : $"{Math.Max(index, 0) + 1} of {count}";

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            Viewer.FindPrevious();
        else if (e.Key == Key.Enter)
            Viewer.FindNext();
        else if (e.Key == Key.Escape)
        {
            SearchBox.Text = ""; // TextChanged clears the viewer's search
            Viewer.FocusViewer();
        }
        else
            return;
        e.Handled = true;
    }

    private void OnPreviousMatch(object? sender, RoutedEventArgs e) => Viewer.FindPrevious();

    private void OnNextMatch(object? sender, RoutedEventArgs e) => Viewer.FindNext();

    private void OnSearchOptionsOpening(object? sender, EventArgs e)
    {
        _showingSearchOptions = true;
        MatchCaseBox.IsChecked = Viewer.SearchMatchCase;
        WholeWordBox.IsChecked = Viewer.SearchMatchWholeWord;
        _showingSearchOptions = false;
    }

    private void OnMatchCaseChanged(object? sender, RoutedEventArgs e)
    {
        if (!_showingSearchOptions)
            Viewer.SearchMatchCase = MatchCaseBox.IsChecked == true;
    }

    private void OnWholeWordChanged(object? sender, RoutedEventArgs e)
    {
        if (!_showingSearchOptions)
            Viewer.SearchMatchWholeWord = WholeWordBox.IsChecked == true;
    }

    // --- Print ----------------------------------------------------------------------------------------------------------

    private async void OnPrint(object? sender, RoutedEventArgs e) => await Print();
}
```

- [ ] **Step 6: Put the toolbar in the window, and hide the viewer's**

In `CoffeePdf/Windows/DocumentWindow.axaml`:

1. On the `<Window>` element, add `xmlns:toolbar="using:CoffeePdf.Toolbar"` after `xmlns:pdf`, and `MinWidth="720"`
   after `Height="760"`. (Task 5 measures the real minimum.)
2. In the Edit menu, before `<NativeMenuItem Header="Find Next" …/>`, add:

   ```xml
          <NativeMenuItem Header="Find…" Gesture="Meta+F" Click="OnFind" />
   ```

3. In `DocumentPanel`, after the Healdan bar's `</Border>`, add:

   ```xml
    <!-- CoffeePDF's own toolbar (toolbar spec §4), under the Healdan bar; the PdfViewer's own stays hidden. -->
    <toolbar:DocumentToolbar x:Name="Toolbar" DockPanel.Dock="Top" />
   ```

4. On `<pdf:PdfViewer x:Name="Viewer" …>`, add `IsToolbarVisible="False"` after `IsRedactToolVisible="False"`, and add
   to the comment above it, after "AutoSave stays off too (spec §6.4): each edit would otherwise become a save.": "Its
   toolbar is off: CoffeePDF's own, above, drives it (toolbar spec)."

In `CoffeePdf/Windows/DocumentWindow.axaml.cs`, in the constructor, right after `InitializeComponent();`:

```csharp
        // CoffeePDF's own toolbar drives the viewer, whose own is hidden (toolbar spec §4). Its Print goes through the
        // window's, so a failure is reported.
        Toolbar.Attach(Viewer);
        Toolbar.Print = PrintAndReportAsync;
```

and beside `OnFindNext`:

```csharp
    internal void OnFind(object? sender, EventArgs e) => Toolbar.FocusSearch();
```

- [ ] **Step 7: Move the menu tests to the toolbar's search box**

In `CoffeePdf.Tests/Windows/MenuTests.cs`:

1. In `Shortcuts`, add `("Find…", "Meta+F"),` before `("Find Next", "Meta+G")`.
2. In `Undo_in_a_focused_text_field_undoes_the_typing_not_the_markup`, the box was the PdfViewer's own search box,
   which is now hidden. Replace
   `var search = window.Viewer.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible);` with
   `var search = window.Toolbar.SearchBox;`, and change the test's comment to "The owner's check 10: Edit › Undo's ⌘Z
   reached the menu first and undid a note while the search box had focus. The search box is now the toolbar's."
3. Remove `using Avalonia.VisualTree;` if nothing else in the file uses it.

- [ ] **Step 8: Run the toolbar's and the window's tests**

```bash
dotnet build CoffeePdf.sln
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~DocumentToolbarTests|FullyQualifiedName~MenuTests|FullyQualifiedName~DocumentWindow"
```

Expected: all pass with the key. If `Search_counts_its_matches_and_Escape_clears_it` times out with
`SearchResultCount` at 0, setting `SearchQuery` doesn't search: call `Viewer.SearchAsync(SearchBox.Text, new
SearchOptions { … })` there instead, with the options probe 6 names, and record it in §10.

- [ ] **Step 9: Run everything**

```bash
dotnet test CoffeePdf.sln
```

Expected: all pass. A test that looked up the PdfViewer's own toolbar fails here: move it to the toolbar's part, as in
Step 7.

- [ ] **Step 10: Commit**

```bash
git add CoffeePdf/Toolbar/DocumentToolbar.axaml CoffeePdf/Toolbar/DocumentToolbar.axaml.cs CoffeePdf/Windows/DocumentWindow.axaml CoffeePdf/Windows/DocumentWindow.axaml.cs CoffeePdf.Tests/Fixtures/PagesPdf.cs CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs CoffeePdf.Tests/Windows/MenuTests.cs
git commit -m "Hide the PdfViewer's toolbar for CoffeePDF's own: sidebar, page, zoom, search and Print" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: the tools, and their overflow (spec §3 centre, §4, §5)

**Files:**
- Modify: `CoffeePdf/Toolbar/DocumentToolbar.axaml`
- Modify: `CoffeePdf/Toolbar/DocumentToolbar.axaml.cs`
- Modify: `CoffeePdf/Windows/DocumentWindow.axaml` (`MinWidth`, as measured)
- Test: `CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs`

**Interfaces:**
- Consumes: Task 4's `DocumentToolbar`; `ToolbarRules.Shapes`, `WithLabelFirst` (Task 2).
- Produces: `internal void Arm(PdfViewerTool tool)`, `internal void ChooseShape(ShapeType shape)`,
  `internal void ChooseStampLabel(string label)`, `internal Control ButtonFor(PdfViewerTool tool)`,
  `private void ShowTools()` (Task 6 adds the format menus to it), and the parts `ShapesButton`, `ShapesMenu`,
  `StampButton`, `StampFlyout`, `StampLabelList`, `StampStyleBox`, `StampDateBox`, `MoreButton`, `MoreMenu`.

- [ ] **Step 1: Write the failing tests**

Add to `DocumentToolbarTests`:

```csharp
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
            await TestWindows.WaitForAsync(() => window.Toolbar.Tools.OverflowedChildren.Count == 0);
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
            double X(Control c, double at) => c.TranslatePoint(new Avalonia.Point(at, 0), t)!.Value.X;
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
```

Add `using Avalonia.Automation;`, `using Avalonia.Controls.Primitives;` and `using Avalonia.LogicalTree;` to the file's
usings.

- [ ] **Step 2: Run them to see them fail**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~DocumentToolbarTests"
```

Expected: the build fails: no `ButtonFor`, `Arm`, `ShapesMenu` and the rest.

- [ ] **Step 3: Add the tools to the XAML**

In `CoffeePdf/Toolbar/DocumentToolbar.axaml`, replace the empty
`<pdfc:OverflowToolbarPanel x:Name="Tools" … />` with:

```xml
      <pdfc:OverflowToolbarPanel x:Name="Tools" Grid.Column="1" Spacing="2" Margin="12,0" HorizontalAlignment="Center">
        <!-- The tools, in the Tools menu's order (spec §3). Each Tag is a PdfViewerTool's name. -->
        <ToggleButton Classes="bar tool" Tag="Highlight" ToolTip.Tip="Highlight (⌃⌘H)" AutomationProperties.Name="Highlight" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Highlight}" />
        </ToggleButton>
        <ToggleButton Classes="bar tool" Tag="Underline" ToolTip.Tip="Underline (⌃⌘U)" AutomationProperties.Name="Underline" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Underline}" />
        </ToggleButton>
        <ToggleButton Classes="bar tool" Tag="Strikeout" ToolTip.Tip="Strikethrough (⌃⌘S)" AutomationProperties.Name="Strikethrough" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Strikethrough}" />
        </ToggleButton>
        <ToggleButton Classes="bar tool" Tag="Squiggly" ToolTip.Tip="Squiggly" AutomationProperties.Name="Squiggly" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Squiggly}" />
        </ToggleButton>
        <ToggleButton Classes="bar tool" Tag="Draw" ToolTip.Tip="Draw" AutomationProperties.Name="Draw" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Draw}" />
        </ToggleButton>
        <ToggleButton Classes="bar tool" Tag="Text" ToolTip.Tip="Text (⌃⌘T)" AutomationProperties.Name="Text" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Text}" />
        </ToggleButton>
        <ToggleSplitButton x:Name="ShapesButton" Classes="bar tool" Tag="Shape" ToolTip.Tip="Shapes" AutomationProperties.Name="Shapes" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Shapes}" />
          <ToggleSplitButton.Flyout>
            <MenuFlyout x:Name="ShapesMenu" Placement="BottomEdgeAlignedLeft" />
          </ToggleSplitButton.Flyout>
        </ToggleSplitButton>
        <ToggleButton Classes="bar tool" Tag="Note" ToolTip.Tip="Note (⌃⌘N)" AutomationProperties.Name="Note" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Note}" />
        </ToggleButton>
        <ToggleButton Classes="bar tool" Tag="Link" ToolTip.Tip="Link" AutomationProperties.Name="Link" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Link}" />
        </ToggleButton>
        <ToggleSplitButton x:Name="StampButton" Classes="bar tool" Tag="Stamp" ToolTip.Tip="Stamp" AutomationProperties.Name="Stamp" Click="OnTool">
          <PathIcon Data="{StaticResource Icon.Stamp}" />
          <ToggleSplitButton.Flyout>
            <Flyout x:Name="StampFlyout" Placement="BottomEdgeAlignedLeft" Opening="OnStampOpening">
              <StackPanel Spacing="8" Width="220">
                <ListBox x:Name="StampLabelList" MaxHeight="260" AutomationProperties.Name="Stamp Labels" SelectionChanged="OnStampLabelChosen" />
                <ComboBox x:Name="StampStyleBox" HorizontalAlignment="Stretch" AutomationProperties.Name="Stamp Style" ToolTip.Tip="Stamp Style" SelectionChanged="OnStampStyleChosen" />
                <CheckBox x:Name="StampDateBox" Content="Include Date" IsCheckedChanged="OnStampDateChanged" />
              </StackPanel>
            </Flyout>
          </ToggleSplitButton.Flyout>
        </ToggleSplitButton>
        <!-- The tools that don't fit, when the window is narrow (spec §3). -->
        <Button x:Name="MoreButton" Classes="bar" pdfc:OverflowToolbarPanel.IsOverflowIndicator="True" ToolTip.Tip="More Tools" AutomationProperties.Name="More Tools">
          <PathIcon Data="{StaticResource Icon.More}" />
          <Button.Flyout>
            <MenuFlyout x:Name="MoreMenu" Placement="BottomEdgeAlignedLeft" />
          </Button.Flyout>
        </Button>
      </pdfc:OverflowToolbarPanel>
```

- [ ] **Step 4: Add the tools' code**

In `CoffeePdf/Toolbar/DocumentToolbar.axaml.cs`, add `using Avalonia.Automation;`, `using Avalonia.Controls.Primitives;`
and `using Avalonia.Media;`, and these fields beside `_sidebar`:

```csharp
    private static readonly PdfStampStyle[] StampStyles = Enum.GetValues<PdfStampStyle>();
    private IReadOnlyList<string> _stampLabels = []; // the viewer's labels as they first were, in the menu's order
    private bool _showingStamp;                      // the stamp menu is being set from the viewer, not by a person
```

In the constructor, after `FillZoomMenu();`:

```csharp
        foreach (var shape in ToolbarRules.Shapes)
            ShapesMenu.Items.Add(Item(shape.ToString(), () => ChooseShape(shape)));
        Tools.OverflowChanged += (_, _) => FillMoreMenu();
```

In `Attach`, after `_viewer = viewer;`:

```csharp
        _stampLabels = viewer.StampLabels;
        ShowStamp();
```

and after `ShowAvailability();` at its end:

```csharp
        ShowTools();
```

In `OnViewerPropertyChanged`, add a first branch:

```csharp
        if (e.Property == PdfViewer.ActiveToolProperty || e.Property == PdfViewer.SelectedShapeTypeProperty)
            ShowTools();
        else if …
```

(the existing `if` becomes `else if`).

At the end of `ShowAvailability`:

```csharp
        // The tools hide while the window may not be marked up, as the built-in toolbar's did, and no hidden tool stays
        // armed (spec §4, §5).
        Tools.IsVisible = Viewer.CanEditAnnotations;
        Tools.IsEnabled = open;
        if (!Viewer.CanEditAnnotations && Viewer.ActiveTool != PdfViewerTool.None)
            Viewer.ActiveTool = PdfViewerTool.None;
```

And a new section, before the Print section:

```csharp
    // --- The tools ------------------------------------------------------------------------------------------------------

    private IEnumerable<Control> ToolButtons => Tools.Children.Where(c => c.Classes.Contains("tool"));

    private static PdfViewerTool ToolOf(Control button) => Enum.Parse<PdfViewerTool>((string)button.Tag!);

    /// <summary>The button for <paramref name="tool"/>, for the tests.</summary>
    internal Control ButtonFor(PdfViewerTool tool) => ToolButtons.Single(b => ToolOf(b) == tool);

    /// <summary>Arms <paramref name="tool"/>, or disarms it when it's armed already, so text selection comes back (spec §3).</summary>
    internal void Arm(PdfViewerTool tool)
    {
        Viewer.ActiveTool = Viewer.ActiveTool == tool ? PdfViewerTool.None : tool;
        ShowTools();
    }

    private void OnTool(object? sender, RoutedEventArgs e) => Arm(ToolOf((Control)sender!));

    /// <summary>Shows which tool is armed, however it was armed: here, the Tools menu, or the viewer itself.</summary>
    private void ShowTools()
    {
        foreach (var button in ToolButtons)
        {
            var armed = Viewer.ActiveTool == ToolOf(button);
            if (button is ToggleButton toggle)
                toggle.IsChecked = armed;
            else if (button is ToggleSplitButton split)
                split.IsChecked = armed;
        }
    }

    internal void ChooseShape(ShapeType shape)
    {
        Viewer.SelectedShapeType = shape;
        Viewer.ActiveTool = PdfViewerTool.Shape;
    }

    /// <summary>
    /// Arms the Stamp tool with <paramref name="label"/>. Nothing public chooses the label the tool places, which is the
    /// first in StampLabels, so the label goes first there (spec §6, probe 3).
    /// </summary>
    internal void ChooseStampLabel(string label)
    {
        Viewer.StampLabels = ToolbarRules.WithLabelFirst(_stampLabels, label);
        Viewer.ActiveTool = PdfViewerTool.Stamp;
    }

    private void ShowStamp()
    {
        _showingStamp = true;
        StampLabelList.ItemsSource = _stampLabels;
        StampLabelList.SelectedItem = Viewer.StampLabels.FirstOrDefault();
        StampStyleBox.ItemsSource = StampStyles.Select(PdfViewer.StampStyleDisplayName).ToList();
        StampStyleBox.SelectedIndex = Array.IndexOf(StampStyles, Viewer.StampStyle);
        StampDateBox.IsChecked = Viewer.IncludeStampDate;
        _showingStamp = false;
    }

    private void OnStampOpening(object? sender, EventArgs e) => ShowStamp();

    private void OnStampLabelChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (_showingStamp || StampLabelList.SelectedItem is not string label)
            return;
        ChooseStampLabel(label);
        StampFlyout.Hide();
    }

    private void OnStampStyleChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (!_showingStamp && StampStyleBox.SelectedIndex >= 0)
            Viewer.StampStyle = StampStyles[StampStyleBox.SelectedIndex];
    }

    private void OnStampDateChanged(object? sender, RoutedEventArgs e)
    {
        if (!_showingStamp)
            Viewer.IncludeStampDate = StampDateBox.IsChecked == true;
    }

    /// <summary>
    /// Lists the tools that don't fit in the More menu, rebuilt each time the panel says which (spec §4). Shapes and Stamp
    /// bring their own menus as submenus.
    /// </summary>
    private void FillMoreMenu()
    {
        MoreMenu.Items.Clear();
        foreach (var button in Tools.OverflowedChildren.Where(c => c.Classes.Contains("tool")))
        {
            var tool = ToolOf(button);
            var item = new MenuItem
            {
                Header = AutomationProperties.GetName(button),
                Icon = new PathIcon { Data = ((button as ContentControl)?.Content as PathIcon)?.Data },
            };
            if (tool == PdfViewerTool.Shape)
                foreach (var shape in ToolbarRules.Shapes)
                    item.Items.Add(Item(shape.ToString(), () => ChooseShape(shape)));
            else if (tool == PdfViewerTool.Stamp)
                foreach (var label in _stampLabels)
                    item.Items.Add(Item(label, () => ChooseStampLabel(label)));
            else
                item.Command = new ActionCommand(() => Arm(tool));
            MoreMenu.Items.Add(item);
        }
    }
```

- [ ] **Step 5: Run the tests**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~DocumentToolbarTests"
```

Expected: all pass, except possibly `At_its_minimum_width_everything_but_the_tools_fits`.

- [ ] **Step 6: Measure the window's minimum width**

If `At_its_minimum_width_everything_but_the_tools_fits` fails, raise `MinWidth` in `DocumentWindow.axaml` by 20 until
it passes. If it passes at 720, lower it by 20 until it fails, and keep the last that passed. Note the value for
Task 7's findings. Then run the toolbar's tests again.

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~At_its_minimum_width"
```

- [ ] **Step 7: Run everything**

```bash
dotnet test CoffeePdf.sln
```

Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add CoffeePdf/Toolbar/DocumentToolbar.axaml CoffeePdf/Toolbar/DocumentToolbar.axaml.cs CoffeePdf/Windows/DocumentWindow.axaml CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs
git commit -m "Add the tools to CoffeePDF's toolbar, with Shapes, Stamp and a More menu for narrow windows" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: the format menus (spec §3's table, §4)

**Files:**
- Create: `CoffeePdf/Toolbar/ColourSlot.cs`
- Create: `CoffeePdf/Toolbar/ColourPicker.cs`
- Create: `CoffeePdf/Toolbar/LinePicker.cs`
- Create: `CoffeePdf/Toolbar/TextFormatPicker.cs`
- Modify: `CoffeePdf/Toolbar/DocumentToolbar.axaml`
- Modify: `CoffeePdf/Toolbar/DocumentToolbar.axaml.cs`
- Test: `CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs`

**Interfaces:**
- Consumes: `ToolbarRules.For`, `Swatches`, `Fonts`, `FontSizes`, `ParseHex`, `Hex` (Task 2); `ShowTools` and `Arm`
  (Task 5); the `Icon.*` resources (Task 3).
- Produces: `internal sealed record ColourSlot(string Name, bool AllowsNone, Func<PdfAnnotationColor?> Get, Action<PdfAnnotationColor?> Set)`;
  `public sealed class ColourPicker : UserControl` with `internal void Show(IReadOnlyList<ColourSlot>)`,
  `internal void ShowSlot(int)`, `internal void Choose(PdfAnnotationColor?)`, `internal void ChooseHex(string)`,
  `public event EventHandler? Chosen`; `public sealed class LinePicker : UserControl` with
  `internal void Show(Func<float>, Action<float>)` and `internal void Choose(double)`;
  `public sealed class TextFormatPicker : UserControl` with `internal void Show(PdfViewer)` and the parts
  `FontBox`, `SizeBox`, `BoldButton`, `ItalicButton`, `UnderlineButton`, `AlignLeftButton`, `AlignCentreButton`,
  `AlignRightButton`; on the toolbar, `internal PageSelection Selection()`, `internal IReadOnlyList<ColourSlot> ColourSlots()`,
  `internal void ShowColourMenu()`, `internal void ShowLineMenu()`, `internal void ShowTextMenu()`, and the parts
  `FormatGroup`, `ColourButton`, `ColourSwatch`, `Colours`, `LineButton`, `LineWidths`, `TextButton`, `TextFormat`.

- [ ] **Step 1: Write the failing tests**

Add to `DocumentToolbarTests`:

```csharp
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

        var (fromSwatch, typed, swatchShown) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var t = window.Toolbar;
            t.Arm(PdfViewerTool.Highlight);
            t.ShowColourMenu();
            t.Colours.Choose(CoffeePdf.Toolbar.ToolbarRules.Swatches[2]);
            var fromSwatch = window.Viewer.HighlightColor;
            t.Colours.ChooseHex("#112233");
            var shown = (t.ColourSwatch.Fill as Avalonia.Media.ISolidColorBrush)?.Color;
            return (fromSwatch, window.Viewer.HighlightColor, shown);
        });

        Assert.Equal(new PdfAnnotationColor(0, 122, 255, 255), fromSwatch);
        Assert.Equal(new PdfAnnotationColor(0x11, 0x22, 0x33, 255), typed);
        Assert.Equal(Avalonia.Media.Color.FromRgb(0x11, 0x22, 0x33), swatchShown);
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
            Drag(window, OnPage(v, window, 200, 500), OnPage(v, window, 300, 420));
            v.ActiveTool = PdfViewerTool.None; // so what shows is the selection's, not the tool's
            Click(window, OnPage(v, window, 200, 460));
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
```

Add `using Avalonia.Interactivity;` to the file's usings.

- [ ] **Step 2: Run them to see them fail**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~DocumentToolbarTests"
```

Expected: the build fails: no `FormatGroup`, `Colours`, `ColourPicker` and the rest.

- [ ] **Step 3: Write the colour slot and the three pickers**

`CoffeePdf/Toolbar/ColourSlot.cs`:

```csharp
using Avalonia.Controls.Pdf.Core;

namespace CoffeePdf.Toolbar;

/// <summary>
/// One colour the Colour menu edits (toolbar spec §3): its tab's name, whether it may be none (a fill or a border), and
/// the viewer's property it reads and sets.
/// </summary>
internal sealed record ColourSlot(string Name, bool AllowsNone, Func<PdfAnnotationColor?> Get, Action<PdfAnnotationColor?> Set);
```

`CoffeePdf/Toolbar/ColourPicker.cs`:

```csharp
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoffeePdf.Toolbar;

/// <summary>
/// The Colour menu (toolbar spec §3): a tab for each colour the markup carries, the swatches, No Colour where the colour
/// may be none, and a typed hex colour.
/// </summary>
public sealed class ColourPicker : UserControl
{
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly WrapPanel _swatches = new() { Width = 8 * 28 };
    private readonly Button _none = new() { Content = "No Colour", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _hex = new() { PlaceholderText = "#RRGGBB", Width = 110 };
    private IReadOnlyList<ColourSlot> _slots = [];
    private int _slot;

    public ColourPicker()
    {
        foreach (var colour in ToolbarRules.Swatches)
        {
            var swatch = new Button { Width = 24, Height = 24, Margin = new(2), Padding = new(0), Background = Brush(colour) };
            AutomationProperties.SetName(swatch, ToolbarRules.Hex(colour));
            ToolTip.SetTip(swatch, ToolbarRules.Hex(colour));
            swatch.Click += (_, _) => Choose(colour);
            _swatches.Children.Add(swatch);
        }
        _none.Click += (_, _) => Choose(null);
        AutomationProperties.SetName(_none, "No Colour");
        ToolTip.SetTip(_none, "No Colour");
        AutomationProperties.SetName(_hex, "Hex Colour");
        ToolTip.SetTip(_hex, "Hex Colour");
        _hex.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            ChooseHex(_hex.Text ?? "");
            e.Handled = true;
        };
        Content = new StackPanel { Spacing = 8, Children = { _tabs, _swatches, _none, _hex } };
    }

    /// <summary>Raised after a colour is chosen, so the toolbar's button can show it.</summary>
    public event EventHandler? Chosen;

    /// <summary>Shows <paramref name="slots"/>, with the first one's tab chosen.</summary>
    internal void Show(IReadOnlyList<ColourSlot> slots)
    {
        _slots = slots;
        _tabs.Children.Clear();
        _tabs.IsVisible = slots.Count > 1;
        for (var i = 0; i < slots.Count; i++)
        {
            var index = i;
            var tab = new ToggleButton { Content = slots[i].Name };
            AutomationProperties.SetName(tab, slots[i].Name);
            ToolTip.SetTip(tab, slots[i].Name);
            tab.Click += (_, _) => ShowSlot(index);
            _tabs.Children.Add(tab);
        }
        ShowSlot(0);
    }

    /// <summary>Chooses the tab of the slot at <paramref name="index"/>.</summary>
    internal void ShowSlot(int index)
    {
        _slot = index;
        var tabs = _tabs.Children.OfType<ToggleButton>().ToList();
        for (var i = 0; i < tabs.Count; i++)
            tabs[i].IsChecked = i == index;
        var slot = _slots.ElementAtOrDefault(index);
        _none.IsVisible = slot?.AllowsNone == true;
        _hex.Text = slot?.Get() is { } colour ? ToolbarRules.Hex(colour) : "";
    }

    /// <summary>Sets the chosen tab's colour; null is No Colour.</summary>
    internal void Choose(PdfAnnotationColor? colour)
    {
        if (_slots.ElementAtOrDefault(_slot) is not { } slot || (colour is null && !slot.AllowsNone))
            return;
        slot.Set(colour);
        ShowSlot(_slot);
        Chosen?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets the chosen tab's colour from typed text, or puts the box back when it isn't a colour.</summary>
    internal void ChooseHex(string text)
    {
        if (ToolbarRules.ParseHex(text) is { } colour)
            Choose(colour);
        else
            ShowSlot(_slot);
    }

    internal static IBrush Brush(PdfAnnotationColor colour) => new SolidColorBrush(Color.FromArgb(colour.A, colour.R, colour.G, colour.B));
}
```

`CoffeePdf/Toolbar/LinePicker.cs`:

```csharp
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoffeePdf.Toolbar;

/// <summary>The Line menu (toolbar spec §3): the width, 0.5 to 20 points, the viewer's own range, with a preview.</summary>
public sealed class LinePicker : UserControl
{
    private readonly Slider _width = new() { Minimum = 0.5, Maximum = 20, TickFrequency = 0.5, IsSnapToTickEnabled = true, Width = 180 };
    private readonly TextBlock _label = new();
    private readonly Border _preview = new() { Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Gray };
    private Action<float>? _set;
    private bool _showing; // the slider is being set from the viewer, not by a person

    public LinePicker()
    {
        AutomationProperties.SetName(_width, "Line Width");
        _width.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty)
                return;
            if (!_showing)
                _set?.Invoke((float)_width.Value);
            ShowPreview();
        };
        Content = new StackPanel { Spacing = 8, Children = { _width, _label, _preview } };
    }

    /// <summary>Shows the width <paramref name="get"/> reads, and sets new ones through <paramref name="set"/>.</summary>
    internal void Show(Func<float> get, Action<float> set)
    {
        _set = set;
        _showing = true;
        _width.Value = get();
        _showing = false;
        ShowPreview();
    }

    /// <summary>Sets <paramref name="width"/>, as the slider does.</summary>
    internal void Choose(double width) => _width.Value = width;

    private void ShowPreview()
    {
        _label.Text = $"{_width.Value:0.#} pt";
        _preview.Height = Math.Max(1, _width.Value);
    }
}
```

`CoffeePdf/Toolbar/TextFormatPicker.cs`:

```csharp
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoffeePdf.Toolbar;

/// <summary>The Text menu (toolbar spec §3): a text box's font, size, bold, italic and underline, and its alignment.</summary>
public sealed class TextFormatPicker : UserControl
{
    private PdfViewer? _viewer;
    private bool _showing; // the controls are being set from the viewer, not by a person

    public TextFormatPicker()
    {
        AutomationProperties.SetName(FontBox, "Font");
        AutomationProperties.SetName(SizeBox, "Size");
        FontBox.SelectionChanged += (_, _) => Set(v => { if (FontBox.SelectedItem is string font) v.TextAnnotationFont = font; });
        SizeBox.SelectionChanged += (_, _) => Set(v => { if (SizeBox.SelectedItem is float size) v.TextAnnotationFontSize = size; });
        foreach (var style in new[] { BoldButton, ItalicButton, UnderlineButton })
            style.IsCheckedChanged += (_, _) => Set(v => v.TextAnnotationFontAttributes = Attributes());
        AlignLeftButton.Click += (_, _) => Align(TextAnnotationAlignment.Left);
        AlignCentreButton.Click += (_, _) => Align(TextAnnotationAlignment.Center);
        AlignRightButton.Click += (_, _) => Align(TextAnnotationAlignment.Right);
        Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { FontBox, SizeBox } },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 2,
                    Children = { BoldButton, ItalicButton, UnderlineButton, new Border { Width = 8 }, AlignLeftButton, AlignCentreButton, AlignRightButton },
                },
            },
        };
    }

    internal ComboBox FontBox { get; } = new() { Width = 180 };
    internal ComboBox SizeBox { get; } = new() { Width = 80 };
    internal ToggleButton BoldButton { get; } = Toggle("Icon.Bold", "Bold");
    internal ToggleButton ItalicButton { get; } = Toggle("Icon.Italic", "Italic");
    internal ToggleButton UnderlineButton { get; } = Toggle("Icon.Underline", "Underline");
    internal ToggleButton AlignLeftButton { get; } = Toggle("Icon.AlignLeft", "Align Left");
    internal ToggleButton AlignCentreButton { get; } = Toggle("Icon.AlignCentre", "Centre");
    internal ToggleButton AlignRightButton { get; } = Toggle("Icon.AlignRight", "Align Right");

    /// <summary>Shows <paramref name="viewer"/>'s text format, which, with a text box selected, is that box's (spec §3).</summary>
    internal void Show(PdfViewer viewer)
    {
        _viewer = viewer;
        _showing = true;
        var font = viewer.TextAnnotationFont;
        FontBox.ItemsSource = ToolbarRules.Fonts.Contains(font) ? ToolbarRules.Fonts : [font, .. ToolbarRules.Fonts];
        FontBox.SelectedItem = font;
        var size = viewer.TextAnnotationFontSize;
        SizeBox.ItemsSource = ToolbarRules.FontSizes.Contains(size) ? ToolbarRules.FontSizes : [size, .. ToolbarRules.FontSizes];
        SizeBox.SelectedItem = size;
        BoldButton.IsChecked = viewer.TextAnnotationFontAttributes.HasFlag(FontAttributes.Bold);
        ItalicButton.IsChecked = viewer.TextAnnotationFontAttributes.HasFlag(FontAttributes.Italic);
        UnderlineButton.IsChecked = viewer.TextAnnotationFontAttributes.HasFlag(FontAttributes.Underline);
        ShowAlignment(viewer.TextAnnotationTextAlign);
        _showing = false;
    }

    private void Set(Action<PdfViewer> set)
    {
        if (!_showing && _viewer is { } viewer)
            set(viewer);
    }

    private FontAttributes Attributes() =>
        (BoldButton.IsChecked == true ? FontAttributes.Bold : FontAttributes.None)
        | (ItalicButton.IsChecked == true ? FontAttributes.Italic : FontAttributes.None)
        | (UnderlineButton.IsChecked == true ? FontAttributes.Underline : FontAttributes.None);

    private void Align(TextAnnotationAlignment alignment)
    {
        Set(v => v.TextAnnotationTextAlign = alignment);
        ShowAlignment(alignment);
    }

    private void ShowAlignment(TextAnnotationAlignment alignment)
    {
        AlignLeftButton.IsChecked = alignment == TextAnnotationAlignment.Left;
        AlignCentreButton.IsChecked = alignment == TextAnnotationAlignment.Center;
        AlignRightButton.IsChecked = alignment == TextAnnotationAlignment.Right;
    }

    private static ToggleButton Toggle(string icon, string name)
    {
        var button = new ToggleButton
        {
            Padding = new Thickness(5),
            Content = new PathIcon { Data = Application.Current?.FindResource(icon) as Geometry, Width = 16, Height = 16 },
        };
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        return button;
    }
}
```

`Align`'s handler is a `Click`, so the test's raised `Click` runs it with the flyout closed; `ShowAlignment` then sets
all three toggles from the alignment chosen, whichever way a toggle flipped itself.

- [ ] **Step 4: Add the format menus to the XAML**

In `CoffeePdf/Toolbar/DocumentToolbar.axaml`, add `xmlns:toolbar="using:CoffeePdf.Toolbar"` to the `<UserControl>`, and
inside `Tools`, after the More button:

```xml
        <!-- The armed tool's or selection's format menus (spec §3's table). Pinned, so they never overflow. -->
        <StackPanel x:Name="FormatGroup" Orientation="Horizontal" Spacing="2" Margin="8,0,0,0" IsVisible="False"
                    pdfc:OverflowToolbarPanel.IsPinned="True">
          <DropDownButton x:Name="ColourButton" Classes="bar" ToolTip.Tip="Colour" AutomationProperties.Name="Colour">
            <Ellipse x:Name="ColourSwatch" Width="14" Height="14" StrokeThickness="1"
                     Stroke="{DynamicResource SystemControlForegroundBaseMediumBrush}" />
            <DropDownButton.Flyout>
              <Flyout Placement="BottomEdgeAlignedLeft" Opening="OnColourOpening">
                <toolbar:ColourPicker x:Name="Colours" />
              </Flyout>
            </DropDownButton.Flyout>
          </DropDownButton>
          <DropDownButton x:Name="LineButton" Classes="bar" ToolTip.Tip="Line Width" AutomationProperties.Name="Line Width">
            <PathIcon Data="{StaticResource Icon.Line}" />
            <DropDownButton.Flyout>
              <Flyout Placement="BottomEdgeAlignedLeft" Opening="OnLineOpening">
                <toolbar:LinePicker x:Name="LineWidths" />
              </Flyout>
            </DropDownButton.Flyout>
          </DropDownButton>
          <DropDownButton x:Name="TextButton" Classes="bar" ToolTip.Tip="Text Format" AutomationProperties.Name="Text Format">
            <PathIcon Data="{StaticResource Icon.TextFormat}" />
            <DropDownButton.Flyout>
              <Flyout Placement="BottomEdgeAlignedLeft" Opening="OnTextOpening">
                <toolbar:TextFormatPicker x:Name="TextFormat" />
              </Flyout>
            </DropDownButton.Flyout>
          </DropDownButton>
        </StackPanel>
```

- [ ] **Step 5: Add the format menus' code**

In `CoffeePdf/Toolbar/DocumentToolbar.axaml.cs`, in the constructor after `Tools.OverflowChanged …`:

```csharp
        Colours.Chosen += (_, _) => ShowColourSwatch();
```

In `Attach`, after `viewer.PropertyChanged += OnViewerPropertyChanged;`:

```csharp
        viewer.Classes.CollectionChanged += (_, _) => ShowTools(); // :shape-selected and :editing-text come and go
```

At the end of `ShowTools`:

```csharp
        ShowFormatMenus();
```

And a new section after the tools' section:

```csharp
    // --- The format menus -----------------------------------------------------------------------------------------------

    /// <summary>
    /// What's selected on the page, from the viewer's states (spec §4; the toolbar spec's §10, probe 5 says what tells a
    /// text box from a shape).
    /// </summary>
    internal PageSelection Selection()
    {
        var states = Viewer.Classes;
        if (states.Contains(":editing-text") || (states.Contains(":shape-selected") && Viewer.SelectedShapeType == ShapeType.Text))
            return PageSelection.TextBox;
        return states.Contains(":shape-selected") ? PageSelection.Shape : PageSelection.None;
    }

    private void ShowFormatMenus()
    {
        var menus = ToolbarRules.For(Viewer.ActiveTool, Selection());
        ColourButton.IsVisible = menus.HasFlag(FormatMenus.Colour);
        LineButton.IsVisible = menus.HasFlag(FormatMenus.Line);
        TextButton.IsVisible = menus.HasFlag(FormatMenus.Text);
        FormatGroup.IsVisible = menus != FormatMenus.None;
        ShowColourSwatch();
    }

    /// <summary>The colours the Colour menu edits for the armed tool or the selection, which wins (spec §3's table).</summary>
    internal IReadOnlyList<ColourSlot> ColourSlots()
    {
        var v = Viewer;
        static ColourSlot Solid(string name, Func<PdfAnnotationColor> get, Action<PdfAnnotationColor> set) =>
            new(name, false, () => get(), c => { if (c is { } colour) set(colour); });
        return (Selection(), v.ActiveTool) switch
        {
            (PageSelection.TextBox, _) or (PageSelection.None, PdfViewerTool.Text) =>
            [
                new("Fill", true, () => v.TextAnnotationFillColor, c => v.TextAnnotationFillColor = c),
                new("Border", true, () => v.TextAnnotationStrokeColor, c => v.TextAnnotationStrokeColor = c),
                Solid("Text", () => v.TextAnnotationTextColor, c => v.TextAnnotationTextColor = c),
            ],
            (PageSelection.Shape, _) or (PageSelection.None, PdfViewerTool.Shape) =>
            [
                new("Fill", true, () => v.ShapeFillColor, c => v.ShapeFillColor = c),
                new("Stroke", true, () => v.ShapeStrokeColor, c => v.ShapeStrokeColor = c),
            ],
            (_, PdfViewerTool.Highlight) => [Solid("Colour", () => v.HighlightColor, c => v.HighlightColor = c)],
            (_, PdfViewerTool.Underline) => [Solid("Colour", () => v.UnderlineColor, c => v.UnderlineColor = c)],
            (_, PdfViewerTool.Strikeout) => [Solid("Colour", () => v.StrikeoutColor, c => v.StrikeoutColor = c)],
            (_, PdfViewerTool.Squiggly) => [Solid("Colour", () => v.SquigglyColor, c => v.SquigglyColor = c)],
            (_, PdfViewerTool.Note) => [Solid("Colour", () => v.NoteColor, c => v.NoteColor = c)],
            (_, PdfViewerTool.Draw) => [Solid("Colour", () => v.DrawStrokeColor, c => v.DrawStrokeColor = c)],
            // A stamp with no colour of its own takes its label's (PdfViewer.DefaultStampColorFor), so none isn't offered.
            (_, PdfViewerTool.Stamp) => [new("Colour", false, () => v.StampColor, c => v.StampColor = c)],
            _ => [],
        };
    }

    /// <summary>The Colour button shows the first colour its menu edits.</summary>
    private void ShowColourSwatch() =>
        ColourSwatch.Fill = ColourSlots().FirstOrDefault()?.Get() is { } colour ? ColourPicker.Brush(colour) : Brushes.Transparent;

    internal void ShowColourMenu() => Colours.Show(ColourSlots());

    internal void ShowLineMenu()
    {
        var (get, set) = LineWidth();
        LineWidths.Show(get, set);
    }

    /// <summary>The width the Line menu edits for the armed tool or the selection, which wins (spec §3's table).</summary>
    private (Func<float> Get, Action<float> Set) LineWidth()
    {
        var v = Viewer;
        return (Selection(), v.ActiveTool) switch
        {
            (PageSelection.TextBox, _) or (PageSelection.None, PdfViewerTool.Text) =>
                (() => v.TextAnnotationStrokeWidth, w => v.TextAnnotationStrokeWidth = w),
            (PageSelection.Shape, _) or (PageSelection.None, PdfViewerTool.Shape) =>
                (() => v.ShapeStrokeWidth, w => v.ShapeStrokeWidth = w),
            _ => (() => v.DrawStrokeWidth, w => v.DrawStrokeWidth = w),
        };
    }

    internal void ShowTextMenu() => TextFormat.Show(Viewer);

    private void OnColourOpening(object? sender, EventArgs e) => ShowColourMenu();

    private void OnLineOpening(object? sender, EventArgs e) => ShowLineMenu();

    private void OnTextOpening(object? sender, EventArgs e) => ShowTextMenu();
```

Add `using Avalonia.Controls.Pdf.Core;` to the file's usings, for `PdfAnnotationColor`.

- [ ] **Step 6: Run the tests**

```bash
dotnet test CoffeePdf.sln --filter "FullyQualifiedName~DocumentToolbarTests"
```

Expected: all pass. If `A_selected_shape_shows_its_format_menus_until_the_selection_ends` waits out its 10 seconds,
compare `Selection()` with what probe 5 recorded, and change it to match. If
`At_its_minimum_width_the_text_tools_format_menus_still_fit` fails, raise `MinWidth` by 20 until it passes, and run
Task 5's minimum-width test again.

- [ ] **Step 7: Run everything**

```bash
dotnet test CoffeePdf.sln
```

Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add CoffeePdf/Toolbar CoffeePdf/Windows/DocumentWindow.axaml CoffeePdf.Tests/Toolbar/DocumentToolbarTests.cs
git commit -m "Add the toolbar's Colour, Line and Text menus, for the armed tool or the selection" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: record the build, and package it for the owner's checks (spec §7, §8)

**Files:**
- Modify: `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` (§4.3)
- Modify: `docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md` (status, §10)
- Modify: `docs/ROADMAP.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Run every test, with the key and without it**

```bash
dotnet test CoffeePdf.sln
env -u AVALONIA_LICENSE_KEY dotnet test CoffeePdf.sln
```

Expected: with the key, all pass but the keyless probe, which skips. Without it, the rules' and icons' tests run and
pass, and the PdfViewer's tests skip. Note each run's counts.

- [ ] **Step 2: Point the v1 spec at this one**

In `docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md` §4.3, change the bullet
"  - the menu bar, with its Healdan menu, and the toolbar;" to
"  - the menu bar, with its Healdan menu, and CoffeePDF's own toolbar (`2026-11-05-coffeepdf-toolbar-design.md`);".

- [ ] **Step 3: Add the rule to `CLAUDE.md`**

After the line "- **The PdfViewer's `AutoSave` stays off** (spec §6.4).", add:

```markdown
- **The PdfViewer's own toolbar stays hidden** (`IsToolbarVisible`; the toolbar spec,
  `docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md`). `Toolbar/DocumentToolbar` drives the viewer through
  its public API only, and every button has an icon from `Toolbar/Icons.axaml` (written by `make-icons.sh`), an
  accessible name and a tooltip.
```

- [ ] **Step 4: Record the build in the toolbar spec**

In `docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md`, change **Status** to "agreed with the owner section
by section on 2026-11-05, and built on the date it's finished (`docs/superpowers/plans/2026-11-05-coffeepdf-toolbar.md`). The
owner's checks on the Mac are next (§7)." In §10, add a bullet "**The build (observed on the date it's finished).**" with: the
window's measured `MinWidth`; the test counts of Step 1; and anything a task changed from this plan because a probe or
a test said so (for example `Selection()`'s rule, the search index, or how search starts).

- [ ] **Step 5: Update the roadmap**

In `docs/ROADMAP.md`'s "After v1.0" table, set the toolbar's status to "designed 2026-11-05; built on that date; the
owner's checks on the Mac next".

- [ ] **Step 6: Build a package for the owner's checks**

```bash
packaging/mac/package.sh 1.1.0-toolbar.1
```

Expected: the app and its `.dmg` in `packaging/mac/out/`, with `Contents/Resources/THIRD-PARTY-NOTICES.md` in the app.
A build with a signing identity set is signed with it; without one, it's ad-hoc, which launches on the Mac that built
it (v1 spec §16 Decision 4).

```bash
ls packaging/mac/out/CoffeePDF.app/Contents/Resources/
```

- [ ] **Step 7: Commit**

```bash
git add CLAUDE.md docs/ROADMAP.md docs/superpowers/specs/2026-10-29-coffeepdf-v1-design.md docs/superpowers/specs/2026-11-05-coffeepdf-toolbar-design.md
git commit -m "Record CoffeePDF's own toolbar as built, with its rule in CLAUDE.md" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 8: Hand over to the owner**

Tell the owner the branch is ready, with the package's path and these checks for the pull request (spec §7), and ask
for the go-ahead to push and open it:

1. The toolbar in light and dark, beside Preview's.
2. The icons sharp on Retina, the two drawn ones (Squiggly, Stamp) included.
3. Resizing the window: the tools collapse into More, and come back.
4. Every tool marks up, and each saved markup shows the author in Preview.
5. The format menus restyle a selected shape and a text box.
6. Stamps: labels, styles and Include Date.
7. Search: typing, Return, ⇧Return, Escape, the count, the options, and ⌘F.
8. A Healdan read-only copy shows no tools.
9. VoiceOver reads each button's name.
