using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

public sealed class RecentDocumentsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, []);
        return path;
    }

    [Fact]
    public void Files_are_listed_newest_first_once_each()
    {
        var recent = new RecentDocuments(_dir);
        string a = Touch("a.pdf"), b = Touch("b.pdf");

        recent.Add(a);
        recent.Add(b);
        recent.Add(a);

        Assert.Equal(new[] { a, b }, recent.Paths);
    }

    [Fact]
    public void The_list_holds_the_newest_ten()
    {
        var recent = new RecentDocuments(_dir);
        var files = Enumerable.Range(1, 12).Select(i => Touch($"{i}.pdf")).ToList();

        files.ForEach(recent.Add);

        Assert.Equal(Enumerable.Reverse(files).Take(RecentDocuments.Capacity), recent.Paths);
    }

    [Fact]
    public void Files_can_be_removed_and_the_list_cleared()
    {
        var recent = new RecentDocuments(_dir);
        string a = Touch("a.pdf"), b = Touch("b.pdf");
        recent.Add(a);
        recent.Add(b);

        recent.Remove(b);
        Assert.Equal(new[] { a }, recent.Paths);
        recent.Clear();
        Assert.Empty(recent.Paths);
    }

    [Fact]
    public void A_file_thats_gone_is_left_out()
    {
        var recent = new RecentDocuments(_dir);
        var a = Touch("a.pdf");
        recent.Add(a);

        File.Delete(a);

        Assert.Empty(recent.Paths);
    }

    [Fact]
    public void The_list_is_kept_and_each_change_is_announced()
    {
        var recent = new RecentDocuments(_dir);
        var changes = 0;
        recent.Changed += (_, _) => changes++;
        var a = Touch("a.pdf");

        recent.Add(a);

        Assert.Equal(1, changes);
        Assert.Equal(new[] { a }, new RecentDocuments(_dir).Paths);
    }

    [Fact]
    public void A_file_that_cant_be_read_gives_an_empty_list()
    {
        var recent = new RecentDocuments(_dir);
        File.WriteAllText(recent.FilePath, "{ not json");

        Assert.Empty(recent.Paths);
    }

    [Fact]
    public void A_list_that_cant_be_written_is_dropped_without_failing_the_caller()
    {
        var recent = new RecentDocuments(_dir);
        var a = Touch("a.pdf");
        var b = Touch("b.pdf");
        recent.Add(a);
        var mode = File.GetUnixFileMode(_dir);
        File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Assert.Null(Record.Exception(() => recent.Add(b)));
        }
        finally
        {
            File.SetUnixFileMode(_dir, mode);
        }

        Assert.Equal(new[] { a }, recent.Paths);
    }
}
