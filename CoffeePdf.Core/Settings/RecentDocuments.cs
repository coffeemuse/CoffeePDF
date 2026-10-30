using System.Text.Json;
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Settings;

/// <summary>
/// File › Open Recent (spec §2.1): the PDFs last opened or saved as, newest first, as a JSON array of full paths in
/// <c>recent.json</c> beside the preferences. A change that can't be written is dropped. Phase 2 keeps Healdan's copies
/// out (§6.10).
/// </summary>
public sealed class RecentDocuments(string folder)
{
    public const int Capacity = 10;

    public string FilePath { get; } = Path.Combine(folder, "recent.json");

    /// <summary>Raised after every change, so each window's Open Recent can follow.</summary>
    public event EventHandler? Changed;

    /// <summary>The files, newest first, leaving out any that are gone.</summary>
    public IReadOnlyList<string> Paths => Load().Where(File.Exists).ToList();

    public void Add(string path)
    {
        var full = Path.GetFullPath(path);
        Save([full, .. Load().Where(p => p != full)]);
    }

    public void Remove(string path)
    {
        var full = Path.GetFullPath(path);
        Save(Load().Where(p => p != full).ToList());
    }

    public void Clear() => Save([]);

    private List<string> Load()
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllBytes(FilePath)) ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private void Save(List<string> paths)
    {
        try
        {
            Directory.CreateDirectory(folder);
            AtomicFile.Write(FilePath, JsonSerializer.SerializeToUtf8Bytes(paths.Take(Capacity).ToList()));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Open Recent is a convenience, so a list that can't be written is dropped rather than failing the save or
            // open that changed it.
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
