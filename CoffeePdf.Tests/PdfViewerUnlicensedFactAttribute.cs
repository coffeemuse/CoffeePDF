namespace CoffeePdf.Tests;

/// <summary>
/// The inverse of <see cref="PdfViewerFactAttribute"/>: a probe of what the PdfViewer does without the Avalonia
/// licence key, which runs only in a build without it (AvaloniaLicense.props doesn't define AVALONIA_LICENSED then).
/// With the key it is skipped, since the keyed behaviour is what the other PdfViewer tests check (spec §14 item 4).
/// </summary>
public sealed class PdfViewerUnlicensedFactAttribute : FactAttribute
{
    public PdfViewerUnlicensedFactAttribute()
    {
#if AVALONIA_LICENSED
        Skip = "Observes the behaviour without the Avalonia licence key: unset AVALONIA_LICENSE_KEY and build again";
#endif
    }
}
