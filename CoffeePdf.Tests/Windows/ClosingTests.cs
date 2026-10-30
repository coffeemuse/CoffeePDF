using CoffeePdf.Core.Pdf;
using CoffeePdf.Dialogs;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.8: the usual Save changes? prompt, when a window closes and when CoffeePDF quits.</summary>
[Trait("Category", "PdfViewer")]
public sealed class ClosingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, MinimalPdf.Build());
        return path;
    }

    /// <summary>A window with an unsaved note, whose prompt answers <paramref name="answer"/> and counts its asks.</summary>
    private static async Task<(DocumentWindow Window, Func<bool> Closed, Func<int> Asked)> EditedAsync(string path, SaveChoice answer)
    {
        var (window, _) = await TestWindows.OpenAsync(path);
        var closed = false;
        var asked = 0;
        window.Closed += (_, _) => closed = true;
        window.AskToSave = () =>
        {
            asked++;
            return Task.FromResult(answer);
        };
        await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
        return (window, () => closed, () => asked);
    }

    [PdfViewerFact]
    public async Task A_window_without_changes_closes_without_asking()
    {
        var path = Write("clean.pdf");

        var (result, closed, asked) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var wasClosed = false;
            var asks = 0;
            window.Closed += (_, _) => wasClosed = true;
            window.AskToSave = () => { asks++; return Task.FromResult(SaveChoice.Cancel); };
            return (await window.CloseWithPromptAsync(), wasClosed, asks);
        });

        Assert.True(result);
        Assert.True(closed);
        Assert.Equal(0, asked);
    }

    [PdfViewerFact]
    public async Task Cancel_keeps_the_window_and_its_changes()
    {
        var path = Write("doc.pdf");

        var (result, closed, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, _) = await EditedAsync(path, SaveChoice.Cancel);
            var answer = await window.CloseWithPromptAsync();
            var state = (answer, isClosed(), window.HasUnsavedChanges);
            window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            await window.CloseWithPromptAsync();
            return state;
        });

        Assert.False(result);
        Assert.False(closed);
        Assert.True(unsaved);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
    }

    [PdfViewerFact]
    public async Task Dont_Save_closes_and_leaves_the_file()
    {
        var path = Write("doc.pdf");

        var closed = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, _) = await EditedAsync(path, SaveChoice.DontSave);
            await window.CloseWithPromptAsync();
            return isClosed();
        });

        Assert.True(closed);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
    }

    [PdfViewerFact]
    public async Task Save_saves_then_closes()
    {
        var path = Write("doc.pdf");

        var closed = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, _) = await EditedAsync(path, SaveChoice.Save);
            await window.CloseWithPromptAsync();
            return isClosed();
        });

        Assert.True(closed);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(path), 0)).Author);
    }

    [PdfViewerFact]
    public async Task A_failed_save_keeps_the_window_open()
    {
        var path = Write("doc.pdf");
        var mode = File.GetUnixFileMode(_dir);

        var (result, closed, unsaved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, _) = await EditedAsync(path, SaveChoice.Save);
            var errorList = new List<string>();
            window.ReportError = (heading, detail) =>
            {
                errorList.Add($"{heading} {detail}");
                return Task.CompletedTask;
            };
            File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                var answer = await window.CloseWithPromptAsync();
                return (answer, isClosed(), window.HasUnsavedChanges, errorList);
            }
            finally
            {
                File.SetUnixFileMode(_dir, mode);
                window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
                await window.CloseWithPromptAsync();
            }
        });

        Assert.False(result);
        Assert.False(closed);
        Assert.True(unsaved);
        Assert.Contains("“doc.pdf” couldn’t be saved", Assert.Single(errors));
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
    }

    [PdfViewerFact]
    public async Task A_second_close_while_the_first_is_saving_does_not_ask_again()
    {
        var path = Write("doc.pdf");

        var (second, closedEarly, first, closed, asked) = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, asks) = await EditedAsync(path, SaveChoice.Save);
            var closing = window.CloseWithPromptAsync(); // asks, and is then saving, which counts as unsaved changes
            var again = await window.CloseWithPromptAsync();
            var early = isClosed();
            var done = await closing;
            return (again, early, done, isClosed(), asks());
        });

        Assert.False(second);
        Assert.False(closedEarly);
        Assert.True(first);
        Assert.True(closed);
        Assert.Equal(1, asked);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(path), 0)).Author);
    }

    [PdfViewerFact]
    public async Task Closing_the_window_itself_asks_too()
    {
        var path = Write("doc.pdf");

        var (asked, closed) = await Headless.RunAsync(async () =>
        {
            var (window, isClosed, asks) = await EditedAsync(path, SaveChoice.Cancel);
            var prompted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.AskToSave = () =>
            {
                prompted.TrySetResult();
                return Task.FromResult(SaveChoice.Cancel);
            };
            window.Close(); // the red button and ⌘W both come here
            await prompted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var state = (true, isClosed());
            window.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            await window.CloseWithPromptAsync();
            return state;
        });

        Assert.True(asked);
        Assert.False(closed);
    }

    [PdfViewerFact]
    public async Task Quitting_closes_each_window_in_turn_and_stops_at_a_Cancel()
    {
        string first = Write("first.pdf"), second = Write("second.pdf");

        var (quit, firstClosed, secondClosed) = await Headless.RunAsync(async () =>
        {
            var (a, aClosed, _) = await EditedAsync(first, SaveChoice.DontSave);
            var (b, bClosed, _) = await EditedAsync(second, SaveChoice.Cancel);
            var result = await App.CloseAllAsync([a, b]);
            var state = (result, aClosed(), bClosed());
            b.AskToSave = () => Task.FromResult(SaveChoice.DontSave);
            await b.CloseWithPromptAsync();
            return state;
        });

        Assert.False(quit);
        Assert.True(firstClosed);
        Assert.False(secondClosed);
    }
}
