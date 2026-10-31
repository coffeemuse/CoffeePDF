using CoffeePdf.Platform;

namespace CoffeePdf.Tests.Platform;

/// <summary>Spec §5: the first launch asks only when the Helper is installed, as Launch Services tells.</summary>
public sealed class HelperInstallTests
{
    [Fact]
    public void An_app_Launch_Services_knows_is_installed() => Assert.True(new MacHelperInstall("com.apple.Preview").IsInstalled());

    [Fact]
    public void A_bundle_id_no_app_has_isnt() => Assert.False(new MacHelperInstall("com.healdan.no-such-app").IsInstalled());
}
