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

    /// <summary>The zoom button's menu. A flyout is outside the XAML name scope, so it's found through its button.</summary>
    internal MenuFlyout ZoomMenu => (MenuFlyout)ZoomMenuButton.Flyout!;

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

    // --- Print ----------------------------------------------------------------------------------------------------------

    private async void OnPrint(object? sender, RoutedEventArgs e) => await Print();
}
