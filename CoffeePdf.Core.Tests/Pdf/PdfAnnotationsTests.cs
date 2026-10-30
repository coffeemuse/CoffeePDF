using CoffeePdf.Core.Pdf;
using CoffeePdf.Core.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Pdf;

public sealed class PdfAnnotationsTests
{
    [Fact]
    public void It_reads_each_annotations_subtype_name_and_author()
    {
        var pdf = AnnotatedPdf.Build(AnnotatedPdf.Note("n-1", "Zoë Q. Reviewer"), AnnotatedPdf.Note(null, null), AnnotatedPdf.Widget("w-1"));

        var annotations = PdfAnnotations.Read(pdf, 0);

        Assert.Equal(
            new[]
            {
                new AnnotationFacts(AnnotationSubtype.Text, "n-1", "Zoë Q. Reviewer"),
                new AnnotationFacts(AnnotationSubtype.Text, null, null),
                new AnnotationFacts(AnnotationSubtype.Widget, "w-1", null),
            },
            annotations);
    }

    [Fact]
    public void A_page_without_annotations_has_none() =>
        Assert.Empty(PdfAnnotations.Read(AnnotatedPdf.Build(), 0));

    [Fact]
    public void Bytes_that_arent_a_pdf_are_refused() =>
        Assert.Throws<InvalidDataException>(() => PdfAnnotations.Read("%PDF-1.7 not really"u8.ToArray(), 0));

    [Fact]
    public void Only_text_markup_shapes_ink_stamps_and_the_like_are_markup()
    {
        Assert.True(AnnotationSubtype.Text.IsMarkup());
        Assert.True(AnnotationSubtype.Highlight.IsMarkup());
        Assert.True(AnnotationSubtype.Ink.IsMarkup());
        Assert.True(AnnotationSubtype.Stamp.IsMarkup());
        Assert.False(AnnotationSubtype.Widget.IsMarkup());
        Assert.False(AnnotationSubtype.Link.IsMarkup());
        Assert.False(AnnotationSubtype.Popup.IsMarkup());
    }
}
