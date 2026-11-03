using System.Text;
using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fixtures;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>
/// Spec §6.4 and §16, finding 3's fix: a save keeps only what an unsigned PDF uses, keeps its encryption, and leaves a
/// signed PDF as it was.
/// </summary>
public sealed class PdfCompactionTests
{
    private const string Author = "Test Reviewer";

    /// <summary>An object nothing refers to, as the PdfViewer leaves for a markup that was undone. Its name is the marker.</summary>
    private const string Leftover = "<< /Type /CoffeePdfLeftover >>";

    private const string Marker = "CoffeePdfLeftover";

    /// <summary>A signature dictionary. Its <c>/ByteRange</c> is what tells a signed PDF; the bytes aren't a real signature's.</summary>
    private const string Signature = "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> >>";

    [Fact]
    public void An_object_nothing_uses_is_gone_and_the_page_is_the_same()
    {
        var pdf = WithLeftover();

        var compacted = PdfCompaction.Compact(pdf, null);

        Assert.True(PdfBytes.Holds(pdf, Marker));
        Assert.False(PdfBytes.Holds(compacted, Marker));
        Assert.True(PdfBytes.Holds(compacted, "CoffeePDF fixture")); // the page's text
        Assert.Equal(PdfAnnotations.Read(pdf, 0), PdfAnnotations.Read(compacted, 0));
    }

