using Avalonia;
using Avalonia.Headless;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Logging;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Tests;

/// <summary>
/// Avalonia without a display, for the tests: CoffeePDF's own App, whose themes the PdfViewer needs to draw, with
/// Skia's real drawing. One session serves the whole run, as Avalonia sets up once.
/// </summary>
public static class Headless
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
    {
        // The app's settings and Open Recent go to a folder of the run's own, never the real one. Its Markup author is
        // set before the app starts, so no save in a test falls back to the account's full name (CLAUDE.md).
        var settings = Directory.CreateTempSubdirectory("coffeepdf-settings-").FullName;
        var store = new SettingsStore(settings) { MarkupAuthor = Windows.TestWindows.Author };
        store.HealdanAsked = true; // so the first launch's question never shows over a test's window
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, settings);
        // The Helper's folders and the log go to folders of the run's own too. The real Helper, which may be running on
        // the Mac the tests run on, is never reached, and an app window starts with the Helper away. The flows' tests
        // give each window a fake Helper's link instead (FakeHelper.Link).
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, Directory.CreateTempSubdirectory("coffeepdf-helper-").FullName);
        Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, Directory.CreateTempSubdirectory("coffeepdf-log-").FullName);
        return HeadlessUnitTestSession.StartNew(typeof(Headless));
    });

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Runs <paramref name="action"/> on the session's UI thread, and waits for what it returns.</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> action) => Session.Value.Dispatch(action, CancellationToken.None);
}
