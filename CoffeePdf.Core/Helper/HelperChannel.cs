using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Logging;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// The Healdan Helper's local channel (spec §4.3, §9): HTTP on 127.0.0.1, at the port in <c>channel.json</c>, with its
/// secret in every request. The file is read at each call, so a Helper that restarted is found at its new port. It
/// calls only <see cref="ChannelRoutes"/>, and never holds a Healdan token or calls Healdan itself (§4.1).
/// </summary>
/// <remarks>
/// Nothing it logs holds the secret or a file's contents: a call is logged by its route and outcome, and a failure to
/// reach the Helper by the exception's type, since its message could quote the request. Its awaits keep the caller's
/// context, so a window's code carries on on the UI thread.
/// </remarks>
public sealed class HelperChannel(HelperFolders folders, CoffeeLog log, IReadOnlyDictionary<ChannelWait, TimeSpan>? waits = null)
{
    /// <summary>The app CoffeePDF names itself as to the Helper's windows and in its check-outs (§6.2, §6.3).</summary>
    public const string App = "CoffeePDF";

    /// <summary>
    /// How long each kind of route is waited for: the LibreOffice extension's limits (its <c>healdan_logic.py</c>),
    /// except the window routes, which are waited for however long the user takes (§4.3).
    /// </summary>
    public static readonly IReadOnlyDictionary<ChannelWait, TimeSpan> DefaultWaits = new Dictionary<ChannelWait, TimeSpan>
    {
        [ChannelWait.Quick] = TimeSpan.FromSeconds(10),
        [ChannelWait.Saved] = TimeSpan.FromSeconds(135),
        [ChannelWait.Filing] = TimeSpan.FromSeconds(660),
        [ChannelWait.Window] = Timeout.InfiniteTimeSpan,
    };