    [Fact]
    public void Saves_stacked_in_a_file_come_back_as_one_keeping_what_MarkupAuthor_reads()
    {
        // MarkupAuthor tells new markups by /NM, /T and the PdfViewer's /SNC, so the full save must keep all three.
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null), AnnotatedPdf.Note(null, null, left: 200, byPdfViewer: true));
        var stamped = MarkupAuthor.Stamp(pdf, previousFile: null, [0], Author);

        var compacted = PdfCompaction.Compact(stamped, null);

        Assert.Equal(2, PdfBytes.Saves(stamped));
        Assert.Equal(1, PdfBytes.Saves(compacted));
        Assert.Equal(PdfAnnotations.Read(stamped, 0), PdfAnnotations.Read(compacted, 0));
        Assert.Equal(2, PdfAnnotations.Read(compacted, 0).Count); // so the next line can't pass on an empty list
        Assert.All(PdfAnnotations.Read(compacted, 0), a => Assert.Equal(Author, a.Author));
        Assert.True(PdfBytes.Holds(compacted, "/SNC"));
    }

    [Fact]
    public void A_signed_pdf_comes_back_as_it_was()
    {
        // A full save would break the signature, so a signed PDF keeps its incremental save (owner, 2026-11-03).
        var pdf = Signed();

        Assert.Same(pdf, PdfCompaction.Compact(pdf, null));
    }

    [Fact]
    public void A_signed_field_under_a_parent_field_comes_back_as_it_was()
    {
        // PDFium's signature count reads only the form's top-level fields, so it misses a signature nested under a parent.
        var pdf = SignedUnderParent();

        Assert.Same(pdf, PdfCompaction.Compact(pdf, null));
    }

    [Fact]
    public void A_pdf_with_an_empty_signature_field_gets_its_full_save()
    {
        // PDFium's signature count is 1 for a signature field with no value, which a form awaiting its signature has.
        var pdf = WithEmptySignatureField();

        var compacted = PdfCompaction.Compact(pdf, null);

        Assert.True(PdfBytes.Holds(pdf, Marker));
        Assert.False(PdfBytes.Holds(compacted, Marker));
        // The field survives the full save: its widget is still on the page.
        Assert.Equal(AnnotationSubtype.Widget, Assert.Single(PdfAnnotations.Read(compacted, 0)).Subtype);
    }

    [Fact]
    public void An_encrypted_pdf_keeps_its_encryption_and_loses_what_it_doesnt_use()
    {
        var pdf = EncryptedPdf.Build(unusedObject: Leftover);

        var compacted = PdfCompaction.Compact(pdf, EncryptedPdf.UserPassword);

        Assert.True(PdfBytes.Holds(pdf, Marker));
        Assert.False(PdfBytes.Holds(compacted, Marker));
        Assert.Throws<InvalidDataException>(() => PdfAnnotations.Read(compacted, 0));
        Assert.Empty(PdfAnnotations.Read(compacted, 0, EncryptedPdf.UserPassword));
        var text = Encoding.Latin1.GetString(compacted);
        Assert.Matches(@"/R\s*3\b", text);
        Assert.Matches(@"/P\s*-4\b", text);
    }

    [Fact]
    public void A_pdf_with_only_an_owner_password_keeps_its_encryption()
    {
        var pdf = EncryptedPdf.Build(userPassword: "", unusedObject: Leftover);

        var compacted = PdfCompaction.Compact(pdf, null);

        Assert.False(PdfBytes.Holds(compacted, Marker));
        Assert.Empty(PdfAnnotations.Read(compacted, 0)); // it still opens without a password
        var text = Encoding.Latin1.GetString(compacted);
        Assert.Matches(@"/Encrypt\s*\d+\s+\d+\s+R", text);
        Assert.Matches(@"/P\s*-4\b", text);
    }

    [Fact]
    public void An_encrypted_pdf_without_its_password_comes_back_as_it_was_and_says_why()
    {
        var pdf = EncryptedPdf.Build();
        var warnings = new List<string>();

        Assert.Same(pdf, PdfCompaction.Compact(pdf, null, warnings.Add));
        Assert.Equal("A save had no full save: PDFium couldn't open it (error 4)", Assert.Single(warnings));
    }

    [Fact]
    public void Bytes_that_arent_a_pdf_come_back_as_they_were()
    {
        var bytes = "not a PDF"u8.ToArray();
        var warnings = new List<string>();

        Assert.Same(bytes, PdfCompaction.Compact(bytes, null, warnings.Add));
        Assert.Equal("A save had no full save: PDFium couldn't open it (error 3)", Assert.Single(warnings));
    }

    /// <summary>The minimal PDF with another app's note on its page, and the leftover as object 7.</summary>
    private static byte[] WithLeftover()
    {
        var objects = MinimalPdf.Objects(withForeignNote: true, extraAnnotations: [], acroForm: null);
        objects.Add(Leftover);
        return MinimalPdf.Write(objects);
    }

    /// <summary>
    /// The minimal PDF with a signature field (7) whose value is a signature dictionary (8), which holds a
    /// <c>/ByteRange</c>, and the leftover (9). The signature's bytes aren't real: the app's tests check a real one.
    /// </summary>
    private static byte[] Signed()
    {
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: ["7 0 R"], acroForm: "<< /Fields [7 0 R] /SigFlags 3 >>");
        objects.Add("<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P 3 0 R /V 8 0 R >>");
        objects.Add(Signature);
        objects.Add(Leftover);
        return MinimalPdf.Write(objects);
    }

    /// <summary>
    /// The minimal PDF whose only top-level field (7) has no type, and has the signed widget (8, with its signature
    /// dictionary, 9) as a kid. The leftover is 10.
    /// </summary>
    private static byte[] SignedUnderParent()
    {
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: ["8 0 R"], acroForm: "<< /Fields [7 0 R] /SigFlags 3 >>");
        objects.Add("<< /T (Signatures) /Kids [8 0 R] >>");
        objects.Add("<< /Type /Annot /Subtype /Widget /FT /Sig /T (Client) /Parent 7 0 R /Rect [0 0 0 0] /F 132 /P 3 0 R /V 9 0 R >>");
        objects.Add(Signature);
        objects.Add(Leftover);
        return MinimalPdf.Write(objects);
    }

    /// <summary>The minimal PDF with a signature field (7) that has no value, and the leftover (8).</summary>
    private static byte[] WithEmptySignatureField()
    {
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: ["7 0 R"], acroForm: "<< /Fields [7 0 R] >>");
        objects.Add("<< /Type /Annot /Subtype /Widget /FT /Sig /T (Client) /Rect [0 0 0 0] /F 132 /P 3 0 R >>");
        objects.Add(Leftover);
        return MinimalPdf.Write(objects);
    }
}
