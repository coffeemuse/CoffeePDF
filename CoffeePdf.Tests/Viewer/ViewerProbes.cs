using System.Collections.Concurrent;
using System.Diagnostics;
using CoffeePdf.Tests.Fixtures;
using Xunit.Abstractions;

namespace CoffeePdf.Tests.Viewer;

/// <summary>
/// Spec §14 items 2 and 3: questions with no right answer to assert, so these print what the PdfViewer does, for the
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
            await viewer.AddStickyNoteAsync(0, 300, 400, "Probe");
            await viewer.SaveAsync();
            viewer.CloseDocument();
            var reopened = await Viewers.OpenAsync(path);
            return (await reopened.GetAnnotationsAsync(0, CancellationToken.None))
                .Where(a => a.Contents == "Probe").Select(a => a.Author).ToList();
        });

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
                await viewer.AddStickyNoteAsync(0, 300, 400, "Probe");
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
