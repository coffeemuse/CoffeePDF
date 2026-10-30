using Avalonia.Controls;
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
/// PdfViewer's own saving is off, so its toolbar and shortcuts can't save around that.
/// </remarks>
public partial class DocumentWindow : Window
{
    // The pages an annotation was added to since the file loaded: the only ones a save writes the author on.
    private readonly HashSet<int> _markedPages = [];
    private string? _path;
    private bool _saved;           // a save has worked since the file loaded
    private bool _editedSinceSave; // an undo or redo since that save, which the PdfViewer's IsDirty misses
    private bool _saveFailed;      // the last save failed, though saving to memory cleared IsDirty

    public DocumentWindow()
    {
        InitializeComponent();
        ReportError = (heading, detail) => MessageDialog.ShowErrorAsync(this, heading, detail);
        Viewer.AnnotationAdded += (_, e) => _markedPages.Add(e.Annotation.PageIndex);
        Viewer.UndoRedoStateChanged += (_, _) =>
        {
            if (_saved && !Viewer.IsDirty)
                _editedSinceSave = true;
            UpdateTitle();
        };
        Viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == PdfViewer.IsDirtyProperty)
                UpdateTitle();
        };
    }

    /// <summary>The file shown, or null while the window is empty. Save As moves it to the new file.</summary>
    public string? FilePath => _path;

    /// <summary>Whether the document has changes its file doesn't.</summary>
    public bool HasUnsavedChanges => Viewer.HasDocument && (Viewer.IsDirty || _editedSinceSave || _saveFailed);

    /// <summary>Shows a save's failure, with a heading and the reason. The tests replace it.</summary>
    internal Func<string, string, Task> ReportError { get; set; }

    /// <summary>Shows <paramref name="path"/>.</summary>
    public void Load(string path)
    {
        _path = Path.GetFullPath(path);
        _markedPages.Clear();
        _saved = _editedSinceSave = _saveFailed = false;
        Viewer.Source = _path;
        UpdateTitle();
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
        try
        {
            using var memory = new MemoryStream();
            await Viewer.SaveDocumentAsync(memory);
            var pdf = memory.ToArray();
            var previous = _path;
            int[] pages = [.. _markedPages];
            var author = App.Current.MarkupAuthor;
            var password = Viewer.Password;
            await Task.Run(() => AtomicFile.Write(target, MarkupAuthor.Stamp(pdf, previous, pages, author, password)));
            _path = Path.GetFullPath(target);
            _saved = true;
            _editedSinceSave = _saveFailed = false;
            UpdateTitle();
            return true;
        }
        catch (Exception error) // whatever stopped it, the user is told, and the changes stay unsaved
        {
            _saveFailed = true;
            UpdateTitle();
            await ReportError($"“{Path.GetFileName(target)}” couldn’t be saved.", error.Message);
            return false;
        }
    }

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

    private void OnClose(object? sender, EventArgs e) => Close();
}
