using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Playback;

/// <summary>Immutable view of the player at one instant. Equal snapshots mean nothing changed.</summary>
public sealed record PlayerSnapshot(
    Track? Current,
    TimeSpan Position,
    TimeSpan Duration,
    bool IsPlaying,
    bool Shuffle,
    RepeatMode Repeat,
    int QueueIndex,
    int QueueCount)
{
    public static PlayerSnapshot Empty { get; } = new(null, TimeSpan.Zero, TimeSpan.Zero, false, false, RepeatMode.Off, -1, 0);

    public bool HasTrack => Current is not null;

    /// <summary>True when both snapshots differ only by <see cref="Position"/> (a playback tick).</summary>
    public bool SameExceptPosition(PlayerSnapshot other) => this with { Position = TimeSpan.Zero } == other with { Position = TimeSpan.Zero };
}
