using System.Globalization;

namespace CoffeePdf.Core.Pdf;

/// <summary>
/// Gives an unsigned PDF a full save in CoffeePDF's own PDFium, so the file holds only what the document uses (spec
/// §6.4; §16, finding 3's fix). The PdfViewer's save writes every object it has made while the document is open, used
/// or not, so without this a markup that was undone, and the drawings the PdfViewer replaced, stay in the file, unseen
/// but readable. Every save runs it on the bytes <see cref="MarkupAuthor"/> returns.
/// </summary>
public static class PdfCompaction
{
    private static ReadOnlySpan<byte> Whitespace => " \t\r\n\f\0"u8;

    /// <summary>
    /// Returns PDFium's full save of <paramref name="pdf"/>: one revision, holding only what the document uses, with its
    /// encryption and permissions kept. An encrypted PDF needs its <paramref name="password"/>, unless it has only an
    /// owner password.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns <paramref name="pdf"/> itself for a signed PDF, told by a <c>/ByteRange</c> in its bytes: a full save
    /// rewrites the signed bytes, which breaks the signature, so an undone markup may stay in the update a save appends
    /// to it, a known limit for v1 (owner, 2026-11-03). The PdfViewer's own save already rewrites a PDF whose signature
    /// field sits under a parent field, which breaks that signature, and such a PDF keeps what its save left too (spec
    /// §16).
    /// </para>
    /// <para>
    /// It also returns <paramref name="pdf"/> for an encrypted PDF with XMP metadata, since PDFium's full save writes
    /// metadata streams unencrypted (observed 2026-11-04), and for one whose full save didn't keep its encryption. Each
    /// of these, and bytes PDFium can't open or save, tells <paramref name="warn"/> why, without the file's contents; an
    /// exception is told by its type. A save never fails for want of a full save, as it never does for want of an
    /// author.
    /// </para>
    /// </remarks>
    public static byte[] Compact(byte[] pdf, string? password, Action<string>? warn = null)
    {
        string problem;
        try
        {
            if (IsSigned(pdf))
                problem = "the PDF holds a signature";
            else if (FullSave(pdf, password, out problem) is { } full)
                return full;
        }
        catch (Exception e) // whatever stops it, the PdfViewer's bytes are written as they were
        {
            problem = $"PDFium failed ({e.GetType().Name})";
        }
        warn?.Invoke($"A save had no full save: {problem}");
        return pdf;
    }

    /// <summary>PDFium's full save of <paramref name="pdf"/>, or null, with <paramref name="problem"/> saying why.</summary>
    private static byte[]? FullSave(byte[] pdf, string? password, out string problem)
    {
        problem = "";
        lock (Pdfium.Gate)
        {
            using var document = PdfiumDocument.Open(pdf, password);
            if (document is null)
            {
                // Read at once, before another PDFium call replaces it: a wrong password reads differently from damage.
                problem = $"PDFium couldn't open it (error {Pdfium.FPDF_GetLastError().Value})";
                return null;
            }
            var revision = Pdfium.FPDF_GetSecurityHandlerRevision(document.Handle);
            // A metadata stream's dictionary is never in an object stream, so its name is readable when it's encrypted.
            if (revision >= 0 && pdf.AsSpan().IndexOf("/Metadata"u8) >= 0)
            {
                problem = "PDFium's would leave the encrypted PDF's XMP metadata unencrypted";
                return null;
            }
            var full = document.SaveFull();
            if (revision >= 0)
            {
                using var check = PdfiumDocument.Open(full, password);
                if (check is null || Pdfium.FPDF_GetSecurityHandlerRevision(check.Handle) != revision)
                {
                    problem = "PDFium's didn't keep the PDF's encryption";
                    return null;
                }
            }
            return full;
        }
    }

    /// <summary>
    /// Whether <paramref name="pdf"/> holds a signature, told by a signature dictionary's <c>/ByteRange</c>: the name,
    /// as written or with <c>#</c> escapes, followed by an array of four integers, outside any stream's data. Every
    /// signature dictionary has one, and it stays readable there: the dictionary can't sit in a compressed object
    /// stream, since the byte range must leave out its own <c>/Contents</c>, and names aren't encrypted. So the name in
    /// a string, in a page's text or in an embedded file doesn't count. PDFium's <c>FPDF_GetSignatureCount</c> isn't
    /// used, since it reads only the form's top-level fields: it counts no signature in a signed field under a parent
    /// field, and one in an empty signature field (probe, 2026-11-03). A signature cleared in a later revision still
    /// counts, since its bytes remain, and keeps the PDF's incremental save.
    /// </summary>
    private static bool IsSigned(ReadOnlySpan<byte> pdf)
    {
        for (var at = pdf.IndexOf((byte)'/'); at >= 0;)
        {
            var name = pdf[(at + 1)..];
            var length = name.IndexOfAny("/[]<>(){}% \t\r\n\f\0"u8);
            if (length < 0)
                return false;
            if (IsByteRange(name[..length]) && OpensWithFourIntegers(name[length..]) && !EndsInStream(pdf[..at]))
                return true;
            var next = name[length..].IndexOf((byte)'/');
            at = next < 0 ? -1 : at + 1 + length + next;
        }
        return false;
    }

    /// <summary>Whether <paramref name="name"/>, after the slash, is <c>ByteRange</c>, decoding any <c>#</c> escape.</summary>
    private static bool IsByteRange(ReadOnlySpan<byte> name)
    {
        var expected = "ByteRange"u8;
        var matched = 0;
        for (var i = 0; i < name.Length; i++, matched++)
        {
            var c = name[i];
            if (c == '#' && i + 2 < name.Length && byte.TryParse(name.Slice(i + 1, 2), NumberStyles.AllowHexSpecifier, null, out var decoded))
            {
                c = decoded;
                i += 2;
            }
            if (matched == expected.Length || c != expected[matched])
                return false;
        }
        return matched == expected.Length;
    }

    /// <summary>Whether <paramref name="text"/> opens with an array of four integers, as a signature's byte range does.</summary>
    private static bool OpensWithFourIntegers(ReadOnlySpan<byte> text)
    {
        text = text.TrimStart(Whitespace);
        if (text is not [(byte)'[', ..])
            return false;
        text = text[1..];
        for (var i = 0; i < 4; i++)
        {
            text = text.TrimStart(Whitespace);
            var digits = text.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
            if (digits <= 0)
                return false;
            text = text[digits..];
        }
        return text.TrimStart(Whitespace) is [(byte)']', ..];
    }

    /// <summary>Whether <paramref name="text"/> ends inside a stream's data: its last <c>stream</c> keyword has no <c>endstream</c> yet.</summary>
    private static bool EndsInStream(ReadOnlySpan<byte> text)
    {
        for (var at = text.LastIndexOf("stream"u8); at >= 0; at = text.LastIndexOf("stream"u8))
        {
            if (text[..at].EndsWith("end"u8))
                return false;
            // The keyword follows its dictionary and ends its line; elsewhere "stream" is just bytes.
            if (text[(at + 6)..] is [(byte)'\n', ..] or [(byte)'\r', (byte)'\n', ..] && text[..at].TrimEnd(Whitespace).EndsWith(">>"u8))
                return true;
            text = text[..at];
        }
        return false;
    }
}
