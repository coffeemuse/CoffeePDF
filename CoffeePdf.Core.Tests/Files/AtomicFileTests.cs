using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Tests.Files;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void It_writes_a_new_file()
    {
        var path = Path.Combine(_dir, "new.pdf");

        AtomicFile.Write(path, "new"u8);

        Assert.Equal("new"u8.ToArray(), File.ReadAllBytes(path));
    }

    [Fact]
    public void It_replaces_a_files_bytes_and_leaves_no_temporary_file()
    {
        var path = Path.Combine(_dir, "doc.pdf");
        File.WriteAllBytes(path, "old, and longer"u8.ToArray());

        AtomicFile.Write(path, "new"u8);

        Assert.Equal("new"u8.ToArray(), File.ReadAllBytes(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void It_keeps_the_files_permissions()
    {
        var path = Path.Combine(_dir, "private.pdf");
        File.WriteAllBytes(path, "old"u8.ToArray());
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        AtomicFile.Write(path, "new"u8);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void A_name_too_long_for_a_suffix_still_works()
    {
        // Healdan's copies are named up to 241 bytes (spec §6.8), and a file name can't pass 255.
        var path = Path.Combine(_dir, new string('n', 241) + ".pdf");

        AtomicFile.Write(path, "new"u8);

        Assert.Equal("new"u8.ToArray(), File.ReadAllBytes(path));
    }

    [Fact]
    public void A_failed_rename_leaves_the_target_and_no_temporary_file()
    {
        var target = Directory.CreateDirectory(Path.Combine(_dir, "a-folder.pdf")).FullName;

        Assert.ThrowsAny<IOException>(() => AtomicFile.Write(target, "new"u8));

        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void A_folder_that_cant_be_written_leaves_the_file_as_it_was()
    {
        var path = Path.Combine(_dir, "doc.pdf");
        File.WriteAllBytes(path, "old"u8.ToArray());
        var mode = File.GetUnixFileMode(_dir);
        File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => AtomicFile.Write(path, "new"u8));
        }
        finally
        {
            File.SetUnixFileMode(_dir, mode);
        }

        Assert.Equal("old"u8.ToArray(), File.ReadAllBytes(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }
}
