using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>The Desktop Clients page's "Local Copies": <c>&lt;name&gt; #&lt;doc #&gt; v&lt;version&gt;.pdf</c>.</summary>
public sealed class CopyNameTests
{
    [Fact]
    public void A_copys_name_gives_its_document_name_and_doc_ref() =>
        Assert.Equal(new CopyNameParts("Settlement Agreement", "#1234 v2.1", ReadOnly: false),
            CopyName.Parse("/support/Copies/Settlement Agreement #1234 v2.1.pdf"));

    [Fact]
    public void A_read_only_copy_says_so() =>
        Assert.Equal(new CopyNameParts("Settlement Agreement", "#1234 v2.1", ReadOnly: true),
            CopyName.Parse("/support/Copies/Settlement Agreement #1234 v2.1 (read-only).pdf"));

    [Fact]
    public void The_doc_ref_is_the_last_one_in_the_name() =>
        Assert.Equal(new CopyNameParts("Bill #7 draft", "#1234 v3", ReadOnly: false), CopyName.Parse("Bill #7 draft #1234 v3.pdf"));

    [Theory]
    [InlineData("Report.pdf")]
    [InlineData("Report #1234.pdf")]
    [InlineData("Lease #1234 v2.1.docx")]
    public void Other_names_arent_a_copys(string name) => Assert.Null(CopyName.Parse(name));

    [Fact]
    public void The_Helpers_title_gives_the_document_name()
    {
        Assert.Equal("Settlement Agreement", CopyName.NameIn("Settlement Agreement #1234 v2.2", "#1234 v2.2"));
        Assert.Equal("Settlement Agreement", CopyName.NameIn("Settlement Agreement #1234 v2.1 (read-only)", "#1234 v2.1"));
        Assert.Null(CopyName.NameIn("Settlement Agreement #1234 v2.1", "#1234 v2.2"));
        Assert.Null(CopyName.NameIn("Settlement Agreement", ""));
    }
}
