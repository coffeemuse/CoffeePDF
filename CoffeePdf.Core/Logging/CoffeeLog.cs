using System.Globalization;
using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Logging;

/// <summary>
/// CoffeePDF's log (spec §6.9), a line per event, in <c>coffeepdf.log</c>. Nothing logs the channel's secret or a
/// file's contents, and an exception is logged by its type, since its message can quote either. A line that can't be
/// written is dropped: the log never stops a save or a close.
/// </summary>
public sealed class CoffeeLog(string folder)
{
    /// <summary>Overrides the folder, for tests and development, as <see cref="SettingsLocation.OverrideVariable"/> does the settings'.</summary>
    public const string OverrideVariable = "COFFEEPDF_LOG_DIR";

    private readonly Lock _gate = new();

    public string FilePath { get; } = Path.Combine(folder, "coffeepdf.log");

    /// <summary>
    /// <c>~/Library/Logs/CoffeePDF/</c> on macOS, unless <see cref="OverrideVariable"/> is set. Elsewhere it's the
    /// settings folder, as the Helper keeps its log in its support folder on Linux, until the Linux phase (§13).
    /// </summary>
    public static string Folder()
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } overridden)
            return overridden;
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "CoffeePDF");
        return SettingsLocation.Folder();
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    private void Write(string level, string message)
    {
        // In the invariant culture, so the time reads the same whatever the Mac's region: a Thai calendar's year is 543
        // ahead, for one.
        var line = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {level} {message}{Environment.NewLine}");
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(FilePath, line);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The log is for diagnosis: a line that can't be written mustn't stop what it describes.
            }
        }
    }
}
