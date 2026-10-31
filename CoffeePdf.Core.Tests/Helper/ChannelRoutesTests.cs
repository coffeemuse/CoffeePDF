using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

/// <summary>Spec §9: the routes CoffeePDF calls, as the Desktop Clients page's table has them.</summary>
public sealed class ChannelRoutesTests
{
    [Fact]
    public void The_routes_are_the_Desktop_Clients_pages_but_link_and_ping() =>
        // /v1/link is for a second Helper, and CoffeePDF needs no /v1/ping: /v1/state tells it the Helper answers.
        Assert.Equal(
            new[] { "open-dialog", "save-new", "file", "adopted", "saved", "new-version", "properties", "closed", "state" },
            ChannelRoutes.All.Select(r => r.Name));

    [Fact]
    public void Each_route_is_under_v1() => Assert.All(ChannelRoutes.All, r => Assert.Equal("/v1/" + r.Name, r.Path));

    [Fact]
    public void Only_the_window_routes_wait_for_the_user() =>
        Assert.Equal(
            new[] { ChannelRoutes.OpenDialog, ChannelRoutes.SaveNew, ChannelRoutes.Properties },
            ChannelRoutes.All.Where(r => r.Wait == ChannelWait.Window));

    [Fact]
    public void A_state_says_known_or_not()
    {
        var path = "/support/Copies/Settlement Agreement #1234 v2.1.pdf";
        var known = System.Text.Json.Nodes.JsonNode.Parse($$"""
            {"known": true, "docNumber": 1234, "title": "Settlement Agreement #1234 v2.1", "docRef": "#1234 v2.1",
             "readOnly": false, "path": "{{path}}", "holdBy": null, "pending": true}
            """)!.AsObject();

        Assert.Equal(new CopyState(true, 1234, "Settlement Agreement #1234 v2.1", "#1234 v2.1", false, path, null, true), CopyState.From(known, path));
        Assert.Equal(CopyState.Unknown(path), CopyState.From(new() { ["known"] = false }, path));
        Assert.Equal(CopyState.Unknown(path), CopyState.From(new(), path));
    }

    [Fact]
    public void The_Save_windows_answer_keeps_its_values()
    {
        var answer = System.Text.Json.Nodes.JsonNode.Parse("""
            {"action": "save", "folderId": 42, "name": "Report", "format": "pdf", "properties": {"Client": "Acme"},
             "tempPath": "/support/Temp/save.pdf"}
            """)!.AsObject();

        var read = SaveNewAnswer.From(answer);

        Assert.Equal(SaveNewAction.Save, read.Action);
        Assert.Equal("/support/Temp/save.pdf", read.TempPath);
        Assert.Equal("pdf", read.Format);
        Assert.Same(answer, read.Raw);
        Assert.Equal(SaveNewAction.Cancel, SaveNewAnswer.From(new() { ["action"] = "something new" }).Action);
    }
}
