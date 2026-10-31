using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Tests.Documents;

/// <summary>Spec §6.4: following a save, as the LibreOffice extension does.</summary>
public sealed class SaveFollowerTests
{
    private static readonly CopyState Pending = new(true, 1234, "Settlement Agreement #1234 v2.1", "#1234 v2.1", false, "/c.pdf", null, Pending: true);
    private static readonly CopyState Done = Pending with { Title = "Settlement Agreement #1234 v2.2", DocRef = "#1234 v2.2", Pending = false };

    private static Func<Task<CopyState>> Answer(CopyState state) => () => Task.FromResult(state);

    private static Func<Task<CopyState>> Away() => () => Task.FromException<CopyState>(new HelperUnreachableException("away"));

    /// <summary>Follows with <paramref name="turns"/> answering in order, and pauses that pass at once but are counted.</summary>
    private static async Task<(List<TimeSpan> Pauses, List<CopyState> Answers, int Unreachable, int Asked)> FollowAsync(
        IEnumerable<Func<Task<CopyState>>> turns, CancellationTokenSource? stop = null, int stopAtPause = 0)
    {
        var queue = new Queue<Func<Task<CopyState>>>(turns);
        var pauses = new List<TimeSpan>();
        var answers = new List<CopyState>();
        var unreachable = 0;
        var asked = 0;
        await SaveFollower.FollowAsync(
            _ =>
            {
                asked++;
                return queue.Dequeue()();
            },
            answers.Add,
            () => unreachable++,
            (pause, token) =>
            {
                pauses.Add(pause);
                if (pauses.Count == stopAtPause)
                    stop!.Cancel(); // the window closed during this pause
                return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
            },
            stop?.Token ?? CancellationToken.None);
        return (pauses, answers, unreachable, asked);
    }

    [Fact]
    public void The_pauses_are_2_5_15_30_then_60_seconds_for_ever()
    {
        Assert.Equal(
            new double[] { 2, 5, 15, 30, 60, 60, 60 },
            Enumerable.Range(1, 7).Select(round => SaveFollower.PauseBefore(round).TotalSeconds));
    }

    [Fact]
    public async Task An_answer_thats_not_pending_ends_it_at_once()
    {
        var (pauses, answers, _, asked) = await FollowAsync([Answer(Done)]);

        Assert.Empty(pauses);
        Assert.Equal(new[] { Done }, answers);
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task While_the_answer_is_pending_it_asks_again_after_each_pause()
    {
        var (pauses, answers, _, _) = await FollowAsync(Enumerable.Repeat(Answer(Pending), 7).Append(Answer(Done)));

        Assert.Equal(new double[] { 2, 5, 15, 30, 60, 60, 60 }, pauses.Select(p => p.TotalSeconds));
        Assert.Equal(8, answers.Count);
        Assert.Equal(Done, answers[^1]);
    }

    [Fact]
    public async Task While_the_Helper_cant_be_reached_it_keeps_asking_the_same_way()
    {
        var (pauses, answers, unreachable, _) = await FollowAsync([Away(), Away(), Away(), Answer(Done)]);

        Assert.Equal(3, unreachable);
        Assert.Equal(new double[] { 2, 5, 15 }, pauses.Select(p => p.TotalSeconds));
        Assert.Equal(new[] { Done }, answers);
    }

    [Fact]
    public async Task Stopping_it_ends_it_without_another_question()
    {
        using var stop = new CancellationTokenSource();

        var (pauses, answers, _, asked) = await FollowAsync([Answer(Pending), Answer(Done)], stop, stopAtPause: 1);

        Assert.Single(pauses);
        Assert.Equal(new[] { Pending }, answers);
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task An_answer_that_comes_after_its_stopped_isnt_taken()
    {
        using var stop = new CancellationTokenSource();
        var answers = new List<CopyState>();

        await SaveFollower.FollowAsync(_ =>
        {
            stop.Cancel(); // a newer save took over while this one was asked
            return Task.FromResult(Done);
        }, answers.Add, () => { }, (_, _) => Task.CompletedTask, stop.Token);

        Assert.Empty(answers);
    }

    [Fact]
    public async Task An_error_answer_ends_it()
    {
        var (pauses, answers, _, asked) = await FollowAsync([() => Task.FromException<CopyState>(new HelperErrorException(500, "internal-error")), Answer(Done)]);

        Assert.Empty(pauses);
        Assert.Empty(answers);
        Assert.Equal(1, asked);
    }
}
