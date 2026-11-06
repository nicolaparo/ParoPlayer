using ParoPlayer.Core.Models;
using ParoPlayer.Core.Playback;

namespace ParoPlayer.Core.Tests;

public class PlayQueueTests
{
    private static List<Track> Tracks(int count) =>
        [.. Enumerable.Range(1, count).Select(n => new Track { Title = $"Song {n}", DurationSeconds = 100 })];

    private static PlayQueue Queue(int count, int start = 0, int seed = 7) => new(Tracks(count), start, new Random(seed));

    [Fact]
    public void StartsOnTheRequestedTrack()
    {
        var queue = Queue(5, start: 3);

        Assert.Equal(3, queue.Position);
        Assert.Equal("Song 4", queue.Current.Title);
    }

    [Fact]
    public void RejectsAnEmptyQueueAndABadStartIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayQueue([], 0, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayQueue(Tracks(2), 2, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayQueue(Tracks(2), -1, new Random(1)));
    }

    [Fact]
    public void NextWalksTheQueueAndStopsAtTheEnd()
    {
        var queue = Queue(3);

        Assert.True(queue.MoveNext());
        Assert.True(queue.MoveNext());
        Assert.False(queue.MoveNext());
        Assert.Equal(2, queue.Position);
        Assert.False(queue.HasNext);
    }

    [Fact]
    public void NextWrapsToTheFirstTrackWithRepeatAll()
    {
        var queue = Queue(3, start: 2);
        queue.SetRepeat(RepeatMode.All);

        Assert.True(queue.HasNext);
        Assert.True(queue.MoveNext());
        Assert.Equal(0, queue.Position);
    }

    [Fact]
    public void PreviousGoesBackWhenLessThanThreeSecondsPlayed()
    {
        var queue = Queue(3, start: 2);

        Assert.True(queue.MovePrevious(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, queue.Position);
    }

    [Fact]
    public void PreviousRestartsTheTrackAfterThreeSeconds()
    {
        var queue = Queue(3, start: 2);

        Assert.False(queue.MovePrevious(TimeSpan.FromSeconds(3.5)));
        Assert.Equal(2, queue.Position);
    }

    [Fact]
    public void PreviousOnTheFirstTrackRestartsUnlessRepeatAll()
    {
        var queue = Queue(3);

        Assert.False(queue.MovePrevious(TimeSpan.Zero));
        Assert.Equal(0, queue.Position);

        queue.SetRepeat(RepeatMode.All);
        Assert.True(queue.MovePrevious(TimeSpan.Zero));
        Assert.Equal(2, queue.Position);
    }

    [Fact]
    public void FinishedTrackAdvancesAndEndsTheQueueWithoutRepeat()
    {
        var queue = Queue(2);

        Assert.True(queue.Advance());
        Assert.Equal(1, queue.Position);
        Assert.False(queue.Advance());
        Assert.Equal(1, queue.Position);
    }

    [Fact]
    public void FinishedTrackStaysWithRepeatOneButNextStillSkips()
    {
        var queue = Queue(3);
        queue.SetRepeat(RepeatMode.One);

        Assert.True(queue.Advance());
        Assert.Equal(0, queue.Position);

        Assert.True(queue.MoveNext());
        Assert.Equal(1, queue.Position);
    }

    [Fact]
    public void RepeatAllWithOneTrackKeepsPlayingIt()
    {
        var queue = Queue(1);
        queue.SetRepeat(RepeatMode.All);

        Assert.True(queue.Advance());
        Assert.Equal(0, queue.Position);
    }

    [Fact]
    public void ShuffleKeepsTheCurrentTrackFirstAndEveryTrackExactlyOnce()
    {
        var queue = Queue(20, start: 5);
        var current = queue.Current;

        queue.SetShuffle(true);

        Assert.True(queue.Shuffle);
        Assert.Equal(0, queue.Position);
        Assert.Same(current, queue.Order[0]);
        Assert.Equal(20, queue.Order.Count);
        Assert.Equal(queue.Original.OrderBy(t => t.Title), queue.Order.OrderBy(t => t.Title));
    }

    [Fact]
    public void ShuffleActuallyChangesTheOrderAndIsDeterministicForASeed()
    {
        var first = Queue(20, seed: 42);
        var second = Queue(20, seed: 42);

        first.SetShuffle(true);
        second.SetShuffle(true);

        Assert.NotEqual(first.Original.Select(t => t.Id), first.Order.Select(t => t.Id));
        Assert.Equal(first.Order.Select(t => t.Title), second.Order.Select(t => t.Title));
    }

    [Fact]
    public void ShuffleOffRestoresTheQueuedOrderAndKeepsTheCurrentTrack()
    {
        var queue = Queue(10);
        queue.SetShuffle(true);
        queue.MoveNext();
        queue.MoveNext();
        var current = queue.Current;

        queue.SetShuffle(false);

        Assert.False(queue.Shuffle);
        Assert.Equal(queue.Original, queue.Order);
        Assert.Same(current, queue.Current);
    }

    [Fact]
    public void ShuffleNextVisitsEveryTrackOnceBeforeTheQueueEnds()
    {
        var queue = Queue(8);
        queue.SetShuffle(true);
        var seen = new HashSet<Guid> { queue.Current.Id };

        while (queue.MoveNext())
        {
            seen.Add(queue.Current.Id);
        }

        Assert.Equal(8, seen.Count);
    }

    [Fact]
    public void MoveToFollowsThePlatformPlayer()
    {
        var queue = Queue(5);

        queue.MoveTo(4);

        Assert.Equal("Song 5", queue.Current.Title);
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.MoveTo(5));
    }
}
