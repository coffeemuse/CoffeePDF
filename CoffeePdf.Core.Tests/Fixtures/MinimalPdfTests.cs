using System.Text;
using System.Text.RegularExpressions;

namespace CoffeePdf.Tests.Fixtures;

public sealed class MinimalPdfTests
{
    [Fact]
    public void Every_cross_reference_entry_points_at_its_object()
    {
        var text = Encoding.ASCII.GetString(MinimalPdf.Build(withForeignNote: true));
        var xref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n").Groups[1].Value);
        Assert.StartsWith("xref\n", text[xref..]);
        var entries = Regex.Matches(text[xref..], @"(\d{10}) 00000 n\r\n");
        Assert.Equal(6, entries.Count);
        for (var i = 0; i < entries.Count; i++)
            Assert.StartsWith($"{i + 1} 0 obj\n", text[int.Parse(entries[i].Groups[1].Value)..]);
    }

    [Fact]
    public void The_foreign_note_is_on_the_page_only_when_asked_for()
    {
        Assert.DoesNotContain("/Annots", Encoding.ASCII.GetString(MinimalPdf.Build()));
        Assert.Contains("/Annots [6 0 R]", Encoding.ASCII.GetString(MinimalPdf.Build(withForeignNote: true)));
    }
}
