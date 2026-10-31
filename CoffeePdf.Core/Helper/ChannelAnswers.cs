using System.Text.Json.Nodes;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// A copy's state, as the channel's routes answer it (<see cref="ChannelRoutes.StateShape"/>).
/// </summary>
/// <param name="Known">False for a path the Helper has no copy at; the rest is then empty.</param>
/// <param name="Title">The copy's file name without its extension: <c>Settlement Agreement #1234 v2.1</c>, with <c> (read-only)</c> for a read-only copy.</param>
/// <param name="DocRef">The version Healdan has: <c>#1234 v2.1</c>.</param>
/// <param name="ReadOnly">Opened without a check-out, or (spec §6.9) no longer checked out here.</param>
/// <param name="HoldBy">For a read-only copy, the name of whoever has the document checked out, if anyone.</param>
/// <param name="Pending">Changes are still on their way to Healdan.</param>
public sealed record CopyState(bool Known, long DocNumber, string Title, string DocRef, bool ReadOnly, string Path, string? HoldBy, bool Pending)
{
    public static CopyState Unknown(string path) => new(false, 0, "", "", false, path, null, false);

    /// <summary>Reads an answer. One that doesn't say <c>"known": true</c> is <see cref="Unknown"/> for <paramref name="path"/>.</summary>
    public static CopyState From(JsonObject answer, string path) =>
        !JsonFields.Flag(answer, "known")
            ? Unknown(path)
            : new CopyState(true, JsonFields.Number(answer, "docNumber"), JsonFields.Text(answer, "title") ?? "",
                JsonFields.Text(answer, "docRef") ?? "", JsonFields.Flag(answer, "readOnly"), JsonFields.Text(answer, "path") ?? path,
                JsonFields.Text(answer, "holdBy"), JsonFields.Flag(answer, "pending"));
}

/// <summary><c>/v1/open-dialog</c>'s answer (§6.3). Anything else the Helper might answer counts as <see cref="Cancel"/>.</summary>
public enum OpenDialogAction
{
    /// <summary>The Helper opened what was chosen, in the app for its kind.</summary>
    Opened,

    /// <summary>The user chose the Mac's own Open box.</summary>
    Local,

    Cancel,
}

/// <summary><c>/v1/save-new</c>'s action (§6.6). Anything else counts as <see cref="Cancel"/>.</summary>
public enum SaveNewAction
{
    /// <summary>Write the PDF to the answer's <c>tempPath</c>, then file it.</summary>
    Save,

    /// <summary>The user chose the Mac's own Save box.</summary>
    Local,

    Cancel,
}

/// <summary><c>/v1/properties</c>'s answer (§6.7). Anything else counts as <see cref="Cancel"/>.</summary>
public enum PropertiesAction
{
    Saved,
    Cancel,

    /// <summary>The PDF isn't in Healdan, so Save to Healdan runs instead.</summary>
    File,
}

/// <summary>
/// <c>/v1/save-new</c>'s answer (§6.6): its action, and on <see cref="SaveNewAction.Save"/> the values
/// <c>/v1/file</c> takes back unchanged, which <see cref="Raw"/> keeps as they came.
/// </summary>
public sealed record SaveNewAnswer(SaveNewAction Action, string? TempPath, string? Format, JsonObject Raw)
{
    public static SaveNewAnswer From(JsonObject answer) =>
        new(JsonFields.Text(answer, "action") switch
        {
            "save" => SaveNewAction.Save,
            "local" => SaveNewAction.Local,
            _ => SaveNewAction.Cancel,
        }, JsonFields.Text(answer, "tempPath"), JsonFields.Text(answer, "format"), answer);
}

/// <summary>Reads an answer's fields, taking a field of the wrong type as missing.</summary>
internal static class JsonFields
{
    public static string? Text(JsonObject json, string key) => json[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    public static bool Flag(JsonObject json, string key) => json[key] is JsonValue value && value.TryGetValue(out bool flag) && flag;

    public static long Number(JsonObject json, string key) => json[key] is JsonValue value && value.TryGetValue(out long number) ? number : 0;
}
