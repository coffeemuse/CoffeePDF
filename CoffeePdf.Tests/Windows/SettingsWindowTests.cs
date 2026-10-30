using CoffeePdf.Core.Settings;
using CoffeePdf.Platform;
using CoffeePdf.Windows;

namespace CoffeePdf.Tests.Windows;

/// <summary>Spec §5: the Markup author, defaulting to the full name, and the Default PDF app button.</summary>
public sealed class SettingsWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class FakeDefaultApp(bool supported, bool isDefault) : IDefaultPdfApp
    {
        public bool IsDefault { get; private set; } = isDefault;
        public bool IsSupported => supported;
        public bool IsCoffeePdf() => IsDefault;
        public bool MakeCoffeePdf() => IsDefault = true;
    }

    [Fact]
    public async Task It_shows_the_author_set_with_the_full_name_as_the_placeholder()
    {
        var store = new SettingsStore(_dir) { MarkupAuthor = "Set Name" };

        var (text, placeholder) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(store, "Default Name", new FakeDefaultApp(true, true));
            return Task.FromResult((window.AuthorBox.Text, window.AuthorBox.PlaceholderText));
        });

        Assert.Equal("Set Name", text);
        Assert.Equal("Default Name", placeholder);
    }

    [Fact]
    public async Task Typing_a_name_keeps_it_and_clearing_it_goes_back_to_the_full_name()
    {
        var store = new SettingsStore(_dir);

        var (typed, cleared) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(store, "Default Name", new FakeDefaultApp(true, true));
            window.AuthorBox.Text = "New Name";
            var afterTyping = new SettingsStore(_dir).MarkupAuthor;
            window.AuthorBox.Text = "";
            return Task.FromResult((afterTyping, new SettingsStore(_dir).MarkupAuthor));
        });

        Assert.Equal("New Name", typed);
        Assert.Null(cleared);
    }

    [Fact]
    public async Task A_name_that_cant_be_kept_stays_in_the_box_and_nothing_crashes()
    {
        // A settings folder that can't be made, since its parent is a file: a failed write, as a full disk or a read-only
        // folder would give.
        var file = Path.Combine(_dir, "a-file");
        File.WriteAllText(file, "");
        var store = new SettingsStore(Path.Combine(file, "settings"));

        var (exception, text) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(store, "Default Name", new FakeDefaultApp(true, true));
            var thrown = Record.Exception(() => window.AuthorBox.Text = "New Name");
            return Task.FromResult((thrown, window.AuthorBox.Text));
        });

        Assert.Null(exception);
        Assert.Equal("New Name", text);
    }

    [Fact]
    public async Task The_button_makes_CoffeePDF_the_default_and_then_goes()
    {
        var defaultApp = new FakeDefaultApp(true, false);

        var (before, after, status) = await Headless.RunAsync(() =>
        {
            var window = new SettingsWindow(new SettingsStore(_dir), "Default Name", defaultApp);
            var shown = window.MakeDefaultButton.IsVisible;
            window.MakeDefault();
            return Task.FromResult((shown, window.MakeDefaultButton.IsVisible, window.DefaultAppStatus.Text));
        });

        Assert.True(before);
        Assert.True(defaultApp.IsDefault);
        Assert.False(after);
        Assert.Equal("CoffeePDF opens PDFs.", status);
    }

    [Fact]
    public async Task Where_theres_no_default_app_to_set_the_section_is_hidden()
    {
        var shown = await Headless.RunAsync(() =>
            Task.FromResult(new SettingsWindow(new SettingsStore(_dir), "Default Name", new FakeDefaultApp(false, false)).DefaultAppSection.IsVisible));

        Assert.False(shown);
    }
}
