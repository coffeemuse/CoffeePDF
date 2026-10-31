using System.Globalization;
using CoffeePdf.Core.Logging;

namespace CoffeePdf.Core.Tests.Logging;

/// <summary>Spec §6.9: the log, which never stops the app.</summary>
public sealed class CoffeeLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coffeepdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Each_event_is_a_line_with_its_level()
    {
        var log = new CoffeeLog(Path.Combine(_dir, "not-yet"));

        log.Info("/v1/state answered");
        log.Warn("/v1/file answered 500 (internal-error)");

        var lines = File.ReadAllLines(log.FilePath);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith(" INFO /v1/state answered", lines[0]);
        Assert.EndsWith(" WARN /v1/file answered 500 (internal-error)", lines[1]);
    }

    [Fact]
    public void The_time_is_written_the_same_way_in_every_region()
    {
        var log = new CoffeeLog(_dir);
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("th-TH"); // whose calendar's years are 543 ahead of the Gregorian
        try
        {
            log.Info("/v1/state answered");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        var line = Assert.Single(File.ReadAllLines(log.FilePath));
        var time = DateTimeOffset.ParseExact(line[..30], "yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        Assert.InRange(DateTimeOffset.Now - time, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void A_log_that_cant_be_written_is_dropped()
    {
        var blocked = Path.Combine(_dir, "blocked");
        File.WriteAllText(blocked, "a file where the folder would be");

        new CoffeeLog(blocked).Info("nothing to see");

        Assert.Equal("a file where the folder would be", File.ReadAllText(blocked));
    }
}
