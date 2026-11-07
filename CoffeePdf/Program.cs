using Avalonia;
using Avalonia.Controls;

namespace CoffeePdf;

/// <summary>Starts CoffeePDF. On macOS one instance runs, and the system hands it every PDF opened (spec §4.3).</summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        // Running with no window open, as Preview does (owner, 2026-10-30): ⌘Q quits.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
#if COFFEEPDF_DEVTOOLS
        // Avalonia's Developer Tools, in a test build only (the devtools spec's §4): from launch the app listens for avdt
        // on a pipe of its own, and F12 opens avdt's window. A release never carries them, which package.sh checks.
        builder = builder.WithDeveloperTools();
#endif
        return builder;
    }

    /// <summary>
    /// Whether this build carries Avalonia's Developer Tools, which only a build with <c>CoffeePdfDevTools</c> set does
    /// (the devtools spec's §4).
    /// </summary>
    internal static bool CarriesDeveloperTools =>
#if COFFEEPDF_DEVTOOLS
        true;
#else
        false;
#endif
}
