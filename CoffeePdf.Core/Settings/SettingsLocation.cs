namespace CoffeePdf.Core.Settings;

/// <summary>Where CoffeePDF keeps <c>settings.json</c> and <c>recent.json</c> (spec §5).</summary>
public static class SettingsLocation
{
    /// <summary>Overrides the folder, for tests and development, as the Helper's <c>HEALDAN_HELPER_DEV_ROOT</c> does.</summary>
    public const string OverrideVariable = "COFFEEPDF_SETTINGS_DIR";

    /// <summary>
    /// <c>~/Library/Application Support/CoffeePDF/</c> on macOS, and <c>$XDG_CONFIG_HOME/coffeepdf/</c> (or
    /// <c>~/.config/coffeepdf/</c>) elsewhere, unless <see cref="OverrideVariable"/> is set.
    /// </summary>
    public static string Folder()
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } overridden)
            return overridden;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "CoffeePDF");
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".config");
        return Path.Combine(config, "coffeepdf");
    }
}
