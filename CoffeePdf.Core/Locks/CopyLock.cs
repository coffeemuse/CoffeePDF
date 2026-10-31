using System.Text.Json;

namespace CoffeePdf.Core.Locks;

/// <summary>
/// The lock file CoffeePDF keeps beside an editable copy while its window is open (spec §6.8):
/// <c>.~coffeepdf.&lt;copy's file name&gt;#</c>, which the Helper's <c>CoffeePdfApp.LockExists</c> looks for (§4.4
/// item 1). Its 13 added bytes fit within the 14 that the Helper's 241-byte limit on copies' names leaves. It holds
/// CoffeePDF's process id and when the window opened, for diagnosis. The Helper only checks that it's there, and
/// treats one left with no CoffeePDF running as a crash's.
/// </summary>
public static class CopyLock
{
    public const string Prefix = ".~coffeepdf.";

    public const string Suffix = "#";

    public static string PathFor(string copy)
    {
        var full = Path.GetFullPath(copy);
        return Path.Combine(Path.GetDirectoryName(full)!, Prefix + Path.GetFileName(full) + Suffix);
    }

    /// <summary>Writes the lock for <paramref name="copy"/>, replacing one a crash left.</summary>
    public static void Write(string copy, int processId, DateTimeOffset opened) =>
        File.WriteAllText(PathFor(copy), JsonSerializer.Serialize(new { pid = processId, opened = opened.ToString("O") }));

    /// <summary>
    /// Removes the lock for <paramref name="copy"/>, and returns whether there was one. It never throws: a lock that
    /// can't be removed is left to the Helper's crash handling (§6.8).
    /// </summary>
    public static bool TryRemove(string copy)
    {
        try
        {
            var path = PathFor(copy);
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
