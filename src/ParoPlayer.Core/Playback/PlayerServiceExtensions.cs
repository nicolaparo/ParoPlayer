using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Playback;

public static class PlayerServiceExtensions
{
    /// <summary>
    /// Queues <paramref name="list"/> (tracks without an audio file are skipped) and starts at <paramref name="start"/>.
    /// Does nothing when no track in the list can be played.
    /// </summary>
    public static Task PlayFromAsync(this IPlayerService player, IReadOnlyList<Track> list, Track start, CancellationToken cancellationToken = default)
    {
        var playable = list.Where(HasAudioFile).ToList();
        var index = playable.FindIndex(track => track.Id == start.Id);
        return index < 0 ? Task.CompletedTask : player.PlayAsync(playable, index, cancellationToken);
    }

    /// <summary>Plays <paramref name="list"/> in a random order, starting from a random track.</summary>
    public static async Task PlayShuffledAsync(this IPlayerService player, IReadOnlyList<Track> list, Random random, CancellationToken cancellationToken = default)
    {
        var playable = list.Where(HasAudioFile).ToList();
        if (playable.Count == 0)
        {
            return;
        }

        await player.PlayAsync(playable, random.Next(playable.Count), cancellationToken);
        await player.SetShuffleAsync(true, cancellationToken);
    }

    private static bool HasAudioFile(Track track) => !string.IsNullOrWhiteSpace(track.FilePath);
}
