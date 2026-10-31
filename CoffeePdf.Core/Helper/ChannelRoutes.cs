namespace CoffeePdf.Core.Helper;

/// <summary>How long CoffeePDF waits for a route's answer (spec §4.3; the limits are <see cref="HelperChannel.DefaultWaits"/>).</summary>
public enum ChannelWait
{
    /// <summary>Answered at once.</summary>
    Quick,

    /// <summary><c>/v1/saved</c>, which the Helper answers within 120 seconds.</summary>
    Saved,

    /// <summary><c>/v1/file</c>, which waits for Healdan's filing.</summary>
    Filing,

    /// <summary>The window routes, which answer when the user closes the Helper's window: never timed out (§6.3).</summary>
    Window,
}

/// <summary>One of the Helper's channel routes that CoffeePDF calls: a <c>POST</c>, with its body and answer as the Desktop Clients page has them.</summary>
public sealed record ChannelRoute(string Name, ChannelWait Wait, string Body, string Answer)
{
    public string Path => "/v1/" + Name;
}

/// <summary>
/// Every route CoffeePDF calls on the Helper's channel (spec §9), with its body and answer, following the Desktop
/// Clients page's table (<c>site/content/developer/desktop-clients.md</c> in Healdan-Project/healdan). The channel has
/// no machine-readable contract, so this is CoffeePDF's. The client calls only these, the fake Helper serves only
/// these, and a change to the channel in Healdan changes this list in the same release. <c>/v1/link</c> is for a second
/// Helper, and CoffeePDF never calls it.
/// </summary>
public static class ChannelRoutes
{
    /// <summary>The header every route's request carries the secret in.</summary>
    public const string SecretHeader = "X-Healdan-Secret";

    /// <summary>A copy's state, as the routes that answer one give it.</summary>
    public const string StateShape = """{"known": true, "docNumber", "title", "docRef", "readOnly", "path", "holdBy", "pending"}, or {"known": false}""";

    public static readonly ChannelRoute OpenDialog = new("open-dialog", ChannelWait.Window,
        """{"app"}""", """{"action": "opened" | "local" | "cancel"}, once the window closes""");

    public static readonly ChannelRoute SaveNew = new("save-new", ChannelWait.Window,
        """{"app", "name", "formats", "path"}; path is the copy, for Save as New Document""",
        """{"action": "save" | "local" | "cancel", "folderId", "name", "format", "properties", "tempPath"}""");

    public static readonly ChannelRoute File = new("file", ChannelWait.Filing,
        """{"tempPath", "folderId", "name", "format", "properties", "app", "replaces"}""", "the new copy's state");

    public static readonly ChannelRoute Adopted = new("adopted", ChannelWait.Quick,
        """{"path"}""", "the copy's state; the file as it is now is the new copy's starting point");

    public static readonly ChannelRoute Saved = new("saved", ChannelWait.Saved,
        """{"path"}""", "the copy's state once Healdan has every save written so far, at once when nothing is on its way, else after 120 s as it is");

    public static readonly ChannelRoute NewVersion = new("new-version", ChannelWait.Quick,
        """{"path"}""", "{}; the copy's next save goes up as a version");

    public static readonly ChannelRoute Properties = new("properties", ChannelWait.Window,
        """{"path"}""", """{"action": "saved" | "cancel" | "file"}; file means it isn't in Healdan, so the caller runs Save to Healdan""");

    public static readonly ChannelRoute Closed = new("closed", ChannelWait.Quick, """{"path"}""", "{}");

    public static readonly ChannelRoute State = new("state", ChannelWait.Quick, """{"path"}""", "the copy's state");

    /// <summary>Every route, in the Desktop Clients page's order.</summary>
    public static IReadOnlyList<ChannelRoute> All { get; } = [OpenDialog, SaveNew, File, Adopted, Saved, NewVersion, Properties, Closed, State];
}
