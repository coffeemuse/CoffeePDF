using CoffeePdf.Core.Helper;

namespace CoffeePdf.Core.Documents;

/// <summary>
/// Follows a copy's save to Healdan (spec §6.4), as the LibreOffice extension does. It asks <c>/v1/saved</c>, which
/// answers once Healdan has the save, or after 120 seconds with the save still <c>pending</c>. While it's pending, or
/// the Helper can't be reached, it asks again after 2, 5 and 15 seconds, then 30, then every 60.
/// </summary>
public static class SaveFollower
{
    public static readonly IReadOnlyList<TimeSpan> Pauses =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    /// <summary>The pause before round <paramref name="round"/>, counting the first question as round 0.</summary>
    public static TimeSpan PauseBefore(int round) => Pauses[Math.Min(round, Pauses.Count) - 1];

    /// <summary>
    /// Asks <paramref name="saved"/> until an answer isn't pending. Each answer goes to <paramref name="answered"/>, and
    /// each time the Helper can't be reached, <paramref name="unreachable"/> is called. It ends quietly when
    /// <paramref name="stop"/> is cancelled (the window closed, a newer save took over, or the file was saved
    /// elsewhere), or at an error answer, which leaves the title as it is, as in the extension. It keeps the caller's
    /// context, so the callbacks run on the window's thread.
    /// </summary>
    public static async Task FollowAsync(Func<CancellationToken, Task<CopyState>> saved, Action<CopyState> answered, Action unreachable,
        Func<TimeSpan, CancellationToken, Task> pause, CancellationToken stop)
    {
        try
        {
            for (var round = 0; !stop.IsCancellationRequested; round++)
            {
                if (round > 0)
                    await pause(PauseBefore(round), stop);
                CopyState state;
                try
                {
                    state = await saved(stop);
                }
                catch (HelperUnreachableException)
                {
                    if (!stop.IsCancellationRequested)
                        unreachable();
                    continue;
                }
                if (stop.IsCancellationRequested)
                    return;
                answered(state);
                if (!state.Pending)
                    return;
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Stopped during a pause or a question.
        }
        catch (HelperErrorException)
        {
            // The Helper answered the question itself with an error: there's nothing more to follow.
        }
    }
}
