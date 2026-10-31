using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>In one collection with the other tests that change environment variables, which xUnit runs one at a time.</summary>
[Collection("Environment variables")]
public sealed class HelperFoldersLocationTests : IDisposable
{
    private readonly string? _root = Environment.GetEnvironmentVariable(HelperFolders.DevRootVariable);
    private readonly string? _xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, _root);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _xdg);
    }

    [Fact]
    public void The_dev_root_holds_everything_when_its_set()
    {
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, "/tmp/helper-dev");

        Assert.Equal("/tmp/helper-dev", HelperFolders.ForThisUser().Support);
    }

    [Fact]
    public void Otherwise_its_the_platforms_own_place()
    {
        Environment.SetEnvironmentVariable(HelperFolders.DevRootVariable, null);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", "/tmp/xdg-data");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var expected = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support", "Healdan Helper")
            : Path.Combine("/tmp/xdg-data", "healdan-helper");
        Assert.Equal(expected, HelperFolders.ForThisUser().Support);
    }
}
