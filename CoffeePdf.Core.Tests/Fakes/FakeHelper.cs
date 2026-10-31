using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoffeePdf.Core.Helper;
using CoffeePdf.Tests.Fixtures;

namespace CoffeePdf.Core.Tests.Fakes;

/// <summary>
/// A Healdan Helper for the tests (spec §9). Its channel is on 127.0.0.1, with <c>channel.json</c>, <c>Copies/</c> and
/// <c>Temp/</c> in a support folder of its own, and it serves exactly <see cref="ChannelRoutes"/>, as the Helper's
/// server does: a <c>POST</c>, the secret, a JSON body, and <c>{"error": code}</c> for a refusal.
/// </summary>
/// <remarks>
/// Each route answers from a handler the test can replace. By default the state routes answer the copies added,
/// <c>file</c> files the temporary file as a new copy, the window routes answer <c>cancel</c>, and the rest answer
/// <c>{}</c>. A call to any other route is answered 404 and fails the test at <see cref="Dispose"/>. The app's tests
/// compile this file too.
/// </remarks>
internal sealed class FakeHelper : IDisposable
{
    /// <summary>The channel's secret: 48 hex digits, as the Helper's.</summary>
    public const string Secret = "5ec7e75ec7e75ec7e75ec7e75ec7e75ec7e75ec7e75ec7e7";

    private readonly HttpListener _listener;
    private readonly ConcurrentQueue<(string Route, JsonObject Body)> _calls = new();
    private readonly ConcurrentQueue<string> _unlisted = new();
    private readonly ConcurrentDictionary<string, Func<JsonObject, Task<JsonNode>>> _answers = new();
    private readonly ConcurrentDictionary<string, CopyState> _copies = new();
    private long _lastDocNumber;

    public FakeHelper()
    {
        Folders = new HelperFolders(Directory.CreateTempSubdirectory("coffeepdf-helper-").FullName);
        Directory.CreateDirectory(Folders.Copies);
        Directory.CreateDirectory(Folders.Temp);
        (_listener, Port) = Listen();
        ComeBack();
        _ = Task.Run(ServeAsync);
        Answer(ChannelRoutes.State, body => Task.FromResult<JsonNode>(Json(StateOf(PathIn(body)))));
        Answer(ChannelRoutes.Saved, body => Task.FromResult<JsonNode>(Json(StateOf(PathIn(body)))));
        Answer(ChannelRoutes.Adopted, body => Task.FromResult<JsonNode>(Json(StateOf(PathIn(body)))));
        Answer(ChannelRoutes.File, FileTemporary);
        Answer(ChannelRoutes.OpenDialog, new JsonObject { ["action"] = "cancel" });
        Answer(ChannelRoutes.SaveNew, new JsonObject { ["action"] = "cancel" });
        Answer(ChannelRoutes.Properties, new JsonObject { ["action"] = "cancel" });
        Answer(ChannelRoutes.NewVersion, new JsonObject());
        Answer(ChannelRoutes.Closed, new JsonObject());
    }

    public HelperFolders Folders { get; }

    public int Port { get; }

    /// <summary>Every call answered so far, in order: the route's name and its body.</summary>
    public IReadOnlyList<(string Route, JsonObject Body)> Calls => [.. _calls];

    /// <summary>The routes called so far, in order.</summary>
    public IReadOnlyList<string> Routes => [.. _calls.Select(c => c.Route)];

    /// <summary>The bodies of the calls to <paramref name="route"/>, in order.</summary>
    public IReadOnlyList<JsonObject> CallsTo(ChannelRoute route) => [.. _calls.Where(c => c.Route == route.Name).Select(c => c.Body)];

    /// <summary>Answers <paramref name="route"/> with what <paramref name="answer"/> gives for each call's body.</summary>
    public void Answer(ChannelRoute route, Func<JsonObject, Task<JsonNode>> answer) => _answers[route.Name] = answer;

    /// <summary>Answers <paramref name="route"/> with a copy of <paramref name="answer"/> each time.</summary>
    public void Answer(ChannelRoute route, JsonNode answer) => Answer(route, _ => Task.FromResult(answer.DeepClone()));

    /// <summary>Answers <paramref name="route"/> with an error, as the Helper's server does: <c>{"error": code}</c>.</summary>
    public void Fail(ChannelRoute route, int status, string code) => Answer(route, _ => throw new Refusal(status, code));

