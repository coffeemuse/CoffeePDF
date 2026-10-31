using System.Net;
using System.Text;
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Fakes;

/// <summary>Spec §9: the fake Helper serves exactly ChannelRoutes, and fails a test that calls anything else.</summary>
public sealed class FakeHelperTests
{
    private static async Task<HttpStatusCode> PostAsync(FakeHelper helper, string path, string? secret = FakeHelper.Secret)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{helper.Port}{path}")
        {
            Content = new StringContent("""{"path": "/nowhere.pdf"}""", Encoding.UTF8, "application/json"),
        };
        if (secret is not null)
            request.Headers.Add(ChannelRoutes.SecretHeader, secret);
        using var response = await http.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task It_serves_each_route_ChannelRoutes_lists()
    {
        using var helper = new FakeHelper();
        helper.Answer(ChannelRoutes.File, new System.Text.Json.Nodes.JsonObject { ["known"] = false }); // there's no file to file

        foreach (var route in ChannelRoutes.All)
            Assert.Equal(HttpStatusCode.OK, await PostAsync(helper, route.Path));
        Assert.Equal(ChannelRoutes.All.Select(r => r.Name), helper.Routes);
    }

    [Fact]
    public async Task Without_the_secret_it_refuses_as_the_Helper_does()
    {
        using var helper = new FakeHelper();

        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(helper, ChannelRoutes.State.Path, secret: null));
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(helper, ChannelRoutes.State.Path, secret: new string('0', 48)));
        Assert.Empty(helper.Calls);
    }

    [Fact]
    public async Task A_route_ChannelRoutes_doesnt_list_fails_the_test()
    {
        var helper = new FakeHelper();

        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(helper, "/v1/link"));

        Assert.Contains("/v1/link", Assert.Throws<InvalidOperationException>(helper.Dispose).Message);
    }

    [Fact]
    public void A_copy_is_named_as_the_Helper_names_it_and_answers_its_state()
    {
        using var helper = new FakeHelper();

        var path = helper.AddCopy("Settlement Agreement", 1234, "2.1", readOnly: true, holdBy: "Zoë Q. Reviewer");

        Assert.Equal(Path.Combine(helper.Folders.Copies, "Settlement Agreement #1234 v2.1 (read-only).pdf"), path);
        Assert.True(File.Exists(path));
        Assert.Equal(new CopyState(true, 1234, "Settlement Agreement #1234 v2.1 (read-only)", "#1234 v2.1", true, path, "Zoë Q. Reviewer", false),
            helper.StateOf(path));
    }

    [Fact]
    public void Going_away_takes_channel_json_and_coming_back_writes_it()
    {
        using var helper = new FakeHelper();

        helper.GoAway();
        Assert.Null(ChannelInfo.Read(helper.Folders.ChannelFile));
        helper.ComeBack();

        Assert.Equal(new ChannelInfo(helper.Port, FakeHelper.Secret, Environment.ProcessId), ChannelInfo.Read(helper.Folders.ChannelFile));
    }
}
