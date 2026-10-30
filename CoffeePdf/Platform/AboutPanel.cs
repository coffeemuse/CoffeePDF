using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>
/// About CoffeePDF: macOS's own About panel, which shows the app's name, icon, version and copyright from its
/// Info.plist. Avalonia's default app menu has an About Avalonia item only while the app sets no menu of its own, so
/// CoffeePDF's menu, which adds Settings…, brings its own.
/// </summary>
internal static partial class AboutPanel
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    public static bool IsSupported => OperatingSystem.IsMacOS();

    /// <summary>Shows the panel, or brings it forward if it's open.</summary>
    public static void Show()
    {
        if (!IsSupported)
            return;
        var application = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
        objc_msgSend(application, sel_registerName("orderFrontStandardAboutPanel:"), 0);
    }

    [LibraryImport(ObjectiveC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint objc_getClass(string name);

    [LibraryImport(ObjectiveC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint sel_registerName(string name);

    [LibraryImport(ObjectiveC)]
    private static partial nint objc_msgSend(nint receiver, nint selector);

    [LibraryImport(ObjectiveC)]
    private static partial nint objc_msgSend(nint receiver, nint selector, nint argument);
}
