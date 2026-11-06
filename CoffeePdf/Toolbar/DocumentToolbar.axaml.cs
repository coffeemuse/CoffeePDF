using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
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
    private static readonly PdfStampStyle[] StampStyles = Enum.GetValues<PdfStampStyle>();
    private IReadOnlyList<string> _stampLabels = []; // the viewer's labels as they first were, in the menu's order
    private bool _showingStamp;                      // the stamp menu is being set from the viewer, not by a person

    public DocumentToolbar()
    {
        InitializeComponent();
        StampLabelList.AddHandler(PointerReleasedEvent, OnStampLabelReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        FillZoomMenu();
        foreach (var shape in ToolbarRules.Shapes)
            ShapesMenu.Items.Add(Item(shape.ToString(), () => ChooseShape(shape)));
        Tools.OverflowChanged += (_, _) => ShowOverflow();
        ShowOverflow(); // nothing has overflowed yet, so More starts hidden; the panel only says when that changes
        Colours.Chosen += (_, _) => ShowColourSwatch();
    }

    /// <summary>Print, as the window does it, so a failure is reported, not lost (spec §4).</summary>
    internal Func<Task> Print { get; set; } = () => Task.CompletedTask;

    /// <summary>The zoom button's menu. A flyout is outside the XAML name scope, so it's found through its button.</summary>
    internal MenuFlyout ZoomMenu => (MenuFlyout)ZoomMenuButton.Flyout!;

    /// <summary>The Shapes button's menu. A flyout is outside the XAML name scope, so it's found through its button.</summary>
    internal MenuFlyout ShapesMenu => (MenuFlyout)ShapesButton.Flyout!;

    internal Flyout StampFlyout => (Flyout)StampButton.Flyout!;

    internal MenuFlyout MoreMenu => (MenuFlyout)MoreButton.Flyout!;

    private PdfViewer Viewer => _viewer ?? throw new InvalidOperationException("The toolbar has no viewer yet");

    /// <summary>Drives <paramref name="viewer"/> from now on. The window calls it once, as it's built.</summary>
    public void Attach(PdfViewer viewer)
    {
        _viewer = viewer;
        _stampLabels = viewer.StampLabels;
        ShowStamp();
        viewer.PropertyChanged += OnViewerPropertyChanged;
        viewer.Classes.CollectionChanged += (_, _) => ShowTools(); // :shape-selected and :editing-text come and go
        ShowSidebar();
        ShowSearch();
        ShowAvailability();
        ShowTools();
    }

    /// <summary>Puts the cursor in the search box, for Edit › Find… (spec §4).</summary>
    internal void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PdfViewer.ActiveToolProperty || e.Property == PdfViewer.SelectedShapeTypeProperty)
            ShowTools();
        else if (e.Property == PdfViewer.CurrentPageProperty || e.Property == PdfViewer.PageCountProperty)
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
        // The tools hide while the window may not be marked up, as the built-in toolbar's did, and no hidden tool stays
        // armed (spec §4, §5).
        Tools.IsVisible = Viewer.CanEditAnnotations;
        Tools.IsEnabled = open;
        if (!Viewer.CanEditAnnotations && Viewer.ActiveTool != PdfViewerTool.None)
            Viewer.ActiveTool = PdfViewerTool.None;
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
        var shown = Viewer.CurrentPage.ToString(CultureInfo.InvariantCulture);
        if (PageBox.Text == shown)
            return; // nothing typed: leaving the box must not move the view
        var page = ToolbarRules.ParsePage(PageBox.Text, Viewer.PageCount);
        if (page is { } typed)
            Viewer.GoToPage(typed);
        PageBox.Text = (page ?? Viewer.CurrentPage).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Return: commits, then hands focus back to the viewer, so the box follows it again (as Preview does).</summary>
    private void EnterPage()
    {
        CommitPage();
        Viewer.FocusViewer();
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            EnterPage();
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
        if (ZoomBox.Text == ToolbarRules.ZoomText(Viewer.ZoomLevel))
            return; // nothing typed: leaving the box must not swap a fit mode for a manual zoom
        if (ToolbarRules.ParseZoom(ZoomBox.Text, Viewer.MinZoom, Viewer.MaxZoom) is { } level)
            SetZoom(level);
        ZoomBox.Text = ToolbarRules.ZoomText(Viewer.ZoomLevel);
    }

    private void OnZoomKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitZoom();
            Viewer.FocusViewer(); // the box follows the viewer again (as Preview does)
        }
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
        ShowFormatMenus();
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

    /// <summary>
    /// A click on the label already selected raises no SelectionChanged, so a release over any label chooses it too (spec §3);
    /// the current label stays selected, to show which one the button places.
    /// </summary>
    private void OnStampLabelReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left && (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { Content: string label })
        {
            ChooseStampLabel(label);
            StampFlyout.Hide();
        }
    }

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
    /// Takes what the panel has moved off the row out of sight, and puts the rest back, each time the panel says which
    /// (spec §4). The panel arranges an overflowed child in an empty rect but leaves it drawn, taking clicks and the
    /// focus, and a split button keeps its minimum height there, over the first tool (the final review's C1). The More
    /// button is the panel's too, arranged the same way while nothing has overflowed, so it shows only while something has.
    /// </summary>
    private void ShowOverflow()
    {
        var overflowed = Tools.OverflowedChildren;
        foreach (var child in Tools.Children)
            SetShown(child, child == MoreButton ? overflowed.Count > 0 : !overflowed.Contains(child));
        FillMoreMenu();
    }

    /// <summary>
    /// Shows <paramref name="control"/>, as its styles have it, or hides it while it stays in the panel: the panel would
    /// stop counting a child that isn't visible, and never bring it back.
    /// </summary>
    private static void SetShown(Control control, bool shown)
    {
        if (shown)
        {
            control.ClearValue(OpacityProperty);
            control.ClearValue(IsHitTestVisibleProperty);
            control.ClearValue(FocusableProperty);
            control.ClearValue(AutomationProperties.AccessibilityViewProperty);
        }
        else
        {
            control.Opacity = 0;
            control.IsHitTestVisible = false;
            control.Focusable = false;
            AutomationProperties.SetAccessibilityView(control, AccessibilityView.Raw); // so VoiceOver passes over it
        }
    }

    /// <summary>
    /// Lists the tools that don't fit in the More menu (spec §4). Shapes and Stamp bring their own menus as submenus.
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

    // --- The format menus -----------------------------------------------------------------------------------------------

    /// <summary>
    /// What's selected on the page, from the viewer's states (spec §4). The viewer exposes nothing else: SelectedShapeType
    /// is None whenever no tool is armed, so it can't tell a text box from a shape. A text box being edited shows
    /// :editing-text (with :shape-selected), and one only selected shows :shape-selected alone, like a shape (spec §10,
    /// probes 5 and 5b).
    /// </summary>
    internal PageSelection Selection()
    {
        var states = Viewer.Classes;
        if (states.Contains(":editing-text"))
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

    // --- Print ----------------------------------------------------------------------------------------------------------

    private async void OnPrint(object? sender, RoutedEventArgs e) => await Print();
}
