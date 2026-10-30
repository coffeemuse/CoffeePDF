using System.Text;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Fixtures;

/// <summary>One page of <see cref="MinimalPdf"/> carrying the annotations given, as objects 7 on.</summary>
internal static class AnnotatedPdf
{
    public static byte[] Build(params string[] annotations)
    {
        var references = annotations.Select((_, i) => $"{7 + i} 0 R").ToList();
        var objects = MinimalPdf.Objects(withForeignNote: false, extraAnnotations: references, acroForm: null);
        objects.AddRange(annotations);
        return MinimalPdf.Write(objects);
    }

    /// <summary>A sticky note, with a name (<c>/NM</c>) and an author (<c>/T</c>) when they're given.</summary>
    public static string Note(string? name, string? author) =>
        "<< /Type /Annot /Subtype /Text /Rect [72 600 96 624] /Contents (Note)" +
        (name is null ? "" : $" /NM {TextString(name)}") + (author is null ? "" : $" /T {TextString(author)}") + " /F 4 >>";

    /// <summary>A text field's widget, with a name and no field name (<c>/T</c>), as a form's kid can have.</summary>
    public static string Widget(string name) =>
        $"<< /Type /Annot /Subtype /Widget /FT /Tx /Rect [72 500 272 520] /NM {TextString(name)} /F 4 >>";

    /// <summary>
    /// A PDF text string. <see cref="MinimalPdf.Write"/> encodes the file as ASCII, so text with other characters goes
    /// in as UTF-16BE hex (ISO 32000-1 §7.9.2.2), which keeps every character of the file one byte.
    /// </summary>
    private static string TextString(string text) => text.All(char.IsAscii)
        ? $"({text})"
        : "<FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text)) + ">";
}
