namespace CoffeePdf.Core.Settings;

/// <summary>The first launch's one question (spec §5): with the Helper installed, whether to use CoffeePDF with Healdan.</summary>
public static class FirstLaunch
{
    /// <summary>
    /// Asks through <paramref name="ask"/>, unless it was asked before, Healdan support is on already, or the Helper
    /// isn't installed. An answer counts as asked, and yes turns support on. A launch that ends before the answer asks
    /// again next time.
    /// </summary>
    public static async Task AskAboutHealdanAsync(SettingsStore settings, Func<bool> helperInstalled, Func<Task<bool>> ask)
    {
        if (settings.HealdanAsked || settings.HealdanEnabled || !helperInstalled())
            return;
        var yes = await ask();
        settings.HealdanAsked = true;
        if (yes)
            settings.HealdanEnabled = true;
    }
}
