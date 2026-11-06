using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Playback;

/// <summary>
/// The ordered play queue: shuffle, repeat and next/previous rules. Pure, no audio and no threads.
/// <see cref="Order"/> is the order the tracks will be played in; the platform player is given exactly this order.
/// </summary>
public sealed class PlayQueue
{
    /// <summary>Pressing Previous after this much of a track has played restarts the track instead.</summary>
    public static readonly TimeSpan RestartThreshold = TimeSpan.FromSeconds(3);

    private readonly Random random;
    private readonly IReadOnlyList<Track> original;
    private List<Track> order;
    private int position;

    public PlayQueue(IReadOnlyList<Track> tracks, int startIndex, Random random)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentOutOfRangeException.ThrowIfZero(tracks.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startIndex, tracks.Count);

        this.random = random;
        original = [.. tracks];
        order = [.. tracks];
        position = startIndex;
    }

    /// <summary>The tracks as they were queued, regardless of shuffle.</summary>
    public IReadOnlyList<Track> Original => original;

    /// <summary>The tracks in play order (shuffled when <see cref="Shuffle"/> is on).</summary>
    public IReadOnlyList<Track> Order => order;

    /// <summary>Index of the current track in <see cref="Order"/>.</summary>
    public int Position => position;

    public Track Current => order[position];

    public bool Shuffle { get; private set; }

    public RepeatMode Repeat { get; private set; }

    public bool HasNext => position < order.Count - 1 || Repeat == RepeatMode.All;

    public bool HasPrevious => position > 0 || Repeat == RepeatMode.All;

    public void SetRepeat(RepeatMode repeat) => Repeat = repeat;

    /// <summary>
    /// Turning shuffle on keeps the current track first and shuffles the rest (Fisher-Yates).
    /// Turning it off restores the queued order and keeps the current track current.
    /// </summary>
    public void SetShuffle(bool shuffle)
    {
        if (shuffle == Shuffle)
        {
            return;
        }

        var current = Current;
        Shuffle = shuffle;
        if (shuffle)
        {
            var rest = original.Where(track => !ReferenceEquals(track, current)).ToList();
            for (var i = rest.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (rest[i], rest[j]) = (rest[j], rest[i]);
            }

            order = [current, .. rest];
            position = 0;
        }
        else
        {
            order = [.. original];
            position = order.FindIndex(track => ReferenceEquals(track, current));
        }
    }

    /// <summary>Jumps to a position in <see cref="Order"/> (used to follow what the platform player is doing).</summary>
    public void MoveTo(int newPosition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newPosition, order.Count);
        position = newPosition;
    }

    /// <summary>User skip. False when there is nothing after the current track and repeat-all is off.</summary>
    public bool MoveNext()
    {
        if (position < order.Count - 1)
        {
            position++;
            return true;
        }

        if (Repeat == RepeatMode.All)
        {
            position = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// User Previous. True when the queue moved to another track. False means "restart the current track":
    /// more than <see cref="RestartThreshold"/> has played, or there is no previous track.
    /// </summary>
    public bool MovePrevious(TimeSpan played)
    {
        if (played > RestartThreshold)
        {
            return false;
        }

        if (position > 0)
        {
            position--;
            return true;
        }

        if (Repeat == RepeatMode.All)
        {
            position = order.Count - 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// The current track finished by itself. Repeat-one stays on it (returns true), otherwise it behaves like
    /// <see cref="MoveNext"/>. False means the queue ended.
    /// </summary>
    public bool Advance() => Repeat == RepeatMode.One || MoveNext();
}
