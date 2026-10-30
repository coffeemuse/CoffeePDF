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
internal sealed unsafe partial class MacDefaultPdfApp : IDefaultPdfApp
{
    public const string BundleId = "com.healdan.coffeepdf";

    private const string PdfType = "com.adobe.pdf";
    private const uint AllRoles = 0xFFFFFFFF;
    private const string CoreServices = "/System/Library/Frameworks/CoreServices.framework/CoreServices";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    public bool IsSupported => OperatingSystem.IsMacOS();

    public bool IsCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CfString(PdfType);
        try
        {
            var handler = LSCopyDefaultRoleHandlerForContentType(type, AllRoles);
            if (handler == 0)
                return false;
            try
            {
                return string.Equals(ManagedString(handler), BundleId, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                CFRelease(handler);
            }
        }
        finally
        {
            CFRelease(type);
        }
    }

    public bool MakeCoffeePdf()
    {
        if (!IsSupported)
            return false;
        var type = CfString(PdfType);
        var bundle = CfString(BundleId);
        try
        {
            return LSSetDefaultRoleHandlerForContentType(type, AllRoles, bundle) == 0;
        }
        finally
        {
            CFRelease(bundle);
            CFRelease(type);
        }
    }

    private static nint CfString(string text)
    {
        fixed (char* characters = text)
            return CFStringCreateWithCharacters(0, characters, text.Length);
    }

    private static string ManagedString(nint cfString)
    {
        var length = CFStringGetLength(cfString);
        var buffer = new char[length];
        fixed (char* b = buffer)
            CFStringGetCharacters(cfString, new CFRange { Location = 0, Length = length }, b);
        return new string(buffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CFRange
    {
        public nint Location;
        public nint Length;
    }

    // Deprecated since macOS 12, and still the plain C call for it.
    [LibraryImport(CoreServices)] private static partial nint LSCopyDefaultRoleHandlerForContentType(nint contentType, uint role);
    [LibraryImport(CoreServices)] private static partial int LSSetDefaultRoleHandlerForContentType(nint contentType, uint role, nint handlerBundleId);
    [LibraryImport(CoreFoundation)] private static partial nint CFStringCreateWithCharacters(nint allocator, char* characters, nint length);
    [LibraryImport(CoreFoundation)] private static partial nint CFStringGetLength(nint text);
    [LibraryImport(CoreFoundation)] private static partial void CFStringGetCharacters(nint text, CFRange range, char* buffer);
    [LibraryImport(CoreFoundation)] private static partial void CFRelease(nint cf);
}
