using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Playback;

namespace ParoPlayer.Core.Tests;

public class SilentPlayerServiceTests
{
    private readonly FakeTimeProvider time = new();

    private SilentPlayerService Create() => new(time, new Random(3));

    private static List<Track> Tracks(int count, int seconds = 10) =>
        [.. Enumerable.Range(1, count).Select(n => new Track { Title = $"Song {n}", DurationSeconds = seconds })];

    [Fact]
    public async Task StartsEmpty()
    {
        await using var player = Create();

        Assert.Equal(PlayerSnapshot.Empty, await player.GetSnapshotAsync());
    }

    [Fact]
    public async Task PlayStartsTheRequestedTrackFromZero()
    {
        await using var player = Create();

        await player.PlayAsync(Tracks(3), 1);
        var snapshot = await player.GetSnapshotAsync();

        Assert.Equal("Song 2", snapshot.Current?.Title);
        Assert.True(snapshot.IsPlaying);
        Assert.Equal(TimeSpan.Zero, snapshot.Position);
        Assert.Equal(TimeSpan.FromSeconds(10), snapshot.Duration);
        Assert.Equal(3, snapshot.QueueCount);
    }

    [Fact]
    public async Task PositionFollowsTheClockOnlyWhilePlaying()
    {
        await using var player = Create();
        await player.PlayAsync(Tracks(1, seconds: 60));

        time.Advance(TimeSpan.FromSeconds(5));
        await player.PauseAsync();
        time.Advance(TimeSpan.FromSeconds(20));
        var paused = await player.GetSnapshotAsync();
        await player.ResumeAsync();
        time.Advance(TimeSpan.FromSeconds(2));
        var resumed = await player.GetSnapshotAsync();

        Assert.False(paused.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(5), paused.Position);
        Assert.Equal(TimeSpan.FromSeconds(7), resumed.Position);
    }

    [Fact]
    public async Task SeekMovesThePositionAndIsClampedToTheDuration()
    {
        await using var player = Create();
        await player.PlayAsync(Tracks(1, seconds: 60));

        await player.SeekToAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(30), (await player.GetSnapshotAsync()).Position);

        await player.PauseAsync();
        await player.SeekToAsync(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromSeconds(60), (await player.GetSnapshotAsync()).Position);
    }

    [Fact]
    public async Task TrackAdvancesByItselfAndTheQueueEndsPaused()
    {
        await using var player = Create();
        await player.PlayAsync(Tracks(2, seconds: 10));

        time.Advance(TimeSpan.FromSeconds(12));
        var second = await player.GetSnapshotAsync();
        time.Advance(TimeSpan.FromSeconds(20));
        var ended = await player.GetSnapshotAsync();

        Assert.Equal("Song 2", second.Current?.Title);
        Assert.Equal(TimeSpan.FromSeconds(2), second.Position);
        Assert.False(ended.IsPlaying);
        Assert.Equal("Song 2", ended.Current?.Title);
    }

    [Fact]
    public async Task RepeatOneKeepsLoopingTheSameTrack()
    {
        await using var player = Create();
        await player.PlayAsync(Tracks(2, seconds: 10));
        await player.SetRepeatAsync(RepeatMode.One);

        time.Advance(TimeSpan.FromSeconds(25));
        var snapshot = await player.GetSnapshotAsync();

        Assert.Equal("Song 1", snapshot.Current?.Title);
        Assert.Equal(TimeSpan.FromSeconds(5), snapshot.Position);
        Assert.True(snapshot.IsPlaying);
    }

    [Fact]
    public async Task NextAndPreviousFollowTheQueueRules()
    {
        await using var player = Create();
        await player.PlayAsync(Tracks(3, seconds: 60));

        await player.NextAsync();
        Assert.Equal("Song 2", (await player.GetSnapshotAsync()).Current?.Title);

        time.Advance(TimeSpan.FromSeconds(10));
        await player.PreviousAsync();
        var restarted = await player.GetSnapshotAsync();
        Assert.Equal("Song 2", restarted.Current?.Title);
        Assert.Equal(TimeSpan.Zero, restarted.Position);

        await player.PreviousAsync();
        Assert.Equal("Song 1", (await player.GetSnapshotAsync()).Current?.Title);
    }

    [Fact]
    public async Task ShuffleKeepsTheCurrentTrackAndIsReported()
    {
        await using var player = Create();
        await player.PlayAsync(Tracks(10), 4);

        await player.SetShuffleAsync(true);
        var snapshot = await player.GetSnapshotAsync();

        Assert.True(snapshot.Shuffle);
        Assert.Equal("Song 5", snapshot.Current?.Title);
        Assert.Equal(0, snapshot.QueueIndex);
    }

    [Fact]
    public async Task SubscribersAreAwaitedWhenTheStateChanges()
    {
        await using var player = Create();
        var seen = new List<PlayerSnapshot>();
        using var subscription = player.Subscribe(snapshot =>
        {
            seen.Add(snapshot);
            return Task.CompletedTask;
        });

        await player.PlayAsync(Tracks(1, seconds: 60));
        await player.PauseAsync();
        await player.PauseAsync();

        Assert.Equal(2, seen.Count);
        Assert.True(seen[0].IsPlaying);
        Assert.False(seen[1].IsPlaying);
    }

    [Fact]
    public async Task TheTickerPublishesPositionWhilePlayingAndStopsAfterUnsubscribe()
    {
        await using var player = Create();
        var positions = new List<TimeSpan>();
        var subscription = player.Subscribe(snapshot =>
        {
            positions.Add(snapshot.Position);
            return Task.CompletedTask;
        });
        await player.PlayAsync(Tracks(1, seconds: 60));

        for (var i = 0; i < 3; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            await WaitForCountAsync(positions, i + 2);
        }

        subscription.Dispose();
        var countAfterUnsubscribe = positions.Count;
        time.Advance(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.Equal(TimeSpan.FromSeconds(1.5), positions[^1]);
        Assert.Equal(countAfterUnsubscribe, positions.Count);
    }

    private static async Task WaitForCountAsync(List<TimeSpan> list, int count)
    {
        for (var i = 0; i < 200 && list.Count < count; i++)
        {
            await Task.Delay(10);
        }

        Assert.True(list.Count >= count, $"expected {count} ticks, got {list.Count}");
    }

    [Fact]
    public async Task DeletingThePlayingTrackSkipsToTheNextAndKeepsPlaying()
    {
        await using var player = Create();
        var tracks = Tracks(3);
        await player.PlayAsync(tracks, 0);

        await player.RemoveTracksAsync([tracks[0].Id]);
        var snapshot = await player.GetSnapshotAsync();

        Assert.Equal("Song 2", snapshot.Current?.Title);
        Assert.True(snapshot.IsPlaying);
        Assert.Equal(2, snapshot.QueueCount);
    }

    [Fact]
    public async Task DeletingTheLastPlayingTrackStopsCleanly()
    {
        await using var player = Create();
        var tracks = Tracks(2);
        await player.PlayAsync(tracks, 1);

        await player.RemoveTracksAsync([tracks[1].Id]);
        var snapshot = await player.GetSnapshotAsync();

        Assert.False(snapshot.IsPlaying);
        Assert.Equal(1, snapshot.QueueCount);
    }

    [Fact]
    public async Task DeletingEveryQueuedTrackEmptiesThePlayer()
    {
        await using var player = Create();
        var tracks = Tracks(2);
        await player.PlayAsync(tracks, 0);

        await player.RemoveTracksAsync([.. tracks.Select(track => track.Id)]);

        Assert.Equal(PlayerSnapshot.Empty, await player.GetSnapshotAsync());
    }
}
