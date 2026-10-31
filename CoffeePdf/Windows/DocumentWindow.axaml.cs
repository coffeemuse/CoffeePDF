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
/// that starts while another is writing waits, and then saves the document as it is by then, unless the window has
/// closed or begun moving to a new copy meanwhile. A close, and a move, wait for a save still writing.
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
    private const string SaveAsNewDocumentLabel = "Save as New Document…";
    private bool _writtenForHealdan; // a copy was written for Healdan, which cleared IsDirty, but the window's file lacks the changes
    private bool _moving;            // moving to the new copy Healdan filed: the window takes no input (spec §6.6 step 3)
    private bool _closeWhenMoved;    // a close came during the move, and goes through once it's done
    private TaskCompletionSource<bool>? _loadWaiter; // completed when the PdfViewer has loaded the file, or failed to

    public DocumentWindow()
    {
        InitializeComponent();
        ReportError = (heading, detail) => MessageDialog.ShowErrorAsync(this, heading, detail);
        AskToSave = () => MessageDialog.AskToSaveAsync(this, Path.GetFileName(_path) ?? "CoffeePDF");
        PickSaveTarget = async name => (await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = name,
            DefaultExtension = "pdf",
            FileTypeChoices = [FilePickerFileTypes.Pdf],
            ShowOverwritePrompt = true,
        }))?.TryGetLocalPath();
        PickFilesToOpen = async () => (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.Pdf],
        })).Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
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
            _loadWaiter?.TrySetResult(true);
        };
        Viewer.LoadError += (_, _) => _loadWaiter?.TrySetResult(false);
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
            // A save still writing finishes first, so the copy isn't let go under it (spec §6.8), and one that moves the
            // window off the copy lets that go itself. A save waiting behind it writes nothing, now the window has closed.
            await _oneSaveAtATime.WaitAsync();
            _oneSaveAtATime.Release();
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
    public bool HasUnsavedChanges =>
        Viewer.HasDocument && (Viewer.IsDirty || _editedSinceSave || _saveFailed || _writtenForHealdan || _savesRunning > 0);

    /// <summary>Shows a save's failure, with a heading and the reason. The tests replace it.</summary>
    internal Func<string, string, Task> ReportError { get; set; }

    /// <summary>Asks Save, Don’t Save or Cancel. The tests replace it.</summary>
    internal Func<Task<SaveChoice>> AskToSave { get; set; }

    /// <summary>Replaces the file with a save's bytes, on a pool thread. The tests replace it to hold a save mid-write.</summary>
    internal Action<string, byte[]> WriteFile { get; set; } = (target, pdf) => AtomicFile.Write(target, pdf);

    /// <summary>The Mac's Save panel, given the name it starts with, answering the file picked or null. The tests replace it.</summary>
    internal Func<string?, Task<string?>> PickSaveTarget { get; set; }

    /// <summary>The Mac's Open panel, answering the files picked. The tests replace it.</summary>
    internal Func<Task<IReadOnlyList<string>>> PickFilesToOpen { get; set; }

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
        _saved = _editedSinceSave = _saveFailed = _writtenForHealdan = false;
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
    /// author goes into the new markups, and the file is replaced whole. Returns whether it saved. With
    /// <paramref name="asCopy"/>, it writes a copy for Healdan to file instead (§6.6): the window stays on its own
    /// file, whose changes stay unsaved.
    /// </summary>
    internal async Task<bool> SaveToAsync(string target, bool asCopy = false)
    {
        if (!Viewer.HasDocument || _moving)
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
            // A save that waited behind another writes nothing once the window is moving to a new copy, or has closed:
            // its file is the one the window is leaving or has let go (spec §6.6 step 3, §6.8). The close prompt's own
            // Save runs before the window closes, so it still saves.
            if (!asCopy && (_moving || _closed))
                return false;
            // Whether the window's own file lacks changes, read before the snapshot clears IsDirty: a copy written for
            // Healdan leaves them unsaved there.
            var unsaved = Viewer.IsDirty || _editedSinceSave || _saveFailed || _writtenForHealdan;
            using var memory = new MemoryStream();
            await Viewer.SaveDocumentAsync(memory);
            // From the snapshot on, an undo is a change this save doesn't hold, so it's recorded even while the file is
            // still being written.
            _saved = true;
            _editedSinceSave = false;
            if (asCopy)
                _writtenForHealdan = unsaved;
            var pdf = memory.ToArray();
            previous = _path;
            int[] pages = [.. _markedPages];
            var author = App.Current.MarkupAuthor;
            var password = Viewer.Password ?? _typedPassword;
            var write = WriteFile;
            await Task.Run(() => write(target, MarkupAuthor.Stamp(pdf, previous, pages, author, password)));
            if (!asCopy)
            {
                _path = Path.GetFullPath(target);
                _saveFailed = false;
                _writtenForHealdan = false;
                if (_path != previous)
                    movedTo = _path;
            }
        }
        catch (Exception error) // whatever stopped it, the user is told, and the changes stay unsaved
        {
            failure = error;
            if (!asCopy)
                _saveFailed = true; // a copy for Healdan that fails leaves the window as it was
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
            if (!asCopy)
                AfterSave(previous);
            return true;
        }
        try
        {
            await ReportError(asCopy ? $"“{Path.GetFileName(_path)}” couldn’t be written for Healdan." : $"“{Path.GetFileName(target)}” couldn’t be saved.",
                failure.Message);
        }
        catch (Exception)
        {
            // The window may have closed while the file was written, and then there's nothing to show the error over.
            // The save has failed all the same, and its caller is told so.
        }
        return false;
    }

    /// <summary>Save As (⇧⌘S): the Helper's Save window while Healdan support is on (spec §6.3), else the Mac's Save panel.</summary>
    public Task<bool> SaveAsAsync() => Allowed.HelperBoxes ? SaveNewAsync() : SaveAsWithPanelAsync();

    /// <summary>
    /// The Mac's own Save panel: writes the document to the file picked, and the window moves to it. A closed window, or
    /// one moving to a new copy, shows none.
    /// </summary>
    internal async Task<bool> SaveAsWithPanelAsync() =>
        !_closed && !_moving && Viewer.HasDocument && await PickSaveTarget(Path.GetFileName(_path)) is { } target && await SaveToAsync(target);

    /// <summary>
    /// Save to Healdan for a local PDF, and Save as New Document for a copy (spec §6.6): the Helper's Save window, then
    /// the PDF with its markups written to the answer's temporary file, filed, and the window moved to the new copy.
    /// The Helper's way back shows the Mac's Save panel, as does a Helper that doesn't answer its Save window, and a filing
    /// it refuses; an error is shown first. A Helper that doesn't answer the filing may have filed the document, so
    /// the user is told, and no panel is shown. A closed window files nothing, nor does one that closes while the copy
    /// is written. Returns whether the document was saved.
    /// </summary>
    internal async Task<bool> SaveNewAsync()
    {
        if (_closed || !Viewer.HasDocument || _document is not { } document || _moving)
            return false;
        var original = document.TellsHelper ? _path : null;
        SaveNewAnswer answer;
        try
        {
            answer = await Healdan.Channel.SaveNewAsync(document.NameForHealdan, original);
        }
        catch (HelperUnreachableException)
        {
            OnHelperAway();
            return await SaveAsWithPanelAsync();
        }
        catch (HelperErrorException e)
        {
            await TellAsync("Healdan Helper couldn’t show its Save window.", e.Message);
            return await SaveAsWithPanelAsync();
        }
        if (_closed)
            return false; // the window closed while the Helper's was open
        if (answer.Action == SaveNewAction.Local)
            return await SaveAsWithPanelAsync();
        if (answer.Action != SaveNewAction.Save)
            return false;
        if (answer.Format != "pdf" || answer.TempPath is not { Length: > 0 } temp)
        {
            await TellAsync("Healdan Helper asked for a file CoffeePDF can’t write.", "CoffeePDF writes PDFs only, so the Mac’s Save panel is shown instead.");
            return await SaveAsWithPanelAsync();
        }
        if (!await SaveToAsync(temp, asCopy: true))
        {
            TryDeleteTemporary(temp); // the Helper deletes it only after /v1/file
            return false;
        }
        if (_closed)
        {
            // The window closed while the copy was written: filing it would move a closed window, which waits for a load
            // that never comes. The Helper hasn't been handed the file, so it's deleted here.
            TryDeleteTemporary(temp);
            Healdan.Log.Info("Save to Healdan stopped: the window closed");
            return false;
        }
        var filing = await FileAndMoveAsync(answer, original);
        if (_closeWhenMoved)
        {
            // A close came during the filing, and goes through now that the move is over: the Mac's Save panel isn't shown
            // over it. The close asks about any changes the window still has.
            _closeWhenMoved = false;
            Close();
            return filing == Filing.Filed;
        }
        return filing switch
        {
            Filing.Filed => true,
            Filing.Refused => await SaveAsWithPanelAsync(),
            _ => false,
        };
    }

    /// <summary>How filing the PDF written for Healdan ended (spec §6.6 step 2).</summary>
    private enum Filing
    {
        /// <summary>Healdan has the document.</summary>
        Filed,

        /// <summary>The Helper refused it, and has told the user why.</summary>
        Refused,

        /// <summary>The Helper didn't answer, so Healdan may have the document already.</summary>
        Unknown,
    }

    /// <summary>
    /// Files the PDF written for the answer, and moves the window to the new copy (spec §6.6 steps 2 and 3), taking no
    /// input meanwhile. Returns how the filing ended. When it wasn't filed, the window is as it was, with its changes
    /// unsaved.
    /// </summary>
    private async Task<Filing> FileAndMoveAsync(SaveNewAnswer answer, string? original)
    {
        _moving = true;
        DocumentPanel.IsEnabled = false;
        try
        {
            CopyState filed;
            try
            {
                filed = await Healdan.Channel.FileAsync(answer, original);
            }
            catch (HelperUnreachableException)
            {
                // Every failure of the call arrives here, the time limit and a reset after the request went among them,
                // so the Helper may have filed the document: the file written for it is left to the Helper, and the Mac's
                // Save panel isn't offered, which would file a second copy.
                OnHelperAway();
                await TellAsync("Healdan Helper didn’t answer while filing the document.", "It may be in Healdan already. Check there before you save it again.");
                return Filing.Unknown;
            }
            catch (HelperErrorException e)
            {
                // The Helper has told the user why, in a notification, and deleted the file (the Desktop Clients page).
                Healdan.Log.Warn($"Filing: {e.Message}");
                return Filing.Refused;
            }
            if (!filed.Known || filed.Path.Length == 0)
            {
                await TellAsync("The document is filed in Healdan, but Healdan Helper gave no copy to open.",
                    "Close this window without saving, and open the document from Healdan.");
                return Filing.Filed;
            }
            await MoveToCopyAsync(filed);
            return Filing.Filed;
        }
        finally
        {
            _moving = false;
            DocumentPanel.IsEnabled = true;
        }
    }

    /// <summary>
    /// Moves the window to the copy Healdan filed (spec §6.6 step 3), in its order: reopens the file there, writes its
    /// lock file (as the PdfViewer loads it), lets the old copy go, if there was one, and tells the Helper the window
    /// has adopted the new copy, whose file as it is now is the starting point. The new copy is encrypted as the old
    /// file was, so the window keeps the password. A close during the move ends the wait for the file to load, which a
    /// password prompt could hold for ever.
    /// </summary>
    private async Task MoveToCopyAsync(CopyState filed)
    {
        // No save writes across the move: one still writing finishes first, and one waiting behind it then finds the
        // window moving and writes nothing. The copy written for Healdan was written before the move began.
        await _oneSaveAtATime.WaitAsync();
        var (oldPath, oldDocument, oldLock) = (_path, _document, _lockHeld);
        var loaded = _loadWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            StopFollowing(); // after the wait: a save that finished meanwhile followed the old copy
            var password = Viewer.Password ?? _typedPassword;
            _lockHeld = false;
            Show(filed.Path, WindowDocument.Opened(filed.Path, healdan: true, inCopies: true).Answered(filed));
            _typedPassword = password; // Show cleared it, and the new copy's saves need it to write the author
        }
        finally
        {
            _oneSaveAtATime.Release();
        }
        if (_closeWhenMoved)
            loaded.TrySetResult(false); // a close came during the filing, before there was a load to end
        var shown = await loaded.Task;
        _loadWaiter = null;
        await LetGoAsync(oldPath, oldDocument, oldLock);
        if (!shown)
            return; // the PdfViewer couldn't open the new copy, and says so in the window, or a close ended the wait
        try
        {
            var adopted = await Healdan.Channel.AdoptedAsync(filed.Path);
            if (adopted.Known && _document is not null && _path == Path.GetFullPath(filed.Path))
            {
                _document = _document.Answered(adopted);
                ApplyMode();
            }
        }
        catch (Exception e) when (e is HelperUnreachableException or HelperErrorException)
        {
            // An older Helper, or one that's gone: it takes the first write that settles as the starting point instead.
            Healdan.Log.Warn($"/v1/adopted: {e.GetType().Name}");
        }
    }

    /// <summary>Deletes the file written for Healdan when the Helper won't, as it deletes one only after <c>/v1/file</c>.</summary>
    private void TryDeleteTemporary(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Healdan.Log.Warn($"The file written for Healdan couldn’t be deleted ({e.GetType().Name})");
        }
    }

    /// <summary>
    /// Open… (⌘O): the Helper's Open window while Healdan support is on (spec §6.3). Its way back, a Helper that doesn't
    /// answer, or support off shows the Mac's Open panel; an error is shown first. A window moving to a new copy opens
    /// nothing, and a window that closed meanwhile shows no panel.
    /// </summary>
    internal async Task OpenAsync()
    {
        if (_moving)
            return; // the window takes no input until it's the new copy's (spec §6.6 step 3)
        if (Allowed.HelperBoxes)
        {
            try
            {
                // Opened: the Helper opens what was chosen in the app for its kind, so a PDF comes back to CoffeePDF.
                if (await Healdan.Channel.OpenDialogAsync() is OpenDialogAction.Opened or OpenDialogAction.Cancel)
                    return;
            }
            catch (HelperUnreachableException)
            {
                OnHelperAway();
            }
            catch (HelperErrorException e)
            {
                await TellAsync("Healdan Helper couldn’t show its Open window.", e.Message);
            }
        }
        if (_closed)
            return; // the window closed while the Helper's Open window was up: there's nothing to show the Mac's panel over
        foreach (var path in await PickFilesToOpen())
            App.Current.OpenFile(path);
    }

    /// <summary>Properties… (spec §6.7): the Helper's Properties window, or Save to Healdan for a PDF that isn't in Healdan.</summary>
    internal async Task PropertiesAsync()
    {
        if (_moving || _path is not { } path)
            return;
        PropertiesAction action;
        try
        {
            action = await Healdan.Channel.PropertiesAsync(path);
        }
        catch (HelperUnreachableException)
        {
            OnHelperAway();
            await TellAsync("Healdan Helper isn’t running.", "Start it to see this document’s properties in Healdan.");
            return;
        }
        catch (HelperErrorException e)
        {
            await TellAsync("Healdan Helper couldn’t show its Properties window.", e.Message);
            return;
        }
        if (action == PropertiesAction.File)
            await SaveNewAsync();
        else if (action == PropertiesAction.Saved)
            await AskHelperAsync(); // a new name in Healdan moves the title on
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
    /// Closes the window, first asking to save its changes, if it has any (spec §6.8). Where the window's mode doesn't
    /// allow saving, as for a copy whose save Healdan refused (§6.9), Save is Save As instead. Returns false if it
    /// stayed open: the user cancelled, or the save failed or didn't happen.
    /// </summary>
    public async Task<bool> CloseWithPromptAsync()
    {
        // Held through the save the prompt asked for, which counts as unsaved changes: a second ⌘W meanwhile would
        // otherwise ask the same question again over the save.
        if (_asking || _moving)
            return false;
        _asking = true;
        try
        {
            if (HasUnsavedChanges)
            {
                var choice = await AskToSave();
                if (choice == SaveChoice.Cancel || (choice == SaveChoice.Save && !await (Allowed.Save ? SaveAsync() : SaveAsAsync())))
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
        if (_moving)
        {
            // The window takes no input until it's the new copy's (spec §6.6 step 3); the close follows then, and the wait
            // for the new copy to load ends, so the close can't wait on a password prompt.
            e.Cancel = true;
            _closeWhenMoved = true;
            _loadWaiter?.TrySetResult(false);
            return;
        }
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
        if (_closed)
            return; // a save that was still writing as the window closed: following ended with the window
        StopFollowing();
        var following = _following = new CancellationTokenSource();
        _savingToHealdan = true; // from the write on: the first answer can take 120 seconds
        UpdateTitle();
        _ = FollowAsync(_path!, following);
    }

    private async Task FollowAsync(string path, CancellationTokenSource following)
    {
        try
        {
            await SaveFollower.FollowAsync(stop => Healdan.Channel.SavedAsync(path, stop), state => OnSavedAnswer(path, state), OnHelperAway,
                Pause, following.Token);
        }
        catch (Exception e) // whatever else stops it: logged by type, and the title mustn't say a save is on its way for good
        {
            Healdan.Log.Warn($"Following a save stopped ({e.GetType().Name})");
        }
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
    /// With nothing to save, nothing is saved or followed, since the Helper would say a save is pending until the next
    /// one comes: the user is told that save goes as the version. With the Helper away, the version can't be asked
    /// for, so nothing is saved, and the user is told (Phase 2's plan, question 9).
    /// </summary>
    internal async Task<bool> SaveAsNewVersionAsync()
    {
        if (_moving || _document is not { Mode: DocumentMode.Editable } document || _path is not { } path)
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
        if (_closed || _path != path)
            return false; // the window closed, or moved to another file, while the Helper was answering
        if (HasUnsavedChanges)
            return await SaveAsync();
        await TellAsync("The next save goes to Healdan as a new version.", "There are no changes to save yet.");
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
        MenuItem("Healdan").IsVisible = allowed.HealdanMenu; // none with support off (spec §2.5 step 7)
        MenuItem("Save to Healdan…").IsEnabled = allowed.SaveToHealdan;
        MenuItem("Save as New Version").IsEnabled = allowed.SaveAsNewVersion;
        MenuItem("Save as New Document…").IsEnabled = allowed.SaveAsNewDocument;
        MenuItem("Properties…").IsEnabled = allowed.Properties;
        ShowBar(_document?.Bar);
        UpdateTitle();
    }

    private void ShowBar(WindowBar? bar)
    {
        Bar.IsVisible = bar is not null;
        BarText.Text = bar?.Text;
        BarButton.IsVisible = bar is { Action: not BarAction.None };
        BarButton.Content = bar?.Action switch
        {
            BarAction.SaveAsNewDocument => SaveAsNewDocumentLabel,
            BarAction.TurnOnHealdan => TurnOnHealdanLabel,
            _ => null,
        };
    }

    private async void OnBarButton(object? sender, RoutedEventArgs e)
    {
        switch (_document?.Bar?.Action)
        {
            case BarAction.SaveAsNewDocument:
                await SaveNewAsync();
                break;
            case BarAction.TurnOnHealdan:
                TurnOnHealdan();
                break;
        }
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

    private async void OnOpen(object? sender, EventArgs e) => await OpenAsync();

    private async void OnSave(object? sender, EventArgs e) => await SaveAsync();

    private async void OnSaveAs(object? sender, EventArgs e) => await SaveAsAsync();

    // Save to Healdan on a copy is its ordinary save, as in the LibreOffice extension; on a local PDF it files it (§6.6).
    private async void OnSaveToHealdan(object? sender, EventArgs e)
    {
        if (_document?.Mode == DocumentMode.Editable)
            await SaveAsync();
        else
            await SaveNewAsync();
    }

    private async void OnSaveAsNewVersion(object? sender, EventArgs e) => await SaveAsNewVersionAsync();

    private async void OnSaveAsNewDocument(object? sender, EventArgs e) => await SaveNewAsync();

    private async void OnProperties(object? sender, EventArgs e) => await PropertiesAsync();

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
