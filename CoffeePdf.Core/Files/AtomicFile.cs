namespace CoffeePdf.Core.Files;

/// <summary>
/// Writes a file whole or not at all: to a temporary file beside it, then renamed over it, as the PdfViewer's own
/// save did (spec §16 row 2). A crash part-way leaves the old file, never half a new one, and Healdan's Helper learns
/// to count the rename as a save (§4.4 item 8, §16 Decision 3).
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// The temporary file's name is <c>.coffeepdf-</c>, 32 hex digits and <c>.tmp</c>, whatever the target's name, so
    /// even a Healdan copy's longest name fits. The Helper's watch ignores names like it (§4.4 item 8).
    /// </summary>
    public const string TemporaryPrefix = ".coffeepdf-";

    public const string TemporarySuffix = ".tmp";

    /// <summary>
    /// Writes <paramref name="bytes"/> to <paramref name="path"/>, keeping an existing file's permissions. On failure
    /// the file is as it was, and the exception is the one that stopped it.
    /// </summary>
    public static void Write(string path, ReadOnlySpan<byte> bytes)
    {
        var target = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(target)!, $"{TemporaryPrefix}{Guid.NewGuid():N}{TemporarySuffix}");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsWindows() && File.Exists(target))
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The write's own failure is the one to report.
        }
    }
}
