using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §6.4, §7 and §16 Decision 1: every save writes the author, keeps signatures, and says when it fails.</summary>
[Trait("Category", "PdfViewer")]
public sealed class DocumentWindowSaveTests : IDisposable
{
    private const string Author = TestWindows.Author;

    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [PdfViewerFact]
    public async Task A_save_writes_the_markup_author_into_a_new_note()
    {
        // This also watches the PdfViewer: the author is written only into annotations it names (/NM).
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var saved = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            return await window.SaveAsync();
        });

        Assert.True(saved);
        var note = Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(path), 0));
        Assert.Equal(Author, note.Author);
    }

    [PdfViewerFact]
    public async Task Another_apps_note_keeps_its_own_author()
    {
        var path = Write("noted.pdf", MinimalPdf.Build(withForeignNote: true));

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            return await window.SaveAsync();
        });

        var authors = PdfAnnotations.Read(File.ReadAllBytes(path), 0).Select(a => a.Author).ToList();
        Assert.Equal(new string?[] { MinimalPdf.ForeignNoteAuthor, Author }, authors);
    }

    [PdfViewerFact]
    public async Task A_signed_pdf_stays_signed_through_two_saves_and_its_notes_get_the_author()
    {
        var original = SignedPdf.Build();
        var path = Write("signed.pdf", original);

        await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "First");
            await window.SaveAsync();
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "Second");
            return await window.SaveAsync();
        });

        var saved = File.ReadAllBytes(path);
        Assert.Equal(original, saved[..original.Length]);
        SignedPdf.VerifyFirstSignature(saved);
        var notes = PdfAnnotations.Read(saved, 0).Where(a => a.Subtype == AnnotationSubtype.Text).ToList();
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(Author, n.Author));
    }

    [PdfViewerFact]
    public async Task The_title_says_Edited_until_the_save()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (editedTitle, editedUnsaved, savedTitle, savedUnsaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var (title, unsaved) = (window.Title, window.HasUnsavedChanges);
            await window.SaveAsync();
            return (title, unsaved, window.Title, window.HasUnsavedChanges);
        });

        Assert.Equal("fixture.pdf — Edited", editedTitle);
        Assert.True(editedUnsaved);
        Assert.Equal("fixture.pdf", savedTitle);
        Assert.False(savedUnsaved);
    }

    [PdfViewerFact]
    public async Task An_undo_after_a_save_leaves_unsaved_changes()
    {
        // The PdfViewer's IsDirty misses it (spec §16, the planning probe).
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var unsaved = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveAsync();
            await window.Viewer.UndoAsync();
            return window.HasUnsavedChanges;
        });

        Assert.True(unsaved);
    }

    [PdfViewerFact]
    public async Task A_failed_save_keeps_the_changes_unsaved_and_says_so()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());
        var mode = File.GetUnixFileMode(_dir);

        var (saved, unsaved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                return (await window.SaveAsync(), window.HasUnsavedChanges, errorList);
            }
            finally
            {
                File.SetUnixFileMode(_dir, mode);
            }
        });

        Assert.False(saved);
        Assert.True(unsaved);
        Assert.Contains("“fixture.pdf” couldn’t be saved", Assert.Single(errors));
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
    }

    [PdfViewerFact]
    public async Task The_PdfViewers_own_saving_is_off()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (allowed, autoSave) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            return (window.Viewer.AllowDocumentSaving, window.Viewer.AutoSave);
        });

        Assert.False(allowed);
        Assert.False(autoSave);
    }
}
