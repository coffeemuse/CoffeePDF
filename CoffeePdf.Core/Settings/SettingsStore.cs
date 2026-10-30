using System.Text.Json;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Settings;

/// <summary>
/// The preferences (spec §5), in <c>settings.json</c>. The Helper reads <c>healdan.enabled</c> from it (§4.4 item 5), so
/// its shape is a contract: <c>{ "healdan": { "enabled": false }, "markup": { "author": "…" } }</c>. Keys this version
/// doesn't know are kept. A file that can't be read gives the defaults, and the next change replaces it.
/// </summary>
public sealed class SettingsStore(string folder)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public string FilePath { get; } = Path.Combine(folder, "settings.json");

    /// <summary>Healdan support (§5), off by default. Phase 1 only keeps it, and Phase 2 acts on it.</summary>
    public bool HealdanEnabled
    {
        get => Load()["healdan"]?["enabled"] is JsonValue value && value.TryGetValue(out bool enabled) && enabled;
        set => Change(root => Section(root, "healdan")["enabled"] = value);
    }

    /// <summary>The Markup author the user set (§5), or null to follow the macOS full name. Setting it blank clears it.</summary>
    public string? MarkupAuthor
    {
        get => Load()["markup"]?["author"] is JsonValue value && value.TryGetValue(out string? author) && !string.IsNullOrWhiteSpace(author)
            ? author
            : null;
        set => Change(root =>
        {
            var markup = Section(root, "markup");
            if (string.IsNullOrWhiteSpace(value))
                markup.Remove("author");
            else
                markup["author"] = value.Trim();
        });
    }

    private JsonObject Load()
    {
        try
        {
            return JsonNode.Parse(File.ReadAllBytes(FilePath)) as JsonObject ?? new JsonObject();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new JsonObject();
        }
    }

    private void Change(Action<JsonObject> change)
    {
        var root = Load();
        change(root);
        Directory.CreateDirectory(folder);
        AtomicFile.Write(FilePath, System.Text.Encoding.UTF8.GetBytes(root.ToJsonString(Indented)));
    }

    private static JsonObject Section(JsonObject root, string name)
    {
        if (root[name] is JsonObject section)
            return section;
        section = new JsonObject();
        root[name] = section;
        return section;
    }
}
