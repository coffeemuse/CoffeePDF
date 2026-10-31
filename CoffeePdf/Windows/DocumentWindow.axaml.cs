using Avalonia.Controls;
using Avalonia.Controls.Pdf.Services;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Locks;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Dialogs;

namespace CoffeePdf.Windows;

/// <summary>One PDF's window (spec §2.1), hosting the PdfViewer. It's empty until a file is loaded.</summary>
/// <remarks>
/// <para>
/// Every save goes through <see cref="SaveToAsync"/>. The PdfViewer saves to memory, <see cref="MarkupAuthor"/> writes
/// the author into the markups made here, and <see cref="AtomicFile"/> renames the result over the file. The
/// PdfViewer's own saving is off, so its toolbar and shortcuts can't save around that. Saves run one at a time: one
/// that starts while another is writing waits, and then saves the document as it is by then.
/// </para>
/// <para>
/// A Healdan copy opens in its mode (spec §6.1, §6.2), which <see cref="WindowDocument"/> decides from the Helper's
/// answers; the window shows it, keeps the copy's lock file while it may edit it (§6.8), and asks the Helper again as
/// it answers or comes back.
/// </para>
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
    private const string TurnOnHealdanLabel = "Turn On Healdan Support";
    private HealdanLink _healdan = App.Current.Healdan;
    private WindowDocument? _document; // the file's mode and what the Helper last said of it (spec §6.1)
    private int _loads;                // counts the files shown, so an answer about an earlier one is dropped
    private bool _loaded;              // the PdfViewer has the file loaded
    private bool _lockHeld;            // this window wrote the copy's lock file (spec §6.8)
    private bool _closed;              // the window has closed: a flow still running stops short
    private bool _askingHelper;        // a /v1/state for the file is on its way
    private DateTimeOffset _openedAt;  // when the file was shown, for the lock file
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _letGo = Task.CompletedTask; // the copies a save moved the window off are being let go until this is done
    private bool _savingToHealdan;                // a save is being followed, and Healdan doesn't have it yet (spec §6.4)
    private CancellationTokenSource? _following;  // stops the save being followed

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
        Viewer.DocumentLoaded += (_, _) =>
        {
            _loaded = true;
            TakeLockIfEditable();
        };
        // The Helper-away bar goes once the Helper answers again (spec §6.9): it's asked each time the window comes forward.
        Activated += (_, _) =>
        {
            if (_document?.Mode == DocumentMode.HelperAway)
                _ = AskHelperAsync();
        };
        _healdan.Settings.HealdanEnabledChanged += OnHealdanSupportChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            _healdan.Settings.HealdanEnabledChanged -= OnHealdanSupportChanged;
        };
        ApplyMode();
        Closed += async (_, _) =>
        {
            StopFollowing(); // following ends when the window closes (spec §6.4)
            var (path, document, lockHeld) = (_path, _document, _lockHeld);
            _lockHeld = false;
            await Task.WhenAll(_letGo, LetGoAsync(path, document, lockHeld));
            _released.TrySetResult();
        };
    }

    /// <summary>The file shown, or null while the window is empty. Save As moves it to the new file.</summary>
    public string? FilePath => _path;

    /// <summary>Healdan support, the Helper's folders and its channel. The tests set a fake Helper's before Load.</summary>
    internal HealdanLink Healdan
    {
        get => _healdan;
        set
        {
            _healdan.Settings.HealdanEnabledChanged -= OnHealdanSupportChanged;
            _healdan = value;
            _healdan.Settings.HealdanEnabledChanged += OnHealdanSupportChanged;
            ApplyMode();
        }
    }

    /// <summary>The file's mode and what the Helper last said of it, or null while the window is empty.</summary>
    internal WindowDocument? Document => _document;

    /// <summary>
    /// Done once the closed window has let its copy go, and any copy a save moved it off (spec §6.8), and at once for a
    /// window without one.
    /// </summary>
    internal Task Released => _released.Task;

    /// <summary>Whether the window has a copy to let go when it closes, or is letting one go.</summary>
    internal bool HoldsCopy => _lockHeld || _document?.TellsHelper == true || !_letGo.IsCompleted;

    /// <summary>Whether the document has changes its file doesn't.</summary>
    public bool HasUnsavedChanges => Viewer.HasDocument && (Viewer.IsDirty || _editedSinceSave || _saveFailed || _savesRunning > 0);

    /// <summary>Shows a save's failure, with a heading and the reason. The tests replace it.</summary>
    internal Func<string, string, Task> ReportError { get; set; }

    /// <summary>Asks Save, Don’t Save or Cancel. The tests replace it.</summary>
    internal Func<Task<SaveChoice>> AskToSave { get; set; }

    /// <summary>Replaces the file with a save's bytes, on a pool thread. The tests replace it to hold a save mid-write.</summary>
    internal Action<string, byte[]> WriteFile { get; set; } = (target, pdf) => AtomicFile.Write(target, pdf);

    /// <summary>The pause between questions while a save is followed. The tests replace it, so none waits for the real pauses.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Pause { get; set; } = Task.Delay;

    /// <summary>Shows <paramref name="path"/> in an empty window, in its mode (spec §6.2). A copy asks the Helper what it is.</summary>
    public void Load(string path)
    {
        Show(path, WindowDocument.Opened(path, Healdan.Enabled, Healdan.Folders.HoldsCopy(path)));
        if (_document!.Mode == DocumentMode.Asking)
            _ = AskHelperAsync();
    }

    /// <summary>Shows <paramref name="path"/>, starting afresh, in <paramref name="document"/>'s mode.</summary>
    private void Show(string path, WindowDocument document)
    {
        _path = Path.GetFullPath(path);
        _markedPages.Clear();
        _saved = _editedSinceSave = _saveFailed = false;
        _typedPassword = null;
        _loads++;
        _loaded = false;
        _openedAt = DateTimeOffset.Now;
        _document = document;
        Viewer.Source = _path;
        ApplyMode();
        MenuItem("Undo").IsEnabled = MenuItem("Redo").IsEnabled = false;
    }

    /// <summary>Saves to the window's file (⌘S), where its mode allows it. Returns whether it saved; a failure has been shown.</summary>
    public Task<bool> SaveAsync() => _path is null || !Allowed.Save ? Task.FromResult(false) : SaveToAsync(_path);

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
        string? previous = null;
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
            previous = _path;
            int[] pages = [.. _markedPages];
            var author = App.Current.MarkupAuthor;
            var password = Viewer.Password ?? _typedPassword;
            var write = WriteFile;
            await Task.Run(() => write(target, MarkupAuthor.Stamp(pdf, previous, pages, author, password)));
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
            // Healdan's copies stay out of Open Recent (spec §6.10).
            if (movedTo is not null && !Healdan.Folders.HoldsCopy(movedTo))
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
            AfterSave(previous);
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

    /// <summary>
    /// Asks the Helper what the window's file is (<c>/v1/state</c>, spec §6.2), and takes the mode its answer gives. A
    /// Helper that doesn't answer, or answers with an error, makes a copy the Helper-away one until it answers (§6.9).
    /// </summary>
    internal async Task AskHelperAsync()
    {
        if (_askingHelper || _path is not { } path || _document is null)
            return;
        _askingHelper = true;
        var load = _loads;
        CopyState? state;
        try
        {
            state = await Healdan.Channel.StateAsync(path);
        }
        catch (Exception e) when (e is HelperUnreachableException or HelperErrorException)
        {
            state = null;
        }
        finally
        {
            _askingHelper = false;
        }
        if (_closed)
            return; // the window closed while the Helper was answering: there's nothing to show or lock
        if (load != _loads || _document is null)
            return; // the window has moved on to another file meanwhile
        _document = _document.Answered(state);
        ApplyMode();
        TakeLockIfEditable();
    }

    /// <summary>
    /// Writes the copy's lock file once the PdfViewer has the file loaded and the window may edit it (spec §6.8, §4.4
    /// item 8). One that can't be written is logged: the Helper then sees no lock, which its close watch handles.
    /// </summary>
    private void TakeLockIfEditable()
    {
        if (_closed || _lockHeld || !_loaded || _path is null || _document?.HoldsLock != true)
            return;
        try
        {
            CopyLock.Write(_path, Environment.ProcessId, _openedAt);
            _lockHeld = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Healdan.Log.Warn($"The lock file couldn’t be written ({e.GetType().Name})");
        }
    }

    /// <summary>
    /// After a save that worked: a copy's save is followed to Healdan (spec §6.4). One that moved the window to another
    /// file ends the following, lets the copy it left go, as a close would (§6.6 step 3, §6.8), and takes the new
    /// file's mode (§6.1).
    /// </summary>
    private void AfterSave(string? previous)
    {
        if (_path == previous)
        {
            if (_document?.FollowsSaves == true)
                Follow();
            return;
        }
        StopFollowing();
        var (document, lockHeld) = (_document, _lockHeld);
        _lockHeld = false;
        _letGo = Task.WhenAll(_letGo, LetGoAsync(previous, document, lockHeld)); // the window's Released waits for it
        _loads++; // an answer about the file the window left is dropped
        _document = WindowDocument.Opened(_path!, Healdan.Enabled, Healdan.Folders.HoldsCopy(_path!));
        ApplyMode();
        if (_document.Mode == DocumentMode.Asking)
            _ = AskHelperAsync();
    }

    /// <summary>Follows the save just written to the copy until Healdan has it (spec §6.4). A newer save takes over.</summary>
    private void Follow()
    {
        StopFollowing();
        var following = _following = new CancellationTokenSource();
        _savingToHealdan = true; // from the write on: the first answer can take 120 seconds
        UpdateTitle();
        _ = FollowAsync(_path!, following);
    }

    private async Task FollowAsync(string path, CancellationTokenSource following)
    {
        await SaveFollower.FollowAsync(stop => Healdan.Channel.SavedAsync(path, stop), state => OnSavedAnswer(path, state), OnHelperAway,
            Pause, following.Token);
        if (_following != following)
            return; // a newer save, a close or a move has taken over
        _following = null;
        _savingToHealdan = false;
        UpdateTitle();
    }

    private void StopFollowing()
    {
        _following?.Cancel();
        _following = null;
        _savingToHealdan = false;
    }

    /// <summary>An answer to <c>/v1/saved</c>: its version moves the title on, and a read-only one is a refused save (spec §6.9).</summary>
    private void OnSavedAnswer(string path, CopyState state)
    {
        if (_document is null || _path != path)
            return;
        _document = _document.Answered(state);
        _savingToHealdan = state.Known && state.Pending;
        ApplyMode();
    }

    /// <summary>The Helper didn't answer: a copy's saves stay on this Mac until it's back, and its bar says so (spec §6.9).</summary>
    private void OnHelperAway()
    {
        if (_document is null)
            return;
        _document = _document.Answered(null);
        ApplyMode();
    }

    /// <summary>
    /// Save as New Version (spec §6.5): the Helper sends the copy's next save as a version, and the changes are saved.
    /// With nothing to save, the save already written is followed, and the Helper sends the version with the next.
    /// With the Helper away, the version can't be asked for, so nothing is saved, and the user is told (Phase 2's plan,
    /// question 9).
    /// </summary>
    internal async Task<bool> SaveAsNewVersionAsync()
    {
        if (_document is not { Mode: DocumentMode.Editable } document || _path is not { } path)
            return false;
        try
        {
            await Healdan.Channel.NewVersionAsync(path);
        }
        catch (HelperUnreachableException)
        {
            OnHelperAway();
            await TellAsync($"“{document.NameForHealdan}” couldn’t be saved as a new version.", "Healdan Helper isn’t running. Try again once it is.");
            return false;
        }
        catch (HelperErrorException e)
        {
            await TellAsync($"“{document.NameForHealdan}” couldn’t be saved as a new version.", e.Message);
            return false;
        }
        if (HasUnsavedChanges)
            return await SaveAsync();
        Follow();
        return true;
    }

    /// <summary>
    /// Lets a copy go (spec §6.8): removes its lock file, if the window wrote one, then tells the Helper with
    /// <c>/v1/closed</c>, so it sends anything unsent, checks the document in and deletes the copy. It never throws: the
    /// Helper watches the lock file too, and its next start closes a copy it wasn't told of (the Desktop Clients page).
    /// </summary>
    private async Task LetGoAsync(string? path, WindowDocument? document, bool lockHeld)
    {
        if (path is null)
            return;
        if (lockHeld)
            CopyLock.TryRemove(path);
        if (document?.TellsHelper != true)
            return;
        try
        {
            await Healdan.Channel.ClosedAsync(path);
        }
        catch (Exception e) when (e is HelperUnreachableException or HelperErrorException)
        {
            Healdan.Log.Info($"Healdan Helper wasn’t told a copy closed ({e.GetType().Name}); it watches the lock file too");
        }
    }

    /// <summary>What the window allows in its mode (spec §6.1).</summary>
    private WindowCommands Allowed => _document?.Commands(Healdan.Enabled) ?? WindowCommands.Empty(Healdan.Enabled);

    /// <summary>Shows the window's mode (spec §6.1): its title and bar, and whether it may be marked up and saved.</summary>
    private void ApplyMode()
    {
        var allowed = Allowed;
        Viewer.IsReadOnly = !allowed.Markup;
        MenuItem("Save").IsEnabled = allowed.Save;
        ShowBar(_document?.Bar);
        UpdateTitle();
    }

    private void ShowBar(WindowBar? bar)
    {
        Bar.IsVisible = bar is not null;
        BarText.Text = bar?.Text;
        // Only Turn On Healdan Support has its button so far; Save as New Document's comes with its flow (Task 12).
        BarButton.IsVisible = bar?.Action == BarAction.TurnOnHealdan;
        BarButton.Content = bar?.Action == BarAction.TurnOnHealdan ? TurnOnHealdanLabel : null;
    }

    private void OnBarButton(object? sender, RoutedEventArgs e)
    {
        if (_document?.Bar?.Action == BarAction.TurnOnHealdan)
            TurnOnHealdan();
    }

    /// <summary>The bar's Turn On Healdan Support: every window follows, through HealdanEnabledChanged.</summary>
    private void TurnOnHealdan()
    {
        try
        {
            Healdan.Settings.HealdanEnabled = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ = TellAsync("Healdan support couldn’t be turned on.", e.Message);
        }
    }

    /// <summary>
    /// Healdan support changed. Turning it on takes effect at once, and a copy opened with it off asks the Helper what
    /// it is. Turning it off takes effect for a copy as its window closes (spec §5), and for a local window at once.
    /// </summary>
    private void OnHealdanSupportChanged(object? sender, EventArgs e)
    {
        if (_document is { Mode: DocumentMode.SupportOff } && Healdan.Enabled)
        {
            _document = WindowDocument.Opened(_document.Path, healdan: true, inCopies: true);
            _ = AskHelperAsync();
        }
        ApplyMode();
    }

    /// <summary>Shows a problem over the window. It never throws: the window may have closed, with nothing to show it over.</summary>
    private async Task TellAsync(string heading, string detail)
    {
        try
        {
            await ReportError(heading, detail);
        }
        catch (Exception)
        {
            // Closed meanwhile: there's nothing to show the problem over, and nothing more to do about it.
        }
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

    private void UpdateTitle() => Title = _document?.Title(HasUnsavedChanges, _savingToHealdan) ?? DocumentTitle.For(_path, HasUnsavedChanges);

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
