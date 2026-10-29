using System.Text;
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Tests.Files;

public sealed class PdfFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-core-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(content));
        return path;
    }

    [Fact]
    public void A_file_starting_with_the_header_is_a_pdf() =>
        Assert.True(PdfFile.IsPdf(Write("a.pdf", "%PDF-1.7\n%%EOF\n")));

    [Fact]
    public void Junk_before_the_header_is_allowed_within_the_window() =>
        Assert.True(PdfFile.IsPdf(Write("junk.pdf", new string('x', PdfFile.HeaderWindow - 5) + "%PDF-1.7\n")));

    [Fact]
    public void A_header_past_the_window_isnt_found() =>
        Assert.False(PdfFile.IsPdf(Write("late.pdf", new string('x', PdfFile.HeaderWindow - 4) + "%PDF-1.7\n")));

    [Fact]
    public void The_name_doesnt_count() =>
        Assert.False(PdfFile.IsPdf(Write("notes.pdf", "just some text")));

    [Fact]
    public void A_missing_file_isnt_a_pdf() =>
        Assert.False(PdfFile.IsPdf(Path.Combine(_dir, "missing.pdf")));

    [Fact]
    public void A_folder_isnt_a_pdf() =>
        Assert.False(PdfFile.IsPdf(_dir));

    [Fact]
    public void An_empty_path_isnt_a_pdf() =>
        Assert.False(PdfFile.IsPdf(""));
}
