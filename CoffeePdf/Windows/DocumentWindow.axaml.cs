using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CoffeePdf.Windows;

/// <summary>One PDF's window (spec §2.1), hosting the PdfViewer. It's empty until a file is loaded.</summary>
public partial class DocumentWindow : Window
{
    public DocumentWindow() => InitializeComponent();

    /// <summary>The file shown, or null while the window is empty.</summary>
    public string? FilePath => Viewer.Source;

    /// <summary>Shows <paramref name="path"/>. The viewer loads it, and its saves go back to it.</summary>
    public void Load(string path)
    {
        Viewer.Source = path;
        Title = Path.GetFileName(path);
    }

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
                ((App)Application.Current!).OpenFile(path);
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        try
        {
            await Viewer.SaveAsync();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Phase 1 gives saving its own error handling. Until then the console says what went wrong.
            Console.Error.WriteLine($"CoffeePDF couldn't save {FilePath}: {error.Message}");
        }
    }

    private void OnClose(object? sender, EventArgs e) => Close();
}
