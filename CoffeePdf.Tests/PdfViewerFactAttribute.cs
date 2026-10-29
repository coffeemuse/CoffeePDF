namespace CoffeePdf.Tests;

/// <summary>
/// A test that needs the PdfViewer, which runs only in a build with the Avalonia licence key (AvaloniaLicense.props
/// defines AVALONIA_LICENSED then). Without it the test is skipped, not failed, so a build without the key still tests
/// everything else (spec §8).
/// </summary>
public sealed class PdfViewerFactAttribute : FactAttribute
{
    public PdfViewerFactAttribute()
    {
#if !AVALONIA_LICENSED
        Skip = "Needs the Avalonia licence key: set AVALONIA_LICENSE_KEY and build again";
#endif
    }
}
