using System.Text.Json;

namespace CoffeePdf.Tests;

/// <summary>
/// The devtools spec's §4: Avalonia's Developer Tools are in the app only when the build set <c>CoffeePdfDevTools</c>,
/// which only a test build does.
/// </summary>
public sealed class DeveloperToolsTests
{
    private const string Package = "AvaloniaUI.DiagnosticsSupport";

    /// <summary>
    /// What the build carries, by the tests' own deps.json, which lists every package that reaches them through the
    /// app. The app's assembly references can't show it: the compiler records only the assemblies the code uses, so
    /// a PackageReference that lost its condition, with the call still under <c>#if</c>, leaves them as they were.
    /// </summary>
    [Fact]
    public void The_build_carries_the_developer_tools_package_exactly_when_it_was_built_with_them()
    {
        var deps = Path.Combine(AppContext.BaseDirectory, typeof(DeveloperToolsTests).Assembly.GetName().Name + ".deps.json");
        using var document = JsonDocument.Parse(File.ReadAllText(deps));
        var carried = document.RootElement.GetProperty("libraries").EnumerateObject()
            .Any(library => library.Name.StartsWith(Package + "/", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Program.CarriesDeveloperTools, carried);
    }

    [Fact]
    public void A_build_that_does_not_ask_for_them_carries_no_developer_tools() =>
        Assert.False(Program.CarriesDeveloperTools,
            "This build set CoffeePdfDevTools, which only a test build may (the devtools spec's §4)");
}
