using Avalonia.Controls;

namespace CoffeePdf.Tests;

/// <summary>A PdfViewer in a headless window, for the tests that check its behaviour. Call these on the UI thread.</summary>
internal static class Viewers
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Shows a viewer that may save, and loads <paramref name="path"/> through <c>Source</c>, as DocumentWindow does,
    /// so <c>SaveAsync</c> saves in place.
    /// </summary>
    public static async Task<PdfViewer> OpenAsync(string path)
    {
        var viewer = new PdfViewer { AllowDocumentSaving = true, AllowAnnotationEditing = true, AutoSave = false };
        await ShowAndLoadAsync(viewer, () => viewer.Source = path);
        return viewer;
    }

    /// <summary>
    /// Shows <paramref name="viewer"/> (in a window of its own unless it's in one already), runs
    /// <paramref name="load"/>, and waits until the document has loaded.
    /// </summary>
    public static async Task ShowAndLoadAsync(PdfViewer viewer, Action load)
    {
        // Asynchronous continuations, so the test never carries on inside the PdfViewer's own DocumentLoaded handler.
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.DocumentLoaded += (_, _) => loaded.TrySetResult();
        viewer.LoadError += (_, e) => loaded.TrySetException(new IOException(e.Message, e.Exception));
        if (TopLevel.GetTopLevel(viewer) is null)
            new Window { Content = viewer, Width = 900, Height = 700 }.Show();
        load();
        await loaded.Task.WaitAsync(LoadTimeout);
    }
}
