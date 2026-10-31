using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Helper;

public sealed class ChannelInfoTests : IDisposable
{
    private const string Secret = "0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string contents)
    {
        var path = Path.Combine(_dir, "channel.json");
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void It_reads_the_port_the_secret_and_the_process() =>
        Assert.Equal(new ChannelInfo(52100, Secret, 4321), ChannelInfo.Read(Write($$"""{"port": 52100, "secret": "{{Secret}}", "pid": 4321}""")));

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{"port": 0, "secret": "0123456789abcdef0123456789abcdef0123456789abcdef", "pid": 1}""")]
    [InlineData("""{"port": "52100", "secret": "0123456789abcdef0123456789abcdef0123456789abcdef", "pid": 1}""")]
    [InlineData("""{"port": 52100, "secret": "short", "pid": 1}""")]
    [InlineData("""{"port": 52100, "secret": "0123456789abcdef0123456789abcdef0123456789abcdeg", "pid": 1}""")]
    [InlineData("""{"port": 52100, "secret": "0123456789abcdef0123456789abcdef0123456789abcdef", "pid": 0}""")]
    [InlineData("""{"port": 52100, "secret": "0123456789abcdef0123456789abcdef0123456789abcdef"}""")]
    public void Anything_but_the_Helpers_shape_is_no_channel(string contents) => Assert.Null(ChannelInfo.Read(Write(contents)));

    [Fact]
    public void A_missing_file_is_no_channel() => Assert.Null(ChannelInfo.Read(Path.Combine(_dir, "channel.json")));

    [Fact]
    public void Printing_it_leaves_the_secret_out() => Assert.DoesNotContain(Secret, new ChannelInfo(52100, Secret, 4321).ToString());
}
