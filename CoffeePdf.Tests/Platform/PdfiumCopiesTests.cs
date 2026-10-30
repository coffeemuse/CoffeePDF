using System.Runtime.InteropServices;

namespace CoffeePdf.Tests.Platform;

/// <summary>
/// CoffeePDF's PDFium is a copy of its own, beside the PdfViewer's (CoffeePdf.Core's csproj). OwnPdfiumTests shows the
/// crash that sharing one copy caused, but only when no other test keeps a document open, so this checks the copies
/// directly. It needs no licence: it only loads the two libraries.
/// </summary>
public sealed class PdfiumCopiesTests
{
    private static string Library(string stem) => OperatingSystem.IsMacOS() ? $"{stem}.dylib" : $"{stem}.so";

    /// <summary>The PdfViewer's libpdfium: beside the assembly, or in the runtimes folder of this machine's platform.</summary>
    private static string ViewersPdfium()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, Library("libpdfium"));
        if (File.Exists(beside))
            return beside;
        var platform = (OperatingSystem.IsMacOS() ? "osx" : "linux") + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "-arm64" : "-x64");
        return Path.Combine(AppContext.BaseDirectory, "runtimes", platform, "native", Library("libpdfium"));
    }

    [Fact]
    public void The_core_and_the_PdfViewer_load_two_images_of_PDFium()
    {
        var own = Path.Combine(AppContext.BaseDirectory, Library("libcoffeepdfium"));
        var viewers = ViewersPdfium();
        Assert.True(File.Exists(own), "The core's copy is missing from the output folder");
        Assert.True(File.Exists(viewers), "The PdfViewer's libpdfium is missing from the output folder");

        var ownHandle = NativeLibrary.Load(own);
        var viewersHandle = NativeLibrary.Load(viewers);
        try
        {
            Assert.NotEqual(ownHandle, viewersHandle);
            // The same function in two images has two addresses, and so two sets of global state behind it.
            Assert.NotEqual(NativeLibrary.GetExport(ownHandle, "FPDF_InitLibrary"), NativeLibrary.GetExport(viewersHandle, "FPDF_InitLibrary"));
            Assert.NotEqual(NativeLibrary.GetExport(ownHandle, "FPDF_DestroyLibrary"), NativeLibrary.GetExport(viewersHandle, "FPDF_DestroyLibrary"));
        }
        finally
        {
            NativeLibrary.Free(ownHandle);
            NativeLibrary.Free(viewersHandle);
        }
    }
}
