using Avalonia;
using Avalonia.Headless;

namespace CoffeePdf.Tests;

/// <summary>
/// Avalonia without a display, for the tests: CoffeePDF's own App, whose themes the PdfViewer needs to draw, with
/// Skia's real drawing. One session serves the whole run, as Avalonia sets up once.
/// </summary>
public static class Headless
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(Headless)));

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Runs <paramref name="action"/> on the session's UI thread, and waits for what it returns.</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> action) => Session.Value.Dispatch(action, CancellationToken.None);
}
