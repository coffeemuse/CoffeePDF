using Avalonia.Controls;
using Avalonia.Controls.Pdf.Services;
using Avalonia.Platform.Storage;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Dialogs;

namespace CoffeePdf.Windows;

/// <summary>One PDF's window (spec §2.1), hosting the PdfViewer. It's empty until a file is loaded.</summary>
/// <remarks>
/// Every save goes through <see cref="SaveToAsync"/>. The PdfViewer saves to memory, <see cref="MarkupAuthor"/> writes
/// the author into the markups made here, and <see cref="AtomicFile"/> renames the result over the file. The
/// PdfViewer's own saving is off, so its toolbar and shortcuts can't save around that. Saves run one at a time: one
/// that starts while another is writing waits, and then saves the document as it is by then.
/// </remarks>
public partial class DocumentWindow : Window
{
    // The pages an annotation was added to since the file loaded: the only ones a save writes the author on.
    private readonly HashSet<int> _markedPages = [];
    private readonly SemaphoreSlim _oneSaveAtATime = new(1, 1);
    private string? _path;
    private int _savesRunning;     // saves begun and not yet over, waiting or writing: the file isn't up to date until they are
    private bool _saved;           // the PdfViewer has taken a snapshot since the file loaded
    private bool _editedSinceSave; // an undo or redo since that snapshot, which the PdfViewer's IsDirty misses
    private bool _saveFailed;      // the last save failed, though saving to memory cleared IsDirty
    private bool _closeConfirmed;  // asked, or nothing to ask: the next close goes through
    private bool _asking;          // a close is being decided, from the prompt through the save it asked for
    private string? _typedPassword; // what was typed into the PdfViewer's password prompt for this file