    /// <summary>Puts a copy in <c>Copies/</c>, named as the Helper names them, and answers its state.</summary>
    public string AddCopy(string name, long docNumber, string version, bool readOnly = false, string? holdBy = null, byte[]? pdf = null)
    {
        var file = $"{name} #{docNumber} v{version}{(readOnly ? " (read-only)" : "")}.pdf";
        var path = Path.Combine(Folders.Copies, file);
        File.WriteAllBytes(path, pdf ?? MinimalPdf.Build());
        SetState(new CopyState(true, docNumber, Path.GetFileNameWithoutExtension(file), $"#{docNumber} v{version}", readOnly, path, holdBy, Pending: false));
        return path;
    }

    /// <summary>What the state routes answer for <paramref name="state"/>'s path from now on.</summary>
    public void SetState(CopyState state) => _copies[Key(state.Path)] = state;

    public CopyState StateOf(string path) => _copies.TryGetValue(Key(path), out var state) ? state : CopyState.Unknown(path);

    /// <summary>Stops answering, as a Helper that quit: <c>channel.json</c> goes.</summary>
    public void GoAway() => File.Delete(Folders.ChannelFile);

    /// <summary>Answers again: <c>channel.json</c> names this channel, and this process as the Helper's.</summary>
    public void ComeBack() =>
        File.WriteAllText(Folders.ChannelFile, JsonSerializer.Serialize(new { port = Port, secret = Secret, pid = Environment.ProcessId }));

    /// <summary>A copy's state as the Helper writes it (its ChannelHandler.Json).</summary>
    public static JsonObject Json(CopyState state) => !state.Known
        ? new JsonObject { ["known"] = false }
        : new JsonObject
        {
            ["known"] = true,
            ["docNumber"] = state.DocNumber,
            ["title"] = state.Title,
            ["docRef"] = state.DocRef,
            ["readOnly"] = state.ReadOnly,
            ["path"] = state.Path,
            ["holdBy"] = state.HoldBy,
            ["pending"] = state.Pending,
        };

    public void Dispose()
    {
        _listener.Close();
        try
        {
            Directory.Delete(Folders.Support, recursive: true);
        }
        catch (IOException)
        {
            // A window still closing may hold a file; the folder is a temporary one all the same.
        }
        if (!_unlisted.IsEmpty)
            throw new InvalidOperationException($"CoffeePDF called routes ChannelRoutes doesn’t list: {string.Join(", ", _unlisted)}");
    }

    /// <summary><c>/v1/file</c> as the Helper does it: the temporary file becomes a new document's copy, checked out here, and goes.</summary>
    private Task<JsonNode> FileTemporary(JsonObject body)
    {
        var temp = (string)body["tempPath"]!;
        var path = AddCopy((string)body["name"]!, Interlocked.Increment(ref _lastDocNumber), "1", pdf: File.ReadAllBytes(temp));
        File.Delete(temp);
        return Task.FromResult<JsonNode>(Json(StateOf(path)));
    }

    private static string PathIn(JsonObject body) => (string?)body["path"] ?? "";

    private static string Key(string path) => Path.GetFullPath(path).Normalize(NormalizationForm.FormC);

    private static (HttpListener Listener, int Port) Listen()
    {
        // HttpListener can't bind port 0, so learn a free port and try again if something takes it first, as the Helper does.
        for (var attempt = 1; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
            }
        }
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            _ = Task.Run(() => RespondAsync(context));
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        var (status, answer) = await RouteAsync(context.Request);
        var bytes = Encoding.UTF8.GetBytes(answer.ToJsonString());
        try
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or IOException)
        {
            // The caller gave up waiting, or the fake is stopping.
        }
    }

    private async Task<(int Status, JsonNode Answer)> RouteAsync(HttpListenerRequest request)
    {
        var path = request.Url?.AbsolutePath ?? "";
        if (ChannelRoutes.All.FirstOrDefault(r => r.Path == path) is not { } route)
        {
            _unlisted.Enqueue(path);
            return (404, Error("not-found"));
        }
        if (request.Headers[ChannelRoutes.SecretHeader] != Secret)
            return (403, Error("forbidden"));
        if (request.HttpMethod != "POST")
            return (405, Error("method-not-allowed"));
        JsonObject body;
        try
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            if (JsonNode.Parse(await reader.ReadToEndAsync()) is not JsonObject parsed)
                return (400, Error("bad-request"));
            body = parsed;
        }
        catch (JsonException)
        {
            return (400, Error("bad-request"));
        }
        _calls.Enqueue((route.Name, body));
        try
        {
            return (200, await _answers[route.Name](body));
        }
        catch (Refusal refusal)
        {
            return (refusal.Status, Error(refusal.Code));
        }
        catch (Exception)
        {
            return (500, Error("internal-error")); // as the Helper answers a route that throws
        }
    }

    private static JsonObject Error(string code) => new() { ["error"] = code };

    private sealed class Refusal(int status, string code) : Exception(code)
    {
        public int Status { get; } = status;

        public string Code { get; } = code;
    }
}
