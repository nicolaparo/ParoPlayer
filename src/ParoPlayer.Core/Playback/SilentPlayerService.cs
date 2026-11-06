using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Playback;

/// <summary>
/// An <see cref="IPlayerService"/> that makes no sound: it walks the queue and moves the position with the clock.
/// Used on Windows (dev preview) and in tests, so the UI and the queue rules work without a native player.
/// </summary>
public sealed class SilentPlayerService : IPlayerService, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly Lock gate = new();
    private readonly TimeProvider timeProvider;
    private readonly Random random;
    private readonly PlayerStateBroadcaster broadcaster;
    private PlayQueue? queue;
    private TimeSpan offset;
    private DateTimeOffset? playingSince;

    public SilentPlayerService(TimeProvider timeProvider, Random random)
    {
        this.timeProvider = timeProvider;
        this.random = random;
        broadcaster = new PlayerStateBroadcaster(ReadAsync, timeProvider, PollInterval);
    }

    public Task PlayAsync(IReadOnlyList<Track> tracks, int startIndex = 0, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            queue = new PlayQueue(tracks, startIndex, random);
            Restart(playing: true);
        }

        return broadcaster.RefreshAsync(cancellationToken);
    }

    public Task PauseAsync(CancellationToken cancellationToken = default) => ChangeAsync(() =>
    {
        offset = PositionNow();
        playingSince = null;
    }, cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) => ChangeAsync(() =>
    {
        if (queue is not null && playingSince is null)
        {
            playingSince = timeProvider.GetUtcNow();
        }
    }, cancellationToken);

    public Task NextAsync(CancellationToken cancellationToken = default) => ChangeAsync(() =>
    {
        if (queue?.MoveNext() == true)
        {
            Restart(playing: playingSince is not null);
        }
    }, cancellationToken);

    public Task PreviousAsync(CancellationToken cancellationToken = default) => ChangeAsync(() =>
    {
        if (queue is null)
        {
            return;
        }

        queue.MovePrevious(PositionNow());
        Restart(playing: playingSince is not null);
    }, cancellationToken);

    public Task SeekToAsync(TimeSpan position, CancellationToken cancellationToken = default) => ChangeAsync(() =>
    {
        if (queue is null)
        {
            return;
        }

        offset = TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, DurationOf(queue.Current).Ticks));
        if (playingSince is not null)
        {
            playingSince = timeProvider.GetUtcNow();
        }
    }, cancellationToken);

    public Task SetShuffleAsync(bool shuffle, CancellationToken cancellationToken = default) => ChangeAsync(() => queue?.SetShuffle(shuffle), cancellationToken);

    public Task SetRepeatAsync(RepeatMode repeat, CancellationToken cancellationToken = default) => ChangeAsync(() => queue?.SetRepeat(repeat), cancellationToken);

    public Task<PlayerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => ReadAsync(cancellationToken);

    public IDisposable Subscribe(Func<PlayerSnapshot, Task> onChanged) => broadcaster.Subscribe(onChanged);

    public ValueTask DisposeAsync() => broadcaster.DisposeAsync();

    private Task ChangeAsync(Action action, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            action();
        }

        return broadcaster.RefreshAsync(cancellationToken);
    }

    private Task<PlayerSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (queue is null)
            {
                return Task.FromResult(PlayerSnapshot.Empty);
            }

            FinishElapsedTracks();
            var current = queue.Current;
            return Task.FromResult(new PlayerSnapshot(
                current, PositionNow(), DurationOf(current), playingSince is not null,
                queue.Shuffle, queue.Repeat, queue.Position, queue.Order.Count));
        }
    }

    /// <summary>Moves on when the clock ran past the end of the current track (called with the lock held).</summary>
    private void FinishElapsedTracks()
    {
        while (queue is not null && playingSince is not null)
        {
            var duration = DurationOf(queue.Current);
            var position = PositionNow();
            if (duration == TimeSpan.Zero || position < duration)
            {
                return;
            }

            if (queue.Advance())
            {
                offset = position - duration;
                playingSince = timeProvider.GetUtcNow();
            }
            else
            {
                offset = TimeSpan.Zero;
                playingSince = null;
            }
        }
    }

    private void Restart(bool playing)
    {
        offset = TimeSpan.Zero;
        playingSince = playing ? timeProvider.GetUtcNow() : null;
    }

    private TimeSpan PositionNow() => playingSince is { } since ? offset + (timeProvider.GetUtcNow() - since) : offset;

    private static TimeSpan DurationOf(Track track) => TimeSpan.FromSeconds(Math.Max(track.DurationSeconds, 0));
}
