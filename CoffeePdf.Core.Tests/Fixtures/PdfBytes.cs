using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Core.Tests.Fixtures;

/// <summary>
/// Reads a PDF's bytes as anyone with a PDF tool could, without a PDF library, for spec §16, finding 3's fix: a markup
/// that's gone from the page mustn't be in the file.
/// </summary>
internal static class PdfBytes
{
    /// <summary>
    /// Whether <paramref name="text"/> is anywhere in <paramref name="pdf"/>, in the file's bytes or in a stream once
    /// inflated, as ASCII or UTF-16BE, or as either one's hex in upper or lower case. It doesn't decrypt.
    /// </summary>
    public static bool Holds(byte[] pdf, string text)
    {
        var forms = Forms(text);
        return Places(pdf).Any(place => forms.Any(form => place.AsSpan().IndexOf(form) >= 0));
    }

    /// <summary>How many saves are stacked in <paramref name="pdf"/>: each ends with <c>%%EOF</c>.</summary>
    public static int Saves(byte[] pdf) => Regex.Count(Encoding.Latin1.GetString(pdf), "%%EOF");

    private static List<byte[]> Forms(string text)
    {
        var ascii = Encoding.ASCII.GetBytes(text);
        var utf16 = Encoding.BigEndianUnicode.GetBytes(text);
        var hex = new[] { ascii, utf16 }
            .SelectMany(bytes => new[] { Convert.ToHexString(bytes), Convert.ToHexString(bytes).ToLowerInvariant() })
            .Select(Encoding.ASCII.GetBytes);
        return [ascii, utf16, .. hex];
    }

    /// <summary>The file's bytes, then each stream's, inflated, for those that inflate.</summary>
    private static IEnumerable<byte[]> Places(byte[] pdf)
    {
        yield return pdf;
        var text = Encoding.Latin1.GetString(pdf);
        foreach (Match match in Regex.Matches(text, @"stream\r?\n"))
        {
            var start = match.Index + match.Length;
            var end = text.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0)
                continue;
            var inflated = Inflate(pdf, start, end - start);
            if (inflated is not null)
                yield return inflated;
        }
    }

    /// <summary>The zlib data at <paramref name="start"/> inflated, or null if it isn't zlib data.</summary>
    private static byte[]? Inflate(byte[] pdf, int start, int length)
    {
        try
        {
            using var input = new MemoryStream(pdf, start, length);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
