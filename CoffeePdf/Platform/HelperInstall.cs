using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>Whether the Healdan Helper is installed, for the first launch's question (spec §5).</summary>
internal interface IHelperInstall
{
    bool IsInstalled();
}

/// <summary>The Helper is installed when Launch Services knows an app with its bundle id, wherever it is.</summary>
internal sealed unsafe partial class MacHelperInstall(string bundleId = MacHelperInstall.BundleId) : IHelperInstall
{
    public const string BundleId = "com.healdan.helper";

    public bool IsInstalled()
    {
        if (!OperatingSystem.IsMacOS())
            return false;
        var id = CoreFoundation.CreateString(bundleId);
        try
        {
            var apps = LSCopyApplicationURLsForBundleIdentifier(id, null);
            if (apps == 0)
                return false;
            try
            {
                return CoreFoundation.ArrayCount(apps) > 0;
            }
            finally
            {
                CoreFoundation.Release(apps);
            }
        }
        finally
        {
            CoreFoundation.Release(id);
        }
    }

    [LibraryImport(CoreFoundation.CoreServices)] private static partial nint LSCopyApplicationURLsForBundleIdentifier(nint bundleId, nint* error);
}
