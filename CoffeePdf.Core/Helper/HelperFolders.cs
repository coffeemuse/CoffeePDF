using System.Text;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// Where the Healdan Helper keeps its files (spec §4.3; the Desktop Clients page's "The Helper's Files"): its support
/// folder, the local copies directly in <c>Copies/</c>, the files for filing in <c>Temp/</c>, and <c>channel.json</c>.
/// </summary>
public sealed class HelperFolders(string support)
{
    /// <summary>Puts the Helper's files under one folder, as the Helper itself does when it's set (§4.3).</summary>
    public const string DevRootVariable = "HEALDAN_HELPER_DEV_ROOT";

    public string Support { get; } = Path.GetFullPath(support);

    public string Copies => Path.Combine(Support, "Copies");

    public string Temp => Path.Combine(Support, "Temp");

    public string ChannelFile => Path.Combine(Support, "channel.json");

    /// <summary>
    /// This user's: under <see cref="DevRootVariable"/> when it's set, else <c>~/Library/Application Support/Healdan
    /// Helper/</c> on macOS, and <c>$XDG_DATA_HOME/healdan-helper/</c> (or <c>~/.local/share/healdan-helper/</c>)
    /// elsewhere.
    /// </summary>
    public static HelperFolders ForThisUser()
    {
        if (Environment.GetEnvironmentVariable(DevRootVariable) is { Length: > 0 } root)
            return new HelperFolders(root);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return new HelperFolders(Path.Combine(home, "Library", "Application Support", "Healdan Helper"));
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".local", "share");
        return new HelperFolders(Path.Combine(data, "healdan-helper"));
    }

    /// <summary>
    /// Whether <paramref name="path"/> is directly in <see cref="Copies"/>, where every local copy is (§6.1). Links are
    /// followed (<c>/var</c> is <c>/private/var</c> on macOS) and names compared in Unicode's composed form, as the
    /// Helper's own check does, so a file the Helper calls a copy is one here too.
    /// </summary>
    public bool HoldsCopy(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        return folder is not null && Same(Real(folder), Real(Copies));
    }

    private static bool Same(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a).Normalize(NormalizationForm.FormC),
            Path.TrimEndingDirectorySeparator(b).Normalize(NormalizationForm.FormC), StringComparison.Ordinal);

    /// <summary><paramref name="path"/> with each link in it followed. A part that doesn't exist is kept as it is.</summary>
    private static string Real(string path, int depth = 0)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            // 32 links deep is a loop.
            if (depth < 32 && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                current = Real(target.FullName, depth + 1);
        }
        return current;
    }
}
