using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>Spec §4.3 and §6.1: the Helper's files, and which files are its local copies.</summary>
public sealed class HelperFoldersTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void The_Helpers_files_are_in_its_support_folder()
    {
        var folders = new HelperFolders(_dir);

        Assert.Equal(Path.Combine(_dir, "Copies"), folders.Copies);
        Assert.Equal(Path.Combine(_dir, "Temp"), folders.Temp);
        Assert.Equal(Path.Combine(_dir, "channel.json"), folders.ChannelFile);
    }

    [Fact]
    public void Only_a_file_directly_in_Copies_is_a_copy()
    {
        var folders = new HelperFolders(_dir);

        Assert.True(folders.HoldsCopy(Path.Combine(folders.Copies, "Settlement Agreement #1234 v2.1.pdf")));
        Assert.False(folders.HoldsCopy(Path.Combine(folders.Copies, "inner", "Report.pdf")));
        Assert.False(folders.HoldsCopy(Path.Combine(_dir, "Copies from example.com", "Report #1 v1.pdf")));
        Assert.False(folders.HoldsCopy(Path.Combine(_dir, "Report.pdf")));
    }

    [Fact]
    public void A_copy_reached_through_a_link_is_a_copy()
    {
        var folders = new HelperFolders(_dir);
        Directory.CreateDirectory(folders.Copies);
        var link = Path.Combine(_dir, "linked");
        Directory.CreateSymbolicLink(link, folders.Copies);

        Assert.True(folders.HoldsCopy(Path.Combine(link, "Report #1 v1.pdf")));
    }

    [Fact]
    public void Names_are_compared_in_composed_form_as_the_Helper_compares_them()
    {
        var folders = new HelperFolders(Path.Combine(_dir, "Café"));

        Assert.True(folders.HoldsCopy(Path.Combine(_dir, "Café", "Copies", "Report #1 v1.pdf")));
    }
}
