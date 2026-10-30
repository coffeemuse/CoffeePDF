using System.Text.Json.Nodes;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void With_no_file_Healdan_support_is_off_and_the_author_follows_the_full_name()
    {
        var store = new SettingsStore(Path.Combine(_dir, "not-yet"));

        Assert.False(store.HealdanEnabled);
        Assert.Null(store.MarkupAuthor);
    }

    [Fact]
    public void Settings_are_kept_in_the_shape_the_Helper_reads()
    {
        var store = new SettingsStore(_dir);

        store.HealdanEnabled = true;
        store.MarkupAuthor = "  Test Reviewer ";

        var json = JsonNode.Parse(File.ReadAllText(store.FilePath))!;
        Assert.True(json["healdan"]!["enabled"]!.GetValue<bool>());
        Assert.Equal("Test Reviewer", json["markup"]!["author"]!.GetValue<string>());
        var again = new SettingsStore(_dir);
        Assert.True(again.HealdanEnabled);
        Assert.Equal("Test Reviewer", again.MarkupAuthor);
    }

    [Fact]
    public void A_blank_author_goes_back_to_the_full_name()
    {
        var store = new SettingsStore(_dir) { MarkupAuthor = "Test Reviewer" };

        store.MarkupAuthor = " ";

        Assert.Null(store.MarkupAuthor);
        Assert.Null(JsonNode.Parse(File.ReadAllText(store.FilePath))!["markup"]!["author"]);
    }

    [Fact]
    public void Keys_this_version_doesnt_know_are_kept()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, """{ "later": { "thing": 1 }, "healdan": { "enabled": true, "extra": "x" } }""");

        store.MarkupAuthor = "Test Reviewer";

        var json = JsonNode.Parse(File.ReadAllText(store.FilePath))!;
        Assert.Equal(1, json["later"]!["thing"]!.GetValue<int>());
        Assert.Equal("x", json["healdan"]!["extra"]!.GetValue<string>());
        Assert.True(store.HealdanEnabled);
    }

    [Fact]
    public void A_file_that_cant_be_read_gives_the_defaults_and_the_next_change_replaces_it()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ not json");

        Assert.False(store.HealdanEnabled);
        store.HealdanEnabled = true;

        Assert.True(new SettingsStore(_dir).HealdanEnabled);
    }

    [Fact]
    public void Sections_of_the_wrong_shape_give_the_defaults_and_the_next_change_still_works()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, """{ "healdan": 5, "markup": "text" }""");

        Assert.False(store.HealdanEnabled);
        Assert.Null(store.MarkupAuthor);

        store.HealdanEnabled = true;
        store.MarkupAuthor = "Test Reviewer";

        var again = new SettingsStore(_dir);
        Assert.True(again.HealdanEnabled);
        Assert.Equal("Test Reviewer", again.MarkupAuthor);
    }
}
