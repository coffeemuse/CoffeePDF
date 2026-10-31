using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>Core Foundation's strings and arrays, for Launch Services' calls (DefaultPdfApp, HelperInstall).</summary>
internal static unsafe partial class CoreFoundation
{
    /// <summary>Launch Services' framework.</summary>
    public const string CoreServices = "/System/Library/Frameworks/CoreServices.framework/CoreServices";

    private const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>A CFString holding <paramref name="text"/>, which the caller releases.</summary>
    public static nint CreateString(string text)
    {
        fixed (char* characters = text)
            return CFStringCreateWithCharacters(0, characters, text.Length);
    }

    /// <summary>A CFString's text.</summary>
    public static string ManagedString(nint cfString)
    {
        var length = CFStringGetLength(cfString);
        var buffer = new char[length];
        fixed (char* b = buffer)
            CFStringGetCharacters(cfString, new CFRange { Location = 0, Length = length }, b);
        return new string(buffer);
    }

    /// <summary>How many items a CFArray holds.</summary>
    public static long ArrayCount(nint array) => CFArrayGetCount(array);

    public static void Release(nint cf) => CFRelease(cf);

    [StructLayout(LayoutKind.Sequential)]
    private struct CFRange
    {
        public nint Location;
        public nint Length;
    }

    [LibraryImport(Library)] private static partial nint CFStringCreateWithCharacters(nint allocator, char* characters, nint length);
    [LibraryImport(Library)] private static partial nint CFStringGetLength(nint text);
    [LibraryImport(Library)] private static partial void CFStringGetCharacters(nint text, CFRange range, char* buffer);
    [LibraryImport(Library)] private static partial nint CFArrayGetCount(nint array);
    [LibraryImport(Library)] private static partial void CFRelease(nint cf);
}
