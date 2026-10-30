using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Pdf;

/// <summary>Spec §7 and §16 Decision 1: the author goes into the markups made in CoffeePDF, and nowhere else.</summary>
public sealed class MarkupAuthorTests : IDisposable
{
    private const string Author = "Zoë Q. Reviewer";

    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Previous(byte[] pdf)
    {
        var path = Path.Combine(_dir, "previous.pdf");
        File.WriteAllBytes(path, pdf);
        return path;
    }

    [Fact]
    public void A_new_note_gets_the_author_in_an_incremental_update()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        var stamped = MarkupAuthor.Stamp(pdf, previousFile: null, [0], Author);

        Assert.Equal(Author, Assert.Single(PdfAnnotations.Read(stamped, 0)).Author);
        Assert.True(stamped.Length > pdf.Length);
        Assert.Equal(pdf, stamped[..pdf.Length]);
    }

    [Fact]
    public void A_note_with_an_author_keeps_it()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", "Someone Else"));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }

    [Fact]
    public void An_authorless_note_already_in_the_file_stays_authorless()
    {
        var previous = Previous(AnnotatedPdf.Build(AnnotatedPdf.Note("x-1", null)));
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("x-1", null), AnnotatedPdf.Note("av-2", null));

        var annotations = PdfAnnotations.Read(MarkupAuthor.Stamp(pdf, previous, [0], Author), 0);

        Assert.Null(annotations.Single(a => a.Name == "x-1").Author);
        Assert.Equal(Author, annotations.Single(a => a.Name == "av-2").Author);
    }

    [Fact]
    public void A_note_stamped_at_the_last_save_is_stamped_again()
    {
        // The PdfViewer's own copy of the document never has the author, so each save writes it again.
        var previous = Previous(AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", Author)));
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Equal(Author, Assert.Single(PdfAnnotations.Read(MarkupAuthor.Stamp(pdf, previous, [0], Author), 0)).Author);
    }

    [Fact]
    public void A_note_without_a_name_isnt_ours_and_stays_authorless()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note(null, null));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }

    [Fact]
    public void Form_fields_are_left_alone()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Widget("av-1"));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }

    [Fact]
    public void Only_the_pages_given_are_touched()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [], Author));
        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [1], Author));
    }

    [Fact]
    public void A_blank_author_writes_nothing()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], "  "));
    }

    [Fact]
    public void A_pdf_pdfium_cant_open_is_returned_as_it_is()
    {
        var pdf = "%PDF-1.7 not really"u8.ToArray();

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, null, [0], Author));
    }

    [Fact]
    public void A_previous_file_PDFium_cant_open_stamps_nothing()
    {
        // Nothing can be told apart in a file that can't be read, so the save writes no author rather than guess.
        var previous = Previous("%PDF-1.7 not really"u8.ToArray());
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Same(pdf, MarkupAuthor.Stamp(pdf, previous, [0], Author));
    }

    [Fact]
    public void A_previous_file_that_isnt_there_yet_counts_as_empty()
    {
        var previous = Path.Combine(_dir, "never-written.pdf");
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("av-1", null));

        Assert.Equal(Author, Assert.Single(PdfAnnotations.Read(MarkupAuthor.Stamp(pdf, previous, [0], Author), 0)).Author);
    }
}
