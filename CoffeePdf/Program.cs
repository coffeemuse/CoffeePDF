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

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