    // One client for the app's life, with no proxy, since the channel is on this computer. Its own time limit is off:
    // each call sets its route's.
    private static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };

    private readonly IReadOnlyDictionary<ChannelWait, TimeSpan> _waits = waits ?? DefaultWaits;

    /// <summary><c>/v1/state</c>: the copy's state, which sets a window's mode (§6.2).</summary>
    public async Task<CopyState> StateAsync(string path, CancellationToken cancel = default) =>
        CopyState.From(await CallAsync(ChannelRoutes.State, WithPath(path), cancel), path);

    /// <summary><c>/v1/saved</c>: the copy's state once Healdan has every save so far, or after 120 seconds as it is (§6.4).</summary>
    public async Task<CopyState> SavedAsync(string path, CancellationToken cancel = default) =>
        CopyState.From(await CallAsync(ChannelRoutes.Saved, WithPath(path), cancel), path);

    /// <summary><c>/v1/adopted</c>: the window is now the new copy's, and the file as it is now is its starting point (§6.6).</summary>
    public async Task<CopyState> AdoptedAsync(string path) =>
        CopyState.From(await CallAsync(ChannelRoutes.Adopted, WithPath(path), default), path);

    /// <summary><c>/v1/new-version</c>: the copy's next save goes up as a version (§6.5).</summary>
    public Task NewVersionAsync(string path) => CallAsync(ChannelRoutes.NewVersion, WithPath(path), default);

    /// <summary><c>/v1/closed</c>: the copy's window has closed, so the Helper checks it in (§6.8).</summary>
    public Task ClosedAsync(string path) => CallAsync(ChannelRoutes.Closed, WithPath(path), default);

    /// <summary><c>/v1/open-dialog</c>: the Helper's Open window (§6.3).</summary>
    public async Task<OpenDialogAction> OpenDialogAsync() =>
        JsonFields.Text(await CallAsync(ChannelRoutes.OpenDialog, new JsonObject { ["app"] = App }, default), "action") switch
        {
            "opened" => OpenDialogAction.Opened,
            "local" => OpenDialogAction.Local,
            _ => OpenDialogAction.Cancel,
        };

    /// <summary>
    /// <c>/v1/save-new</c>: the Helper's Save window (§6.6), for Save as New Document when <paramref name="copy"/> is
    /// the window's copy, or Save to Healdan when it's null.
    /// </summary>
    public async Task<SaveNewAnswer> SaveNewAsync(string name, string? copy) =>
        SaveNewAnswer.From(await CallAsync(ChannelRoutes.SaveNew, new JsonObject
        {
            ["app"] = App,
            ["name"] = name,
            ["formats"] = new JsonArray("pdf"),
            ["path"] = copy,
        }, default));

    /// <summary>
    /// <c>/v1/file</c>: files the PDF written to the Save window's <c>tempPath</c>, with the answer's values unchanged,
    /// and answers the new copy's state (§6.6). <paramref name="replaces"/> is the copy Save as New Document started from.
    /// An answer without the new copy's path gives an empty one, so the window has no copy to open: the temporary file is
    /// the Helper's, and goes once it's filed.
    /// </summary>
    public async Task<CopyState> FileAsync(SaveNewAnswer answer, string? replaces)
    {
        var body = new JsonObject
        {
            ["tempPath"] = answer.Raw["tempPath"]?.DeepClone(),
            ["folderId"] = answer.Raw["folderId"]?.DeepClone(),
            ["name"] = answer.Raw["name"]?.DeepClone(),
            ["format"] = answer.Raw["format"]?.DeepClone(),
            ["properties"] = answer.Raw["properties"]?.DeepClone() ?? new JsonObject(),
            ["app"] = App,
            ["replaces"] = replaces,
        };
        return CopyState.From(await CallAsync(ChannelRoutes.File, body, default), "");
    }

    /// <summary><c>/v1/properties</c>: the Helper's Properties window (§6.7).</summary>
    public async Task<PropertiesAction> PropertiesAsync(string path) =>
        JsonFields.Text(await CallAsync(ChannelRoutes.Properties, WithPath(path), default), "action") switch
        {
            "saved" => PropertiesAction.Saved,
            "file" => PropertiesAction.File,
            _ => PropertiesAction.Cancel,
        };

    private static JsonObject WithPath(string path) => new() { ["path"] = path };

    private async Task<JsonObject> CallAsync(ChannelRoute route, JsonObject body, CancellationToken cancel)
    {
        var info = ChannelInfo.Read(folders.ChannelFile) ?? throw Unreachable(route, "no channel.json");
        if (!IsRunning(info.Pid))
            throw Unreachable(route, "its process is gone");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        if (_waits[route.Wait] != Timeout.InfiniteTimeSpan)
            limit.CancelAfter(_waits[route.Wait]);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{info.Port}{route.Path}")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        request.Headers.Add(ChannelRoutes.SecretHeader, info.Secret);
        int status;
        byte[] raw;
        try
        {
            using var response = await Http.SendAsync(request, limit.Token);
            status = (int)response.StatusCode;
            raw = await response.Content.ReadAsByteArrayAsync(limit.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            throw; // the caller stopped waiting, as when a window closes while it follows a save
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            throw Unreachable(route, e.GetType().Name);
        }
        var answer = Parse(raw);
        if (status != 200)
        {
            var code = answer is not null ? JsonFields.Text(answer, "error") ?? "no code" : "no code";
            log.Warn($"{route.Path} answered {status} ({code})");
            throw new HelperErrorException(status, code);
        }
        if (answer is null)
        {
            log.Warn($"{route.Path} answered something that isn’t a JSON object");
            throw new HelperErrorException(status, "not-json");
        }
        log.Info($"{route.Path} answered");
        return answer;
    }

    private static JsonObject? Parse(byte[] raw)
    {
        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private HelperUnreachableException Unreachable(ChannelRoute route, string why)
    {
        log.Info($"{route.Path}: Healdan Helper can’t be reached ({why})");
        return new HelperUnreachableException($"Healdan Helper can’t be reached ({why})");
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
