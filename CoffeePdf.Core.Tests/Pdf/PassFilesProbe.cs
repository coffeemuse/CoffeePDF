using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;
using Xunit.Abstractions;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>
/// The PDFs for Claude's pass on a test build (docs/DEVTOOLS.md, the devtools spec's §6.3), in the folder that
/// <see cref="PassFolderFactAttribute.Variable"/> names, outside the repository. One probe writes them before the pass,
/// and the other reads back what the test build's saves wrote into them. Run with: COFFEEPDF_PASS_DIR=&lt;folder&gt;
/// dotnet test CoffeePdf.Core.Tests --filter FullyQualifiedName~PassFilesProbe --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "Probe")]
public sealed class PassFilesProbe(ITestOutputHelper output)
{
    /// <summary>The authors a pass's PDFs may hold: the tests' made-up one, and that of the fixture's note from another app.</summary>
    private static readonly string[] Authors = ["Test Reviewer", MinimalPdf.ForeignNoteAuthor];

    private static string Folder => Environment.GetEnvironmentVariable(PassFolderFactAttribute.Variable)!;

    [PassFolderFact]
    public void Writes_the_pass_pdfs()
    {
        Directory.CreateDirectory(Folder);
        Write("plain.pdf", MinimalPdf.Build());
        Write("with-note.pdf", MinimalPdf.Build(withForeignNote: true));

        Assert.Empty(PdfAnnotations.Read(File.ReadAllBytes(Path.Combine(Folder, "plain.pdf")), pageIndex: 0));
        var note = Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(Path.Combine(Folder, "with-note.pdf")), pageIndex: 0));
        Assert.Equal(MinimalPdf.ForeignNoteAuthor, note.Author);
    }

    /// <summary>Each PDF's markups on its one page, and their authors, which must be one of <see cref="Authors"/>.</summary>
    [PassFolderFact]
    public void Reads_back_the_pass_pdfs()
    {
        var files = Directory.GetFiles(Folder, "*.pdf").Order().ToList();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var annotations = PdfAnnotations.Read(File.ReadAllBytes(file), pageIndex: 0);
            output.WriteLine($"{Path.GetFileName(file)}: {annotations.Count} annotation(s) on page 1");
            foreach (var annotation in annotations)
                output.WriteLine($"  {annotation.Subtype} by {annotation.Author ?? "no author"} ({annotation.Name ?? "no /NM"})");
            Assert.All(annotations.Where(annotation => annotation.Author is not null),
                annotation => Assert.Contains(annotation.Author!, Authors));
        }
    }

    private void Write(string name, byte[] pdf)
    {
        File.WriteAllBytes(Path.Combine(Folder, name), pdf);
        output.WriteLine($"Wrote {name}");
    }
}
