using Avalonia.Controls;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Tests.Fixtures;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

[Trait("Category", "PdfViewer")]
public sealed class SaveAsAndRecentTests : IDisposable
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
    public async Task Save_As_writes_the_new_file_moves_the_window_to_it_and_leaves_the_old_one()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());
        var copy = Path.Combine(_dir, "copy.pdf");

        var (shown, title, recent) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
            await window.SaveToAsync(copy);
            return (window.FilePath, window.Title, App.Current.Recent.Paths);
        });

        Assert.Equal(copy, shown);
        Assert.Equal("copy.pdf", title);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
        Assert.Equal(MinimalPdf.Build(), File.ReadAllBytes(path));
        Assert.Equal(copy, recent[0]);
    }

    [PdfViewerFact]
    public async Task An_ordinary_save_leaves_Open_Recent_alone()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());

        var (saved, changes, listed) = await Headless.RunAsync(async () =>
        {
            var (window, _) = await TestWindows.OpenAsync(path);
            var changes = 0;
            void Count(object? sender, EventArgs e) => changes++;
            App.Current.Recent.Changed += Count;
            try
            {
                await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
                var saved = await window.SaveAsync();
                return (saved, changes, App.Current.Recent.Paths.Contains(path));
            }
            finally
            {
                App.Current.Recent.Changed -= Count;
                window.Close();
            }
        });

        Assert.True(saved);
        Assert.Equal(0, changes);
        Assert.False(listed);
    }

    [PdfViewerFact]
    public async Task A_problem_updating_Open_Recent_does_not_turn_a_written_save_into_a_failed_one()
    {
        var path = Write("fixture.pdf", MinimalPdf.Build());
        var copy = Path.Combine(_dir, "copy.pdf");

        var (saved, errors, unsaved) = await Headless.RunAsync(async () =>
        {
            var (window, errors) = await TestWindows.OpenAsync(path);
            void Fail(object? sender, EventArgs e) => throw new InvalidOperationException("A menu that can't be updated");
            App.Current.Recent.Changed += Fail;
            try
            {
                await window.Viewer.AddStickyNoteAsync(0, 300, 400, "Reviewed");
                var saved = await window.SaveToAsync(copy);
                return (saved, errors, window.HasUnsavedChanges);
            }
            finally
            {
                App.Current.Recent.Changed -= Fail;
                window.Close();
            }
        });

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.False(unsaved);
        Assert.Equal(TestWindows.Author, Assert.Single(PdfAnnotations.Read(File.ReadAllBytes(copy), 0)).Author);
    }

    [PdfViewerFact]
    public async Task Open_Recent_lists_the_files_newest_first_then_Clear_Menu()
    {
        string a = Write("a.pdf", MinimalPdf.Build()), b = Write("b.pdf", MinimalPdf.Build());

        var headers = await Headless.RunAsync(() =>
        {
            var window = new DocumentWindow();
            window.ShowRecent([b, a]);
            var shown = window.MenuItem("Open Recent").Menu!.Items.Select(i => i is NativeMenuItemSeparator ? "—" : ((NativeMenuItem)i).Header).ToList();
            window.Close();
            return Task.FromResult(shown);
        });

        Assert.Equal(new string?[] { "b.pdf", "a.pdf", "—", "Clear Menu" }, headers);
    }

    [PdfViewerFact]
    public async Task Choosing_a_recent_file_opens_it_and_opening_a_file_adds_it()
    {
        var path = Write("recent.pdf", MinimalPdf.Build());

        var (opened, listed) = await Headless.RunAsync(async () =>
        {
            var window = new DocumentWindow();
            window.ShowRecent([path]);
            window.MenuItem("recent.pdf").Command!.Execute(null);
            window.Close();
            var shown = App.Current.Windows.FirstOrDefault(w => w.FilePath == path);
            if (shown is null)
                return (false, false);
            await TestWindows.WaitForAsync(() => shown.Viewer.HasDocument);
            shown.Close();
            return (true, App.Current.Recent.Paths.Contains(path));
        });

        Assert.True(opened);
        Assert.True(listed);
    }
}
