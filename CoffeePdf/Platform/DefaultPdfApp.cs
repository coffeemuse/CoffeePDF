using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>Settings' Default PDF app (spec §5): whether CoffeePDF opens PDFs, and a way to make it.</summary>
internal interface IDefaultPdfApp
{
    /// <summary>Whether this platform has a default to read and set.</summary>
    bool IsSupported { get; }

    bool IsCoffeePdf();

    /// <summary>Makes CoffeePDF the default for PDFs. Returns whether Launch Services took it.</summary>
    bool MakeCoffeePdf();
}

/// <summary>The default PDF app through macOS's Launch Services, for every role, by CoffeePDF's bundle id.</summary>
internal sealed partial class MacDefaultPdfApp : IDefaultPdfApp
{
    public const string BundleId = "com.healdan.coffeepdf";

    private const string PdfType = "com.adobe.pdf";
    private const uint AllRoles = 0xFFFFFFFF;

    public bool IsSupported => OperatingSystem.IsMacOS();

    public bool IsCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CoreFoundation.CreateString(PdfType);
        try
        {
            var handler = LSCopyDefaultRoleHandlerForContentType(type, AllRoles);
            if (handler == 0)
                return false;
            try
            {
                return string.Equals(CoreFoundation.ManagedString(handler), BundleId, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                CoreFoundation.Release(handler);
            }
        }
        finally
        {
            CoreFoundation.Release(type);
        }
    }

    public bool MakeCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CoreFoundation.CreateString(PdfType);
        var bundle = CoreFoundation.CreateString(BundleId);
        try
        {
            return LSSetDefaultRoleHandlerForContentType(type, AllRoles, bundle) == 0;
        }
        finally
        {
            CoreFoundation.Release(bundle);
            CoreFoundation.Release(type);
        }
    }

    // Deprecated since macOS 12, and still the plain C call for it.
    [LibraryImport(CoreFoundation.CoreServices)] private static partial nint LSCopyDefaultRoleHandlerForContentType(nint contentType, uint role);
    [LibraryImport(CoreFoundation.CoreServices)] private static partial int LSSetDefaultRoleHandlerForContentType(nint contentType, uint role, nint handlerBundleId);
}
