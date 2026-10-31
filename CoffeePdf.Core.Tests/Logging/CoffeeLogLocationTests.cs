using CoffeePdf.Core.Logging;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Logging;

/// <summary>In one collection with the other tests that change environment variables, which xUnit runs one at a time.</summary>
[Collection("Environment variables")]
public sealed class CoffeeLogLocationTests : IDisposable
{
    private readonly string? _override = Environment.GetEnvironmentVariable(CoffeeLog.OverrideVariable);

    public void Dispose() => Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, _override);

    [Fact]
    public void The_override_wins()
    {
        Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, "/tmp/coffeepdf-log");

        Assert.Equal("/tmp/coffeepdf-log", CoffeeLog.Folder());
    }

    [Fact]
    public void Otherwise_its_the_Macs_Logs_folder_or_beside_the_settings()
    {
        Environment.SetEnvironmentVariable(CoffeeLog.OverrideVariable, null);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var expected = OperatingSystem.IsMacOS() ? Path.Combine(home, "Library", "Logs", "CoffeePDF") : SettingsLocation.Folder();
        Assert.Equal(expected, CoffeeLog.Folder());
    }
}
