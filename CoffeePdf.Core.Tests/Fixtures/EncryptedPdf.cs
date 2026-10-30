// Compiled by both test projects (CoffeePdf.Tests links it), so its namespace stays the app tests' own.
using System.Security.Cryptography;
using System.Text;

namespace CoffeePdf.Tests.Fixtures;

/// <summary>
/// A one-page PDF that needs a password to open, encrypted here since no PDF is committed and .NET has no PDF
/// encryption of its own. It uses the standard security handler at revision 3 (RC4, 128-bit), following ISO 32000-1
/// §7.6.3: algorithm 2 for the file key, 3 for /O, 5 for /U, and 1 for each object's key. Every permission is allowed,
/// so a viewer opened with the password can mark it up.
/// </summary>
internal static class EncryptedPdf
{
    public const string UserPassword = "coffee-open";
    public const string OwnerPassword = "coffee-owner";

    // Every permission bit set; bits 1 and 2 must be 0 (ISO 32000-1, table 22).
    private const int Permissions = -4;

    private static readonly byte[] Padding = Convert.FromHexString("28BF4E5E4E758A4164004E56FFFA01082E2E00B6D0683E802F0CA9FE6453697A");
    private static readonly byte[] FileId = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    public static byte[] Build()
    {
        var owner = OwnerEntry();
        var key = FileKey(owner);
        var user = UserEntry(key);
        var content = Rc4(ObjectKey(key, 4), "BT /F1 24 Tf 72 700 Td (CoffeePDF fixture) Tj ET"u8.ToArray());
        byte[][] objects =
        [
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"),
            [.. Ascii($"<< /Length {content.Length} >>\nstream\n"), .. content, .. Ascii("\nendstream")],
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            // The encryption dictionary's own strings aren't encrypted.
            Ascii($"<< /Filter /Standard /V 2 /R 3 /Length 128 /P {Permissions} /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> >>"),
        ];

        using var pdf = new MemoryStream();
        pdf.Write(Ascii("%PDF-1.7\n"));
        var offsets = new List<long>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Position);
            pdf.Write(Ascii($"{i + 1} 0 obj\n"));
            pdf.Write(objects[i]);
            pdf.Write(Ascii("\nendobj\n"));
        }
        var xref = pdf.Position;
        pdf.Write(Ascii($"xref\n0 {objects.Length + 1}\n0000000000 65535 f\r\n"));
        foreach (var offset in offsets)
            pdf.Write(Ascii($"{offset:D10} 00000 n\r\n"));
        var id = Convert.ToHexString(FileId);
        pdf.Write(Ascii($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Encrypt 6 0 R /ID [<{id}> <{id}>] >>\nstartxref\n{xref}\n%%EOF\n"));
        return pdf.ToArray();
    }

    /// <summary>Algorithm 3: /O, the user password's padding encrypted with a key from the owner password.</summary>
    private static byte[] OwnerEntry()
    {
        var hash = MD5.HashData(Pad(OwnerPassword));
        for (var i = 0; i < 50; i++)
            hash = MD5.HashData(hash);
        return Rc4Rounds(hash, Pad(UserPassword));
    }

    /// <summary>Algorithm 2: the file's key, from the user password, /O, the permissions and the file's ID.</summary>
    private static byte[] FileKey(byte[] owner)
    {
        var permissions = BitConverter.GetBytes(Permissions); // little-endian on every platform .NET runs these tests on
        var hash = MD5.HashData([.. Pad(UserPassword), .. owner, .. permissions, .. FileId]);
        for (var i = 0; i < 50; i++)
            hash = MD5.HashData(hash);
        return hash;
    }

    /// <summary>Algorithm 5: /U, the padding and the file's ID hashed and encrypted with the file's key, padded to 32 bytes.</summary>
    private static byte[] UserEntry(byte[] key) => [.. Rc4Rounds(key, MD5.HashData([.. Padding, .. FileId])), .. new byte[16]];

    /// <summary>Algorithm 1: an object's key, from the file's key and the object's number and generation (0).</summary>
    private static byte[] ObjectKey(byte[] key, int number) =>
        MD5.HashData([.. key, (byte)number, (byte)(number >> 8), (byte)(number >> 16), 0, 0]);

    /// <summary>RC4 with <paramref name="key"/>, then 19 more rounds, each with the key's bytes XORed with the round's number.</summary>
    private static byte[] Rc4Rounds(byte[] key, byte[] data)
    {
        var result = Rc4(key, data);
        for (var round = 1; round <= 19; round++)
            result = Rc4(key.Select(b => (byte)(b ^ round)).ToArray(), result);
        return result;
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var s = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        for (int i = 0, j = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 255;
            (s[i], s[j]) = (s[j], s[i]);
        }
        var output = new byte[data.Length];
        for (int n = 0, i = 0, j = 0; n < data.Length; n++)
        {
            i = (i + 1) & 255;
            j = (j + s[i]) & 255;
            (s[i], s[j]) = (s[j], s[i]);
            output[n] = (byte)(data[n] ^ s[(s[i] + s[j]) & 255]);
        }
        return output;
    }

    private static byte[] Pad(string password) => [.. Encoding.Latin1.GetBytes(password).Concat(Padding).Take(32)];

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
}