    public DocumentWindow()
    {
        InitializeComponent();
        ReportError = (heading, detail) => MessageDialog.ShowErrorAsync(this, heading, detail);
        AskToSave = () => MessageDialog.AskToSaveAsync(this, Path.GetFileName(_path) ?? "CoffeePDF");
        Viewer.AnnotationAdded += (_, e) => _markedPages.Add(e.Annotation.PageIndex);
        Viewer.UndoRedoStateChanged += (_, _) =>
        {
            if (_saved && !Viewer.IsDirty)
                _editedSinceSave = true;
            MenuItem("Undo").IsEnabled = Viewer.CanUndo;
            MenuItem("Redo").IsEnabled = Viewer.CanRedo;
            UpdateTitle();
        };
        Viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == PdfViewer.IsDirtyProperty)
                UpdateTitle();
        };
        // The PdfViewer's own password prompt opens an encrypted PDF but leaves Viewer.Password unset, and a save needs
        // the password to write the author into the encrypted bytes (the owner's check 12b). So the window keeps what's
        // typed there. PART_PasswordTextBox is the prompt's box in the PdfViewer 12.0.1 template; a test drives it, and
        // fails if a newer template renames it.
        Viewer.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextBox>("PART_PasswordTextBox") is not { } box)
                return;
            box.PropertyChanged += (_, change) =>
            {
                if (change.Property == TextBox.TextProperty && !string.IsNullOrEmpty(box.Text))
                    _typedPassword = box.Text;
            };
        };
        ShowRecent(App.Current.Recent.Paths);
        App.Current.Recent.Changed += OnRecentChanged;
        Closed += (_, _) => App.Current.Recent.Changed -= OnRecentChanged;
    }

    /// <summary>The file shown, or null while the window is empty. Save As moves it to the new file.</summary>
    public string? FilePath => _path;

    /// <summary>Whether the document has changes its file doesn't.</summary>
    public bool HasUnsavedChanges => Viewer.HasDocument && (Viewer.IsDirty || _editedSinceSave || _saveFailed || _savesRunning > 0);

    /// <summary>Shows a save's failure, with a heading and the reason. The tests replace it.</summary>
    internal Func<string, string, Task> ReportError { get; set; }

    /// <summary>Asks Save, Don’t Save or Cancel. The tests replace it.</summary>
    internal Func<Task<SaveChoice>> AskToSave { get; set; }

    /// <summary>Shows <paramref name="path"/>.</summary>
    public void Load(string path)
    {
        _path = Path.GetFullPath(path);
        _markedPages.Clear();
        _saved = _editedSinceSave = _saveFailed = false;
        _typedPassword = null;
        Viewer.Source = _path;
        UpdateTitle();
        MenuItem("Undo").IsEnabled = MenuItem("Redo").IsEnabled = false;
    }

    /// <summary>Saves to the window's file (⌘S). Returns whether it saved; a failure has been shown.</summary>
    public Task<bool> SaveAsync() => _path is null ? Task.FromResult(false) : SaveToAsync(_path);

    /// <summary>
    /// Writes the document to <paramref name="target"/> (spec §6.4), and the window then shows that file. The
    /// author goes into the new markups, and the file is replaced whole. Returns whether it saved.
    /// </summary>
    internal async Task<bool> SaveToAsync(string target)
    {
        if (!Viewer.HasDocument)
            return false;
        // One window per document (spec §2.1): a window left on the same file would have its next save overwrite this one.
        var full = Path.GetFullPath(target);
        if (App.Current.Windows.Any(w => w != this && w.FilePath == full))
        {
            try
            {
                await ReportError($"“{Path.GetFileName(full)}” is open in another window.", "Close it there first, or save under another name.");
            }
            catch (Exception)
            {
                // The window may have closed meanwhile, and then there's nothing to show the error over. The save hasn't
                // happened all the same, and its caller is told so.
            }
            return false;
        }
        // Counted from here, not from the snapshot: the PdfViewer clears IsDirty at the snapshot, but until the file is
        // written the window still has changes the file doesn't.
        _savesRunning++;
        UpdateTitle();
        Exception? failure = null;
        string? movedTo = null;
        await _oneSaveAtATime.WaitAsync();
        try
        {
            using var memory = new MemoryStream();
            await Viewer.SaveDocumentAsync(memory);
            // From the snapshot on, an undo is a change this save doesn't hold, so it's recorded even while the file is
            // still being written.
            _saved = true;
            _editedSinceSave = false;
            var pdf = memory.ToArray();
            var previous = _path;
            int[] pages = [.. _markedPages];
            var author = App.Current.MarkupAuthor;
            var password = Viewer.Password ?? _typedPassword;
            await Task.Run(() => AtomicFile.Write(target, MarkupAuthor.Stamp(pdf, previous, pages, author, password)));
            _path = Path.GetFullPath(target);
            _saveFailed = false;
            if (_path != previous)
                movedTo = _path;
        }
        catch (Exception error) // whatever stopped it, the user is told, and the changes stay unsaved
        {
            failure = error;
            _saveFailed = true;
        }
        finally
        {
            _savesRunning--;
            _oneSaveAtATime.Release();
            UpdateTitle();
        }
        if (failure is null)
        {
            // Only once the file is written, and outside the try above: adding to Open Recent raises Changed, which runs
            // every window's menu code, and nothing there may turn a save that worked into a failed one.
            if (movedTo is not null)
            {
                try
                {
                    App.Current.Recent.Add(movedTo);
                }
                catch (Exception)
                {
                    // Open Recent is a convenience: the file is saved, and recent.json is written before Changed is
                    // raised, so a menu that couldn't be refreshed now is not worth reporting as a failed save.
                }
            }
            return true;
        }
        try
        {
            await ReportError($"“{Path.GetFileName(target)}” couldn’t be saved.", failure.Message);
        }
        catch (Exception)
        {
            // The window may have closed while the file was written, and then there's nothing to show the error over.
            // The save has failed all the same, and its caller is told so.
        }
        return false;
    }

    /// <summary>Save As (⇧⌘S): writes the document to a file the user picks, and the window moves to it.</summary>
    public async Task<bool> SaveAsAsync()
    {
        if (!Viewer.HasDocument)
            return false;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = Path.GetFileName(_path),
            DefaultExtension = "pdf",
            FileTypeChoices = [FilePickerFileTypes.Pdf],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath() is { } target && await SaveToAsync(target);
    }

    /// <summary>Print… (⌘P): the system print dialog, with the unsaved markups, under the file's name.</summary>
    public Task PrintAsync() => Viewer.PrintAsync(new PrintOptions { Title = Path.GetFileName(_path) ?? "CoffeePDF" });

    /// <summary>
    /// Prints, and shows a failure instead of throwing it. Nothing catches an exception out of a menu handler, so one
    /// from a missing or failing print service would end the app, and the unsaved markups with it.
    /// </summary>
    internal async Task PrintAndReportAsync()
    {
        try
        {
            await PrintAsync();
        }
        catch (Exception error) // whatever the print service throws: the user is told, and the document is untouched
        {
            try
            {
                await ReportError($"“{Path.GetFileName(_path) ?? "CoffeePDF"}” couldn’t be printed.", error.Message);
            }
            catch (Exception)
            {
                // The window may have closed while the print dialog was up, and then there's nothing to show the error
                // over. The print has failed all the same, and that is all there is to say.
            }
        }
    }

    /// <summary>
    /// Closes the window, first asking to save its changes, if it has any (spec §6.8). Returns false if it stayed
    /// open: the user cancelled, or the save failed.
    /// </summary>
    public async Task<bool> CloseWithPromptAsync()
    {
        // Held through the save the prompt asked for, which counts as unsaved changes: a second ⌘W meanwhile would
        // otherwise ask the same question again over the save.
        if (_asking)
            return false;
        _asking = true;
        try
        {
            if (HasUnsavedChanges)
            {
                var choice = await AskToSave();
                if (choice == SaveChoice.Cancel || (choice == SaveChoice.Save && !await SaveAsync()))
                    return false;
            }
        }
        finally
        {
            _asking = false;
        }
        _closeConfirmed = true;
        Close();
        return true;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || !HasUnsavedChanges)
            return;
        // The red button, ⌘W, or the app quitting: ask first, then close for real.
        e.Cancel = true;
        _ = CloseWithPromptAsync();
    }

    /// <summary>Fills File › Open Recent with <paramref name="paths"/>, newest first, and Clear Menu.</summary>
    internal void ShowRecent(IReadOnlyList<string> paths)
    {
        var menu = MenuItem("Open Recent").Menu!;
        menu.Items.Clear();
        foreach (var path in paths)
            menu.Items.Add(new NativeMenuItem { Header = Path.GetFileName(path), Command = new ActionCommand(() => App.Current.OpenFile(path)) });
        if (paths.Count > 0)
            menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem { Header = "Clear Menu", Command = new ActionCommand(App.Current.Recent.Clear, () => paths.Count > 0) });
    }

    /// <summary>The menu bar's item with <paramref name="header"/>, at any depth.</summary>
    internal NativeMenuItem MenuItem(string header) =>
        Find(NativeMenu.GetMenu(this)!, header) ?? throw new InvalidOperationException($"No menu item {header}");

    private static NativeMenuItem? Find(NativeMenu menu, string header)
    {
        foreach (var item in menu.Items.OfType<NativeMenuItem>())
        {
            if (item.Header == header)
                return item;
            if (item.Menu is { } submenu && Find(submenu, header) is { } found)
                return found;
        }
        return null;
    }

    private void OnRecentChanged(object? sender, EventArgs e) => ShowRecent(App.Current.Recent.Paths);

    private void UpdateTitle() => Title = DocumentTitle.For(_path, HasUnsavedChanges);

    private async void OnOpen(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.Pdf],
        });
        foreach (var file in files)
            if (file.TryGetLocalPath() is { } path)
                App.Current.OpenFile(path);
    }

    private async void OnSave(object? sender, EventArgs e) => await SaveAsync();

    private async void OnSaveAs(object? sender, EventArgs e) => await SaveAsAsync();

    private void OnClose(object? sender, EventArgs e) => Close();

    private async void OnPrint(object? sender, EventArgs e) => await PrintAndReportAsync();

    private static T Parameter<T>(object? sender) where T : struct, Enum =>
        Enum.Parse<T>((string)((NativeMenuItem)sender!).CommandParameter!);

    internal void OnTool(object? sender, EventArgs e) => Viewer.ActiveTool = Parameter<PdfViewerTool>(sender);
    internal void OnViewMode(object? sender, EventArgs e) => Viewer.ViewMode = Parameter<PdfViewMode>(sender);
    internal void OnSidebar(object? sender, EventArgs e) => Viewer.SidebarMode = Parameter<SidebarMode>(sender);
    // ⌘Z and ⇧⌘Z are Edit's key equivalents, so the menu gets them before a focused text field, such as the toolbar's
    // search box, would: they act on that field first, and on the PdfViewer's markups otherwise (the owner's check 10).
    internal void OnUndo(object? sender, EventArgs e)
    {
        if (FocusedTextBox() is { } box)
            box.Undo();
        else
            Viewer.Undo();
    }

    internal void OnRedo(object? sender, EventArgs e)
    {
        if (FocusedTextBox() is { } box)
            box.Redo();
        else
            Viewer.Redo();
    }

    private TextBox? FocusedTextBox() => FocusManager?.GetFocusedElement() as TextBox;
    internal void OnZoomIn(object? sender, EventArgs e) => Viewer.ZoomIn();
    private void OnFindNext(object? sender, EventArgs e) => Viewer.FindNext();
    private void OnFindPrevious(object? sender, EventArgs e) => Viewer.FindPrevious();
    private void OnActualSize(object? sender, EventArgs e) => Viewer.ResetZoom();
    private void OnZoomToFit(object? sender, EventArgs e) => Viewer.FitPage();
    private void OnZoomOut(object? sender, EventArgs e) => Viewer.ZoomOut();
    private void OnFitWidth(object? sender, EventArgs e) => Viewer.FitWidth();
    private void OnPreviousPage(object? sender, EventArgs e) => Viewer.PreviousPage();
    private void OnNextPage(object? sender, EventArgs e) => Viewer.NextPage();
    private void OnToggleBookmark(object? sender, EventArgs e) => Viewer.ToggleBookmarkCurrentPage();
}
