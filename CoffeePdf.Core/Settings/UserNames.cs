using System.ComponentModel;
using System.Diagnostics;

namespace CoffeePdf.Core.Settings;

/// <summary>The account's full name, the Markup author's default (spec §5). Never logged or printed.</summary>
public static class UserNames
{
    /// <summary>
    /// macOS's full name (<c>id -F</c>), or on Linux the passwd entry's comment, falling back to the account's user
    /// name. Runs a process on macOS, so callers keep the answer.
    /// </summary>
    public static string FullName()
    {
        var name = OperatingSystem.IsMacOS() ? RunIdF()
            : OperatingSystem.IsLinux() && File.Exists("/etc/passwd") ? Gecos(File.ReadLines("/etc/passwd"), Environment.UserName)
            : null;
        return string.IsNullOrWhiteSpace(name) ? Environment.UserName : name.Trim();
    }

    /// <summary>The first comma-separated part of <paramref name="user"/>'s comment in <paramref name="passwdLines"/>, or null.</summary>
    public static string? Gecos(IEnumerable<string> passwdLines, string user)
    {
        foreach (var line in passwdLines)
        {
            var fields = line.Split(':');
            if (fields.Length >= 5 && fields[0] == user)
                return fields[4].Split(',')[0] is { Length: > 0 } name ? name : null;
        }
        return null;
    }

    private static string? RunIdF()
    {
        try
        {
            var start = new ProcessStartInfo("/usr/bin/id") { RedirectStandardOutput = true, UseShellExecute = false };
            start.ArgumentList.Add("-F");
            using var id = Process.Start(start);
            if (id is null)
                return null;
            var name = id.StandardOutput.ReadToEnd();
            id.WaitForExit();
            return id.ExitCode == 0 ? name : null;
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
