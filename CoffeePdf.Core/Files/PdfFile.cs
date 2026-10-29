namespace CoffeePdf.Core.Files;

/// <summary>Tells a PDF from other files by its header, as PDF readers do, rather than by its name.</summary>
public static class PdfFile
{
    /// <summary>How far into a file the <c>%PDF-</c> header may start: readers accept junk before it.</summary>
    public const int HeaderWindow = 1024;

    private static ReadOnlySpan<byte> Header => "%PDF-"u8;

    /// <summary>Whether <paramref name="path"/> is a readable file with a PDF header in its first 1024 bytes.</summary>
    public static bool IsPdf(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[HeaderWindow];
            var read = stream.ReadAtLeast(buffer, HeaderWindow, throwOnEndOfStream: false);
            return buffer.AsSpan(0, read).IndexOf(Header) >= 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
