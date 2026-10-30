using CoffeePdf.Core.Documents;

namespace CoffeePdf.Core.Tests.Documents;

public sealed class DocumentTitleTests
{
    [Fact]
    public void An_empty_window_is_CoffeePDF() => Assert.Equal("CoffeePDF", DocumentTitle.For(null, edited: false));

    [Fact]
    public void A_file_shows_its_name() => Assert.Equal("Report.pdf", DocumentTitle.For("/docs/Report.pdf", edited: false));

    [Fact]
    public void Unsaved_changes_say_Edited_as_Preview_does() =>
        Assert.Equal("Report.pdf — Edited", DocumentTitle.For("/docs/Report.pdf", edited: true));
}
