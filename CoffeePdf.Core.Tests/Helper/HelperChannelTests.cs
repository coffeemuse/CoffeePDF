using System.Text.Json.Nodes;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Tests.Fakes;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>Spec §4.3 and §9: the channel client, against the fake Helper.</summary>
public sealed class HelperChannelTests : IDisposable
{
    private readonly FakeHelper _helper = new();

    public void Dispose() => _helper.Dispose();

    private HelperChannel Channel(IReadOnlyDictionary<ChannelWait, TimeSpan>? waits = null) => _helper.Link(waits: waits).Channel;

    [Fact]
    public async Task State_sends_the_path_and_reads_the_copys_state()
    {
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");

        var state = await Channel().StateAsync(copy);

        Assert.Equal(_helper.StateOf(copy), state);
        Assert.Equal(copy, (string?)Assert.Single(_helper.CallsTo(ChannelRoutes.State))["path"]);
    }

    [Fact]
    public async Task A_path_the_Helper_doesnt_know_is_unknown() =>
        Assert.False((await Channel().StateAsync("/docs/Report.pdf")).Known);

    [Fact]
    public async Task The_window_routes_name_CoffeePDF_and_ask_for_a_pdf()
    {
        var channel = Channel();

        await channel.OpenDialogAsync();
        await channel.SaveNewAsync("Report", copy: null);
        await channel.SaveNewAsync("Settlement Agreement", copy: "/support/Copies/Settlement Agreement #1234 v2.1.pdf");

        Assert.Equal("""{"app":"CoffeePDF"}""", Assert.Single(_helper.CallsTo(ChannelRoutes.OpenDialog)).ToJsonString());
        Assert.Equal(
            new[]
            {
                """{"app":"CoffeePDF","name":"Report","formats":["pdf"],"path":null}""",
                """{"app":"CoffeePDF","name":"Settlement Agreement","formats":["pdf"],"path":"/support/Copies/Settlement Agreement #1234 v2.1.pdf"}""",
            },
            _helper.CallsTo(ChannelRoutes.SaveNew).Select(b => b.ToJsonString()));
    }

    [Theory]
    [InlineData("opened", OpenDialogAction.Opened)]
    [InlineData("local", OpenDialogAction.Local)]
    [InlineData("cancel", OpenDialogAction.Cancel)]
    [InlineData("something new", OpenDialogAction.Cancel)]
    public async Task The_Open_windows_answer_is_read(string answer, OpenDialogAction expected)
    {
        _helper.Answer(ChannelRoutes.OpenDialog, new JsonObject { ["action"] = answer });

        Assert.Equal(expected, await Channel().OpenDialogAsync());
    }

    [Theory]
    [InlineData("saved", PropertiesAction.Saved)]
    [InlineData("file", PropertiesAction.File)]
    [InlineData("cancel", PropertiesAction.Cancel)]
    public async Task The_Properties_windows_answer_is_read(string answer, PropertiesAction expected)
    {
        _helper.Answer(ChannelRoutes.Properties, new JsonObject { ["action"] = answer });

        Assert.Equal(expected, await Channel().PropertiesAsync("/docs/Report.pdf"));
    }

    [Fact]
    public async Task Filing_sends_the_Save_windows_values_back_unchanged()
    {
        var temp = Path.Combine(_helper.Folders.Temp, "save.pdf");
        File.WriteAllBytes(temp, CoffeePdf.Tests.Fixtures.MinimalPdf.Build());
        var answer = SaveNewAnswer.From(JsonNode.Parse($$"""
            {"action": "save", "folderId": 42, "name": "Report", "format": "pdf", "properties": {"Client": "Acme", "Year": 2026},
             "tempPath": "{{temp}}"}
            """)!.AsObject());

        var filed = await Channel().FileAsync(answer, replaces: "/support/Copies/Old #7 v1.pdf");

        var body = Assert.Single(_helper.CallsTo(ChannelRoutes.File));
        Assert.Equal(temp, (string?)body["tempPath"]);
        Assert.Equal(42, (int)body["folderId"]!);
        Assert.Equal("Report", (string?)body["name"]);
        Assert.Equal("pdf", (string?)body["format"]);
        Assert.Equal("""{"Client":"Acme","Year":2026}""", body["properties"]!.ToJsonString());
        Assert.Equal("CoffeePDF", (string?)body["app"]);
        Assert.Equal("/support/Copies/Old #7 v1.pdf", (string?)body["replaces"]);
        Assert.Equal(Path.Combine(_helper.Folders.Copies, "Report #1 v1.pdf"), filed.Path);
    }

