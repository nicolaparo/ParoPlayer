using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Playback;

/// <summary>
/// Platform independent playback controls. Android implements it on top of a Media3 session service,
/// Windows uses <see cref="SilentPlayerService"/> (no audio).
/// </summary>
public interface IPlayerService
{
    /// <summary>Replaces the queue with <paramref name="queue"/> and starts playing at <paramref name="startIndex"/>.</summary>
    Task PlayAsync(IReadOnlyList<Track> queue, int startIndex = 0, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>Skips to the next track. Does nothing on the last track unless repeat-all is on.</summary>
    Task NextAsync(CancellationToken cancellationToken = default);

    /// <summary>Restarts the track when more than <see cref="PlayQueue.RestartThreshold"/> has played, otherwise goes to the previous track.</summary>
    Task PreviousAsync(CancellationToken cancellationToken = default);

    Task SeekToAsync(TimeSpan position, CancellationToken cancellationToken = default);

    Task SetShuffleAsync(bool shuffle, CancellationToken cancellationToken = default);

    Task SetRepeatAsync(RepeatMode repeat, CancellationToken cancellationToken = default);

    /// <summary>Reads the current state.</summary>
    Task<PlayerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <paramref name="onChanged"/> (awaited, one subscriber at a time) whenever the snapshot changes,
    /// including position ticks while playing. Dispose the result to unsubscribe.
    /// </summary>
    IDisposable Subscribe(Func<PlayerSnapshot, Task> onChanged);
}
