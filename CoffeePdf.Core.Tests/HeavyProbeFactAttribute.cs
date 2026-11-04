namespace CoffeePdf.Core.Tests;

/// <summary>
/// A probe too heavy for every run of the tests, such as one that builds a large PDF. It runs only with
/// <see cref="Variable"/> set, as CLAUDE.md's command for the probes sets it, and is skipped otherwise.
/// </summary>
public sealed class HeavyProbeFactAttribute : FactAttribute
{
    public const string Variable = "COFFEEPDF_PROBES";

    public HeavyProbeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 })
            Skip = $"A heavy probe: set {Variable}=1 to run it";
    }
}