    [Fact]
    public async Task Without_channel_json_the_Helper_cant_be_reached()
    {
        _helper.GoAway();

        await Assert.ThrowsAsync<HelperUnreachableException>(() => Channel().StateAsync("/docs/Report.pdf"));
    }

    [Fact]
    public async Task A_channel_json_whose_process_is_gone_cant_be_reached()
    {
        // The largest pid there is: no process has it.
        File.WriteAllText(_helper.Folders.ChannelFile, $$"""{"port": {{_helper.Port}}, "secret": "{{FakeHelper.Secret}}", "pid": 2147483647}""");

        await Assert.ThrowsAsync<HelperUnreachableException>(() => Channel().StateAsync("/docs/Report.pdf"));
        Assert.Empty(_helper.Calls);
    }

    [Fact]
    public async Task A_channel_nothing_listens_on_cant_be_reached()
    {
        var other = new FakeHelper();
        var port = other.Port;
        other.Dispose(); // its port is free again, and nothing listens on it
        File.WriteAllText(_helper.Folders.ChannelFile, $$"""{"port": {{port}}, "secret": "{{FakeHelper.Secret}}", "pid": {{Environment.ProcessId}}}""");

        await Assert.ThrowsAsync<HelperUnreachableException>(() => Channel().StateAsync("/docs/Report.pdf"));
    }

    [Fact]
    public async Task An_error_answer_carries_its_status_and_code()
    {
        _helper.Fail(ChannelRoutes.State, 500, "internal-error");

        var error = await Assert.ThrowsAsync<HelperErrorException>(() => Channel().StateAsync("/docs/Report.pdf"));

        Assert.Equal(500, error.Status);
        Assert.Equal("internal-error", error.Code);
        Assert.Equal("Healdan Helper answered 500 (internal-error)", error.Message);
    }

    [Fact]
    public async Task A_quick_route_gives_up_at_its_limit_but_a_window_route_waits_for_the_user()
    {
        var user = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _helper.Answer(ChannelRoutes.State, async _ => { await user.Task; return new JsonObject { ["known"] = false }; });
        _helper.Answer(ChannelRoutes.OpenDialog, async _ => { await user.Task; return new JsonObject { ["action"] = "local" }; });
        // A short limit for the quick routes, so the test sees one run out; the window routes keep none.
        var channel = Channel(new Dictionary<ChannelWait, TimeSpan>(HelperChannel.DefaultWaits) { [ChannelWait.Quick] = TimeSpan.FromMilliseconds(200) });

        var open = channel.OpenDialogAsync();
        await Assert.ThrowsAsync<HelperUnreachableException>(() => channel.StateAsync("/docs/Report.pdf"));
        var waitedPastIt = !open.IsCompleted;
        user.SetResult();

        Assert.True(waitedPastIt);
        Assert.Equal(OpenDialogAction.Local, await open.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(Timeout.InfiniteTimeSpan, HelperChannel.DefaultWaits[ChannelWait.Window]);
    }

    [Fact]
    public async Task Cancelling_a_call_cancels_it_rather_than_calling_the_Helper_away()
    {
        _helper.Answer(ChannelRoutes.Saved, async _ => { await new TaskCompletionSource().Task; return new JsonObject(); }); // never answers
        using var stop = new CancellationTokenSource();

        var saved = Channel().SavedAsync("/docs/Report.pdf", stop.Token);
        stop.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => saved);
    }

    [Fact]
    public async Task Nothing_logged_holds_the_secret()
    {
        var link = _helper.Link();
        var copy = _helper.AddCopy("Settlement Agreement", 1234, "2.1");
        await link.Channel.StateAsync(copy);
        _helper.Fail(ChannelRoutes.Closed, 500, "internal-error");
        await Assert.ThrowsAsync<HelperErrorException>(() => link.Channel.ClosedAsync(copy));
        _helper.GoAway();
        var unreachable = await Assert.ThrowsAsync<HelperUnreachableException>(() => link.Channel.StateAsync(copy));

        var log = File.ReadAllText(link.Log.FilePath);
        Assert.Contains("/v1/state", log);
        Assert.DoesNotContain(FakeHelper.Secret, log);
        Assert.DoesNotContain(FakeHelper.Secret, unreachable.Message);
    }

    [Fact]
    public void The_link_reads_Healdan_support_from_its_settings()
    {
        var link = _helper.Link(enabled: false);
        Assert.False(link.Enabled);

        link.Settings.HealdanEnabled = true;

        Assert.True(link.Enabled);
        Assert.Same(_helper.Folders, link.Folders);
    }
}
