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

    /// <summary>
    /// How long <c>id -F</c> gets. It asks the directory service, and a Mac bound to a directory it can't reach would
    /// otherwise hold up the window that asked for the name (the first save, or Settings…) for as long as that takes.
    /// </summary>
    private static readonly TimeSpan IdTimeout = TimeSpan.FromSeconds(2);

    private static string? RunIdF()
    {
        try
        {
            var start = new ProcessStartInfo("/usr/bin/id") { RedirectStandardOutput = true, UseShellExecute = false };
            start.ArgumentList.Add("-F");
            using var id = Process.Start(start);
            if (id is null)
                return null;
            // The output is read once id has exited: a name fits the pipe's buffer, so id never waits for the reader.
            if (!id.WaitForExit(IdTimeout))
            {
                Kill(id);
                return null; // the user name stands in for the full name
            }
            var name = id.StandardOutput.ReadToEnd();
            return id.ExitCode == 0 ? name : null;
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // It ended just now, or it can't be killed: either way there's nothing more to do, and the caller moves on.
        }
    }
}
