namespace CoffeePdf.Tests;

/// <summary>Phase 0's probes (spec §14): files kept for the owner to check by hand, in Acrobat or Preview.</summary>
internal static class Probe
{
    /// <summary>Copies <paramref name="path"/> into <c>COFFEEPDF_PROBE_OUT</c> as <paramref name="name"/>, when that's set.</summary>
    public static void Keep(string path, string name)
    {
        if (Environment.GetEnvironmentVariable("COFFEEPDF_PROBE_OUT") is not { Length: > 0 } outDir)
            return;
        Directory.CreateDirectory(outDir);
        File.Copy(path, Path.Combine(outDir, name), overwrite: true);
    }
}
