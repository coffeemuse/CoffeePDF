using Avalonia;
using Avalonia.Controls;

namespace CoffeePdf;

/// <summary>Starts CoffeePDF. On macOS one instance runs, and the system hands it every PDF opened (spec §4.3).</summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnLastWindowClose);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
