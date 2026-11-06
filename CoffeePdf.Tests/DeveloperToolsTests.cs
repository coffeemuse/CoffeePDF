namespace CoffeePdf.Tests;

/// <summary>
/// The devtools spec's §4: Avalonia's Developer Tools are in the app only when the build set <c>CoffeePdfDevTools</c>,
/// which only a test build does.
/// </summary>
public sealed class DeveloperToolsTests
{
    private const string Assembly = "AvaloniaUI.DiagnosticsSupport.Avalonia";

    [Fact]
    public void The_app_references_the_developer_tools_exactly_when_it_was_built_with_them() =>
        Assert.Equal(Program.CarriesDeveloperTools,
            typeof(App).Assembly.GetReferencedAssemblies().Any(reference => reference.Name == Assembly));

    [Fact]
    public void A_build_that_does_not_ask_for_them_carries_no_developer_tools() =>
        Assert.False(Program.CarriesDeveloperTools,
            "This build set CoffeePdfDevTools, which only a test build may (the devtools spec's §4)");
}
