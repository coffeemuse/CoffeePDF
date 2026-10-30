using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Document windows for the tests. Call these on the UI thread.</summary>
internal static class TestWindows
{
    /// <summary>The Markup author the tests save with: a made-up name, never the account's.</summary>
    public const string Author = "Test Reviewer";

    /// <summary>
    /// Shows a DocumentWindow with <paramref name="path"/> loaded, collecting the errors it would show. It first sets
    /// the Markup author to <see cref="Author"/>, in the run's own settings folder (Headless).
    /// </summary>
    public static async Task<(DocumentWindow Window, List<string> Errors)> OpenAsync(string path)
    {
        App.Current.Settings.MarkupAuthor = Author;
        var errors = new List<string>();
        var window = new DocumentWindow();
        window.ReportError = (heading, detail) =>
        {
            errors.Add($"{heading} {detail}");
            return Task.CompletedTask;
        };
        window.Show();
        await Viewers.ShowAndLoadAsync(window.Viewer, () => window.Load(path));
        return (window, errors);
    }

    /// <summary>Waits until <paramref name="condition"/> holds, letting the UI thread run meanwhile, for at most 10 seconds.</summary>
    public static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition never held");
            await Task.Yield();
        }
    }
}
