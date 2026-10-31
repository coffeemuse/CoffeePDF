using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

/// <summary>Spec §5: at first launch, with the Helper installed, CoffeePDF asks once whether to use it with Healdan.</summary>
public sealed class FirstLaunchTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private async Task<int> AskAsync(SettingsStore store, bool installed, bool answer)
    {
        var asked = 0;
        await FirstLaunch.AskAboutHealdanAsync(store, () => installed, () =>
        {
            asked++;
            return Task.FromResult(answer);
        });
        return asked;
    }

    [Fact]
    public async Task Yes_turns_Healdan_support_on()
    {
        var store = new SettingsStore(_dir);

        Assert.Equal(1, await AskAsync(store, installed: true, answer: true));
        Assert.True(store.HealdanEnabled);
        Assert.True(store.HealdanAsked);
    }

    [Fact]
    public async Task Not_now_leaves_it_off_and_it_isnt_asked_again()
    {
        var store = new SettingsStore(_dir);

        await AskAsync(store, installed: true, answer: false);

        Assert.False(store.HealdanEnabled);
        Assert.Equal(0, await AskAsync(store, installed: true, answer: true));
        Assert.False(store.HealdanEnabled);
    }

    [Fact]
    public async Task Without_the_Helper_it_isnt_asked() =>
        Assert.Equal(0, await AskAsync(new SettingsStore(_dir), installed: false, answer: true));

    [Fact]
    public async Task With_support_on_already_it_isnt_asked() =>
        Assert.Equal(0, await AskAsync(new SettingsStore(_dir) { HealdanEnabled = true }, installed: true, answer: false));
}
