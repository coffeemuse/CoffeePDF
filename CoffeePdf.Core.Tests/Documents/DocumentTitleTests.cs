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

    [Fact]
    public void A_copy_shows_the_Helpers_title_and_says_Edited_and_Saving_to_Healdan()
    {
        Assert.Equal("Settlement Agreement #1234 v2.1", DocumentTitle.ForCopy("Settlement Agreement #1234 v2.1", edited: false, saving: false));
        Assert.Equal("Settlement Agreement #1234 v2.1 — Edited — Saving to Healdan…",
            DocumentTitle.ForCopy("Settlement Agreement #1234 v2.1", edited: true, saving: true));
    }
}
