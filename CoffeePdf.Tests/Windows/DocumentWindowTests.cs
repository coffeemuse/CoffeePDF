using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

[Trait("Category", "PdfViewer")]
public sealed class DocumentWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [PdfViewerFact]
    public async Task A_window_shows_the_pdf_it_loads()
    {
        var path = Path.Combine(_dir, "fixture.pdf");
        File.WriteAllBytes(path, MinimalPdf.Build());

        var (title, pages, shown) = await Headless.RunAsync(async () =>
        {
            var window = new DocumentWindow();
            window.Show();
            await Viewers.ShowAndLoadAsync(window.Viewer, () => window.Load(path));
            return (window.Title, window.Viewer.PageCount, window.FilePath);
        });

        Assert.Equal("fixture.pdf", title);
        Assert.Equal(1, pages);
        Assert.Equal(path, shown);
    }
}
