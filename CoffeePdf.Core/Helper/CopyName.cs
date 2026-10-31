using System.Text.RegularExpressions;

namespace CoffeePdf.Core.Helper;

/// <summary>A copy's file name, split: its document's name, its doc ref, and whether it's a read-only copy.</summary>
public sealed record CopyNameParts(string Name, string DocRef, bool ReadOnly);

/// <summary>
/// The Helper's names for its local copies (the Desktop Clients page's "Local Copies"):
/// <c>Settlement Agreement #1234 v2.1.pdf</c>, and <c>… v2.1 (read-only).pdf</c> for a read-only copy. The Helper's
/// <c>title</c> for a copy is the same name without its extension.
/// </summary>
public static partial class CopyName
{
    private const string ReadOnlyTail = " (read-only)";

    /// <summary>The parts of a copy's file name, or null for a name that isn't a PDF copy's.</summary>
    public static CopyNameParts? Parse(string path)
    {
        var match = FileName().Match(Path.GetFileName(path));
        return match.Success ? new CopyNameParts(match.Groups["name"].Value, match.Groups["ref"].Value, match.Groups["ro"].Success) : null;
    }

    /// <summary>
    /// The document's name in the Helper's <paramref name="title"/>, without <paramref name="docRef"/> and the
    /// read-only mark, or null when the title doesn't end in that doc ref.
    /// </summary>
    public static string? NameIn(string title, string docRef)
    {
        var rest = title.EndsWith(ReadOnlyTail, StringComparison.Ordinal) ? title[..^ReadOnlyTail.Length] : title;
        var tail = " " + docRef;
        return docRef.Length > 0 && rest.Length > tail.Length && rest.EndsWith(tail, StringComparison.Ordinal) ? rest[..^tail.Length] : null;
    }

    // The name runs to the last " #<doc #> v<version>", so a name with its own "#7" in it keeps it.
    [GeneratedRegex(@"^(?<name>.+) (?<ref>#\d+ v\d+(?:\.\d+)*)(?<ro> \(read-only\))?\.pdf$", RegexOptions.IgnoreCase)]
    private static partial Regex FileName();
}
