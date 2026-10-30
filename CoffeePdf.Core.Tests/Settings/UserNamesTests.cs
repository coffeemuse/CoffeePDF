using CoffeePdf.Core.Settings;

namespace CoffeePdf.Core.Tests.Settings;

public sealed class UserNamesTests
{
    private static readonly string[] Passwd =
    [
        "root:x:0:0:root:/root:/bin/bash",
        "tester:x:1000:1000:Test Reviewer,Room 1,,:/home/tester:/bin/bash",
        "blank:x:1001:1001::/home/blank:/bin/bash",
    ];

    [Fact]
    public void The_comment_fields_first_part_is_the_full_name() =>
        Assert.Equal("Test Reviewer", UserNames.Gecos(Passwd, "tester"));

    [Fact]
    public void No_comment_or_no_entry_gives_none()
    {
        Assert.Null(UserNames.Gecos(Passwd, "blank"));
        Assert.Null(UserNames.Gecos(Passwd, "nobody-here"));
    }

    [Fact]
    public void There_is_always_a_name()
    {
        // Asserted, never printed: it's the account's own name.
        Assert.False(string.IsNullOrWhiteSpace(UserNames.FullName()));
    }
}
