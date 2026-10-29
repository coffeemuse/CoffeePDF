using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Tests.Fixtures;

/// <summary>
/// The minimal PDF, signed as Acrobat signs: an approval signature (not a certification, so later annotations are
/// allowed), detached PKCS#7 over the whole file except the signature's own value, with a throwaway self-signed
/// certificate. For spec §14 item 1: a save must keep it.
/// </summary>
internal static class SignedPdf
{
    /// <summary>Room for the CMS blob in hex, about 3 KB of it here; the rest stays zeros, as signers leave it.</summary>
    private const int ContentsHexLength = 16384;

    private const string ByteRangePlaceholder = "[0000000000 0000000000 0000000000 0000000000]";

    public static byte[] Build()
    {
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: ["7 0 R"],
            acroForm: "<< /Fields [7 0 R] /SigFlags 3 >>");
        objects.Add("<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P 3 0 R /V 8 0 R >>");
        objects.Add($"<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange {ByteRangePlaceholder} /Contents <{new string('0', ContentsHexLength)}> /M (D:20261029000000Z) >>");
        var pdf = MinimalPdf.Write(objects);
        var text = Encoding.ASCII.GetString(pdf);

        var hexStart = text.IndexOf("/Contents <", StringComparison.Ordinal) + "/Contents ".Length; // at '<'
        var hexEnd = hexStart + 1 + ContentsHexLength + 1;                                          // just after '>'
        var byteRange = $"[{0:D10} {hexStart:D10} {hexEnd:D10} {pdf.Length - hexEnd:D10}]";
        Encoding.ASCII.GetBytes(byteRange).CopyTo(pdf, text.IndexOf(ByteRangePlaceholder, StringComparison.Ordinal));

        var signature = Sign([.. pdf[..hexStart], .. pdf[hexEnd..]]);
        Encoding.ASCII.GetBytes(Convert.ToHexString(signature)).CopyTo(pdf, hexStart + 1);
        return pdf;
    }

    /// <summary>
    /// Checks the first signature in <paramref name="pdf"/> against the bytes its /ByteRange names: an incremental
    /// update leaves that signature first, and its bytes alone. Throws <see cref="CryptographicException"/> if it fails.
    /// </summary>
    public static void VerifyFirstSignature(byte[] pdf)
    {
        var text = Encoding.ASCII.GetString(pdf);
        var range = Regex.Match(text, @"/ByteRange \[(\d+) (\d+) (\d+) (\d+)\]");
        var r = Enumerable.Range(1, 4).Select(i => int.Parse(range.Groups[i].Value)).ToArray();
        byte[] signed = [.. pdf[r[0]..(r[0] + r[1])], .. pdf[r[2]..(r[2] + r[3])]];
        var blob = Convert.FromHexString(text[(r[1] + 1)..(r[2] - 1)]);
        AsnDecoder.ReadEncodedValue(blob, AsnEncodingRules.BER, out _, out _, out var length);
        var cms = new SignedCms(new ContentInfo(signed), detached: true);
        cms.Decode(blob.AsSpan(0, length));
        cms.CheckSignature(verifySignatureOnly: true);
    }

    private static byte[] Sign(byte[] data)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=CoffeePDF test signer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var cms = new SignedCms(new ContentInfo(data), detached: true);
        cms.ComputeSignature(new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate));
        return cms.Encode();
    }
}
