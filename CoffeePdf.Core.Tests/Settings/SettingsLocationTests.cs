using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

/// <summary>In one collection with the other tests that change environment variables, which xUnit runs one at a time.</summary>
[Collection("Environment variables")]
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
