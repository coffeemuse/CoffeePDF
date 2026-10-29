using System.Collections.Concurrent;
using System.Diagnostics;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;
using Xunit.Abstractions;

namespace CoffeePdf.Tests.Viewer;

/// <summary>
/// Spec §14 items 2, 3 and 4: questions with no right answer to assert, so these print what the PdfViewer does, for the
/// findings. Run with: dotnet test --filter Category=Probe --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "PdfViewer")]
[Trait("Category", "Probe")]
public sealed class ViewerProbes(ITestOutputHelper output) : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-probes-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [PdfViewerFact]
    public async Task What_author_a_new_note_gets()
    {
        var path = Write("author.pdf", MinimalPdf.Build());

        var authors = await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            var added = await viewer.AddStickyNoteAsync(0, 300, 400, "Probe");
            Assert.True(added >= 0, "AddStickyNoteAsync failed (returned -1): the note couldn't be added headless");
            await viewer.SaveAsync();
            viewer.CloseDocument();
            var reopened = await Viewers.OpenAsync(path);
            return (await reopened.GetAnnotationsAsync(0, CancellationToken.None))
                .Where(a => a.Contents == "Probe").Select(a => a.Author).ToList();
        });

        Assert.NotEmpty(authors);
        output.WriteLine($"A new note's author: {(authors.Count == 0 ? "(note not found)" : authors[0] ?? "(none)")}");
        output.WriteLine($"For comparison, this account's user name is {Environment.UserName}");
    }

    [PdfViewerFact]
    public async Task How_a_save_reaches_the_disk()
    {
        foreach (var (name, bytes) in new[] { ("plain.pdf", MinimalPdf.Build()), ("signed.pdf", SignedPdf.Build()) })
        {
            var path = Write(name, bytes);
            var inodeBefore = Inode(path);
            var events = new ConcurrentQueue<string>();
            using var watcher = new FileSystemWatcher(_dir);
            watcher.Changed += (_, e) => events.Enqueue($"Changed {e.Name}");
            watcher.Created += (_, e) => events.Enqueue($"Created {e.Name}");
            watcher.Deleted += (_, e) => events.Enqueue($"Deleted {e.Name}");
            watcher.Renamed += (_, e) => events.Enqueue($"Renamed {e.OldName} -> {e.Name}");
            watcher.EnableRaisingEvents = true;

            var took = await Headless.RunAsync(async () =>
            {
                var viewer = await Viewers.OpenAsync(path);
                var added = await viewer.AddStickyNoteAsync(0, 300, 400, "Probe");
                Assert.True(added >= 0, "AddStickyNoteAsync failed (returned -1): the note couldn't be added headless");
                var clock = Stopwatch.StartNew();
                await viewer.SaveAsync();
                return clock.Elapsed;
            });
            // A probe's one fixed wait: FSEvents delivers in batches, and there's no condition to wait for, since
            // which events come is the question.
            await Task.Delay(TimeSpan.FromSeconds(2));

            output.WriteLine($"{name}: SaveAsync took {took.TotalMilliseconds:F0} ms; size {bytes.Length} -> {new FileInfo(path).Length}");
            output.WriteLine($"{name}: inode {inodeBefore} -> {Inode(path)} ({(inodeBefore == Inode(path) ? "written in place" : "replaced")})");
            output.WriteLine($"{name}: events: {string.Join("; ", events)}");
        }
    }

    /// <summary>
    /// Spec §14 item 4: what the PdfViewer does in the app's own window without the licence key. This probe passes
    /// whatever it observes, since what happens is the question: a window that shows the PDF, a load error, an error
    /// message, an exception or no answer at all.
    /// </summary>
    [PdfViewerUnlicensedFact]
    public async Task What_the_viewer_does_without_a_licence()
    {
        var path = Write("unlicensed.pdf", MinimalPdf.Build());

        var observed = await Headless.RunAsync(async () =>
        {
            var stage = "constructing the DocumentWindow";
            try
            {
                var window = new DocumentWindow();
                var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Viewer.DocumentLoaded += (_, _) => answer.TrySetResult($"DocumentLoaded, {window.Viewer.PageCount} page(s)");
                window.Viewer.LoadError += (_, e) => answer.TrySetResult($"LoadError: {e.Message}");
                stage = "showing it";
                window.Show();
                stage = "loading the PDF";
                window.Load(path);
                stage = "waiting for the answer";
                var result = await Task.WhenAny(answer.Task, Task.Delay(TimeSpan.FromSeconds(15)));
                var outcome = result == answer.Task ? answer.Task.Result : "no DocumentLoaded or LoadError within 15 s";
                return $"{outcome}; Viewer.ErrorMessage: {window.Viewer.ErrorMessage ?? "(none)"}";
            }
            catch (Exception e)
            {
                var chain = new List<string>();
                for (var inner = e; inner is not null; inner = inner.InnerException)
                    chain.Add($"{inner.GetType().Name}: {inner.Message}");
                return $"threw while {stage}: {string.Join(" <- ", chain)}";
            }
        });

        output.WriteLine($"Without a licence key: {observed}");
    }

    private static string Inode(string path)
    {
        var start = new ProcessStartInfo("stat") { RedirectStandardOutput = true };
        foreach (var arg in OperatingSystem.IsMacOS() ? new[] { "-f", "%i" } : new[] { "-c", "%i" })
            start.ArgumentList.Add(arg);
        start.ArgumentList.Add(path);
        using var stat = Process.Start(start)!;
        var inode = stat.StandardOutput.ReadToEnd().Trim();
        stat.WaitForExit();
        return inode;
    }
}
