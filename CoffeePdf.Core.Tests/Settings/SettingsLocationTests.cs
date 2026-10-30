using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

/// <summary>One class, since its tests change environment variables and xUnit runs a class's tests one at a time.</summary>
public sealed class SettingsLocationTests : IDisposable
{
    private readonly string? _override = Environment.GetEnvironmentVariable(SettingsLocation.OverrideVariable);
    private readonly string? _xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, _override);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _xdg);
    }

    [Fact]
    public void The_override_wins()
    {
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, "/tmp/coffeepdf-settings");

        Assert.Equal("/tmp/coffeepdf-settings", SettingsLocation.Folder());
    }

    [Fact]
    public void Otherwise_its_the_platforms_own_place()
    {
        Environment.SetEnvironmentVariable(SettingsLocation.OverrideVariable, null);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "/tmp/xdg");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var expected = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support", "CoffeePDF")
            : Path.Combine("/tmp/xdg", "coffeepdf");
        Assert.Equal(expected, SettingsLocation.Folder());
    }
}
