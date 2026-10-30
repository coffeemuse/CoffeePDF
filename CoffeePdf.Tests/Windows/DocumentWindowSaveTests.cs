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

        var (saved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            return (await window.SaveAsync(), errorList);
        });

        Assert.True(saved);
        Assert.Empty(errors);
        var note = Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(path), 0));
        Assert.Equal(Author, note.Author);
    }

    [PdfViewerFact]
    public async Task Another_apps_note_keeps_its_own_author()
    {
        var path = Write("noted.pdf", MinimalPdf.Build(withForeignNote: true));

        var (saved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            return (await window.SaveAsync(), errorList);
        });

        Assert.True(saved);
        Assert.Empty(errors);
        var authors = PdfAnnotations.Read(File.ReadAllBytes(path), 0).Select(a => a.Author).ToList();
        Assert.Equal(new string?[] { MinimalPdf.ForeignNoteAuthor, Author }, authors);
    }

    [PdfViewerFact]
    public async Task A_signed_pdf_stays_signed_through_two_saves_and_its_notes_get_the_author()
    {
        var original = SignedPdf.Build();
        var path = Write("signed.pdf", original);

        var (firstSaved, secondSaved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "First");
            var first = await window.SaveAsync();
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "Second");
            return (first, await window.SaveAsync(), errorList);
        });

        Assert.True(firstSaved);
        Assert.True(secondSaved);
        Assert.Empty(errors);
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

        var (editedTitle, editedUnsaved, saved, savedTitle, savedUnsaved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var (title, unsaved) = (window.Title, window.HasUnsavedChanges);
            var saveWorked = await window.SaveAsync();
            return (title, unsaved, saveWorked, window.Title, window.HasUnsavedChanges, errorList);
        });

        Assert.Equal("fixture.pdf — Edited", editedTitle);
        Assert.True(editedUnsaved);
        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal("fixture.pdf", savedTitle);
        Assert.False(savedUnsaved);
    }

    [PdfViewerFact]
    public async Task An_undo_after_a_save_leaves_unsaved_changes()
    {
        // The PdfViewer's IsDirty misses it (spec §16, the planning probe).
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (saved, unsavedAfterSave, unsavedAfterUndo, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var saveWorked = await window.SaveAsync();
            var unsaved = window.HasUnsavedChanges;
            await window.Viewer.UndoAsync();
            return (saveWorked, unsaved, window.HasUnsavedChanges, errorList);
        });

        // The save worked and left nothing unsaved, so the undo is what brings the unsaved changes back.
        Assert.True(saved);
        Assert.False(unsavedAfterSave);
        Assert.True(unsavedAfterUndo);
        Assert.Empty(errors);
    }

    [PdfViewerFact]
    public async Task A_save_in_progress_counts_as_unsaved()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (starting, snapshotTaken, finished, writing, saved, afterwards, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var saving = window.SaveAsync();
            var justStarted = window.HasUnsavedChanges;
            // The PdfViewer clears IsDirty as it takes its snapshot, and the file is written after that.
            await TestWindows.WaitForAsync(() => !window.Viewer.IsDirty || saving.IsCompleted);
            var (snapshot, done, unsavedMeanwhile) = (!window.Viewer.IsDirty, saving.IsCompleted, window.HasUnsavedChanges);
            var saveWorked = await saving;
            return (justStarted, snapshot, done, unsavedMeanwhile, saveWorked, window.HasUnsavedChanges, errorList);
        });

        Assert.True(starting);
        Assert.True(snapshotTaken, "The PdfViewer's snapshot never cleared IsDirty, so this saw nothing of the write");
        Assert.False(finished);
        Assert.True(writing);
        Assert.True(saved);
        Assert.False(afterwards);
        Assert.Empty(errors);
    }

    [PdfViewerFact]
    public async Task Two_saves_at_once_leave_the_later_one_on_disk()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (firstSaved, secondSaved, unsaved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "First");
            var firstSaving = window.SaveAsync();
            await window.Viewer.AddStickyNoteAsync(0, 200, 200, "Second");
            var secondSaving = window.SaveAsync();
            return (await firstSaving, await secondSaving, window.HasUnsavedChanges, errorList);
        });

        Assert.True(firstSaved);
        Assert.True(secondSaved);
        Assert.False(unsaved);
        Assert.Empty(errors);
        var notes = PdfAnnotations.Read(File.ReadAllBytes(path), 0).Where(a => a.Subtype == AnnotationSubtype.Text).ToList();
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(Author, n.Author));
    }

    [PdfViewerFact]
    public async Task An_undo_while_the_file_is_written_leaves_unsaved_changes()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (saved, unsaved, errors) = await Headless.RunAsync(async () =>
        {
            var (window, errorList) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            var saving = window.SaveAsync();
            await TestWindows.WaitForAsync(() => !window.Viewer.IsDirty || saving.IsCompleted);
            await window.Viewer.UndoAsync();
            var saveWorked = await saving;
            return (saveWorked, window.HasUnsavedChanges, errorList);
        });

        Assert.True(saved);
        Assert.True(unsaved);
        Assert.Empty(errors);
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
    public async Task A_failure_to_show_the_error_doesnt_escape_the_save()
    {
        // The window may have closed while the file was written, and then showing the error throws.
        var path = Write("fixture.pdf", MinimalPdf.Build());
        var mode = File.GetUnixFileMode(_dir);

        var (saved, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            window.ReportError = (_, _) => throw new InvalidOperationException("The window has closed");
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                return (await window.SaveAsync(), window.HasUnsavedChanges);
            }
            finally
            {
                File.SetUnixFileMode(_dir, mode);
            }
        });

        Assert.False(saved);
        Assert.True(unsaved);
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
