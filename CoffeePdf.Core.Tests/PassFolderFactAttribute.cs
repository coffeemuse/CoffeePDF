namespace CoffeePdf.Core.Tests;

/// <summary>
/// A probe for Claude's pass on a test build (docs/DEVTOOLS.md). It runs only with <see cref="Variable"/> naming a folder
/// outside the repository, where the pass's PDFs go, and is skipped otherwise.
/// </summary>
public sealed class PassFolderFactAttribute : FactAttribute
{
    public const string Variable = "COFFEEPDF_PASS_DIR";

    public PassFolderFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 })
            Skip = $"For Claude's pass on a test build: set {Variable} to a folder outside the repository";
    }
}
