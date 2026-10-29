using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Tests.Fixtures;

public sealed class SignedPdfTests
{
    [Fact]
    public void The_signature_covers_everything_but_its_own_value()
    {
        var pdf = SignedPdf.Build();
        var range = Regex.Match(Encoding.ASCII.GetString(pdf), @"/ByteRange \[(\d{10}) (\d{10}) (\d{10}) (\d{10})\]");
        var r = Enumerable.Range(1, 4).Select(i => int.Parse(range.Groups[i].Value)).ToArray();
        Assert.Equal(0, r[0]);
        Assert.Equal((byte)'<', pdf[r[1]]);
        Assert.Equal((byte)'>', pdf[r[2] - 1]);
        Assert.Equal(pdf.Length, r[2] + r[3]);
    }

    [Fact]
    public void The_signature_verifies() => SignedPdf.VerifyFirstSignature(SignedPdf.Build());

    [Fact]
    public void A_changed_byte_breaks_the_signature()
    {
        var pdf = SignedPdf.Build();
        var at = Encoding.ASCII.GetString(pdf).IndexOf("CoffeePDF fixture", StringComparison.Ordinal);
        pdf[at] = (byte)'K';
        Assert.ThrowsAny<CryptographicException>(() => SignedPdf.VerifyFirstSignature(pdf));
    }
}
