using CoffeePdf.Platform;

namespace CoffeePdf.Tests.Platform;

public sealed class DefaultPdfAppTests
{
    [Fact]
    public void It_reads_the_default_without_changing_it()
    {
        // Reads Launch Services on macOS: either answer is right, and what's checked is that the call works. MakeCoffeePdf
        // is never called here, since it would change the Mac's default.
        var app = new MacDefaultPdfApp();

        Assert.Equal(OperatingSystem.IsMacOS(), app.IsSupported);
        var exception = Record.Exception(() => app.IsCoffeePdf());
        Assert.Null(exception);
    }
}
