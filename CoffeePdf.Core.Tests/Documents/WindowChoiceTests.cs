using CoffeePdf.Core.Documents;

namespace CoffeePdf.Core.Tests.Documents;

public sealed class WindowChoiceTests
{
    [Fact]
    public void With_no_windows_a_new_one_opens() =>
        Assert.Equal(new WindowChoice(WindowAction.OpenNew, -1), WindowChoice.For([], "/docs/a.pdf"));

    [Fact]
    public void The_window_showing_the_file_comes_forward() =>
        Assert.Equal(new WindowChoice(WindowAction.Activate, 1), WindowChoice.For(["/docs/b.pdf", "/docs/a.pdf"], "/docs/a.pdf"));

    [Fact]
    public void Another_spelling_of_the_same_path_is_the_same_file() =>
        Assert.Equal(new WindowChoice(WindowAction.Activate, 0), WindowChoice.For(["/docs/a.pdf"], "/docs/sub/../a.pdf"));

    [Fact]
    public void An_empty_window_takes_the_file() =>
        Assert.Equal(new WindowChoice(WindowAction.LoadInto, 1), WindowChoice.For(["/docs/b.pdf", null], "/docs/a.pdf"));

    [Fact]
    public void The_window_showing_the_file_wins_over_an_empty_one() =>
        Assert.Equal(new WindowChoice(WindowAction.Activate, 1), WindowChoice.For([null, "/docs/a.pdf"], "/docs/a.pdf"));

    [Fact]
    public void Windows_showing_other_files_are_left_alone() =>
        Assert.Equal(new WindowChoice(WindowAction.OpenNew, -1), WindowChoice.For(["/docs/b.pdf", "/docs/c.pdf"], "/docs/a.pdf"));
}
