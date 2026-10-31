using System.Text;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Locks;

namespace CoffeePdf.Core.Tests.Locks;

/// <summary>Spec §6.8: the lock file beside an editable copy, which the Helper looks for.</summary>
public sealed class CopyLockTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void The_lock_sits_beside_the_copy_named_for_it() =>
        Assert.Equal(Path.Combine(_dir, ".~coffeepdf.Settlement Agreement #1234 v2.1.pdf#"),
            CopyLock.PathFor(Path.Combine(_dir, "Settlement Agreement #1234 v2.1.pdf")));

    [Fact]
    public void Its_13_added_bytes_fit_beside_the_longest_copy_name()
    {
        // The Helper keeps copies' names to 241 bytes (the Desktop Clients page), so their records fit in 255.
        var copy = Path.Combine(_dir, new string('a', 237) + ".pdf");

        CopyLock.Write(copy, 4321, DateTimeOffset.Now);

        Assert.Equal(254, Encoding.UTF8.GetByteCount(Path.GetFileName(CopyLock.PathFor(copy))));
        Assert.True(File.Exists(CopyLock.PathFor(copy)));
    }

    [Fact]
    public void It_holds_the_process_and_when_the_window_opened_and_replaces_a_crashs_lock()
    {
        var copy = Path.Combine(_dir, "Report #1 v1.pdf");
        File.WriteAllText(CopyLock.PathFor(copy), "left by a crash");
        var opened = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero);

        CopyLock.Write(copy, 4321, opened);

        var json = JsonNode.Parse(File.ReadAllText(CopyLock.PathFor(copy)))!;
        Assert.Equal(4321, json["pid"]!.GetValue<int>());
        Assert.Equal(opened, DateTimeOffset.Parse(json["opened"]!.GetValue<string>()));
    }

    [Fact]
    public void Removing_takes_the_lock_and_a_lock_thats_gone_is_fine()
    {
        var copy = Path.Combine(_dir, "Report #1 v1.pdf");
        CopyLock.Write(copy, 4321, DateTimeOffset.Now);

        Assert.True(CopyLock.TryRemove(copy));
        Assert.False(File.Exists(CopyLock.PathFor(copy)));
        Assert.False(CopyLock.TryRemove(copy));
    }
}
