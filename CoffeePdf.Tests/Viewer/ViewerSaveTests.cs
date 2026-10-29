using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Tests.Viewer;

/// <summary>Spec §14 items 1 and 7: what a save keeps.</summary>
[Trait("Category", "PdfViewer")]
public sealed class ViewerSaveTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [PdfViewerFact]
    public async Task Saving_a_signed_pdf_appends_and_keeps_the_signature()
    {
        var original = SignedPdf.Build();
        var path = Write("signed.pdf", original);
        Probe.Keep(path, "signed-before.pdf");

        await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            var added = await viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            Assert.True(added >= 0, "AddStickyNoteAsync failed (returned -1): the note couldn't be added headless");
            await viewer.SaveAsync();
            return true;
        });

        var saved = File.ReadAllBytes(path);
        Probe.Keep(path, "signed-after.pdf");
        Assert.True(saved.Length > original.Length, "The save added nothing to the file");
        Assert.Equal(original, saved[..original.Length]);
        SignedPdf.VerifyFirstSignature(saved);
    }

    [PdfViewerFact]
    public async Task A_save_keeps_another_apps_note()
    {
        var path = Write("noted.pdf", MinimalPdf.Build(withForeignNote: true));

        var annotations = await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            var added = await viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            Assert.True(added >= 0, "AddStickyNoteAsync failed (returned -1): the note couldn't be added headless");
            await viewer.SaveAsync();
            viewer.CloseDocument();
            var reopened = await Viewers.OpenAsync(path);
            return await reopened.GetAnnotationsAsync(0, CancellationToken.None);
        });

        Assert.Contains(annotations, a => a.Author == MinimalPdf.ForeignNoteAuthor && a.Contents == MinimalPdf.ForeignNoteContents);
        Assert.Contains(annotations, a => a.Contents == "Reviewed");
    }

    [PdfViewerFact]
    public async Task A_save_clears_the_dirty_flag()
    {
        var path = Write("plain.pdf", MinimalPdf.Build());

        var (afterEdit, afterSave) = await Headless.RunAsync(async () =>
        {
            var viewer = await Viewers.OpenAsync(path);
            var added = await viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            Assert.True(added >= 0, "AddStickyNoteAsync failed (returned -1): the note couldn't be added headless");
            var edited = viewer.IsDirty;
            await viewer.SaveAsync();
            return (edited, viewer.IsDirty);
        });

        Assert.True(afterEdit);
        Assert.False(afterSave);
    }
}
