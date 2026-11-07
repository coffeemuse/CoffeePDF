namespace CoffeePdf.Core.Tests;

/// <summary>
/// A probe for Claude's pass on a test build (docs/DEVTOOLS.md). It runs only with <see cref="Variable"/> set, and is
/// skipped otherwise. The variable should name a folder outside the repository, where the pass's PDFs go.
/// </summary>
public sealed class PassFolderFactAttribute : FactAttribute
{
    public const string Variable = "COFFEEPDF_PASS_DIR";

    public PassFolderFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 })
            Skip = $"For Claude's pass on a test build: set {Variable} to a folder outside the repository";
    }

    /// <summary>
    /// The folder <see cref="Variable"/> names. It throws unless that's an absolute path outside any git work tree, as
    /// launch-test-build.sh asks of its folder, so no PDF lands in the repository or beside the tests' binaries.
    /// </summary>
    public static string Folder()
    {
        var folder = Environment.GetEnvironmentVariable(Variable)!;
        if (!Path.IsPathFullyQualified(folder))
            throw new InvalidOperationException($"{Variable} must be an absolute path, outside the repository");
        for (var at = new DirectoryInfo(Path.GetFullPath(folder)); at is not null; at = at.Parent)
        {
            var git = Path.Combine(at.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
                throw new InvalidOperationException($"{Variable} must name a folder outside any git work tree");
        }
        return folder;
    }
}
