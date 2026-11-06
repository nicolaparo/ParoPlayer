using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Harmony;

/// <summary>The key of a track: the stored one, or a fresh detection saved to the database (the audio file is never written).</summary>
public class TrackKeyService(IRepository<Track> tracks, KeyDetectionService detection)
{
    /// <summary>The key of <paramref name="track"/>, or null when none could be detected.</summary>
    public async Task<string?> GetAsync(Track track, CancellationToken cancellationToken = default)
    {
        if (track.Key is not null)
        {
            return track.Key;
        }

        var key = await detection.DetectAsync(track.FilePath, cancellationToken);
        if (key is null)
        {
            return null;
        }

        track.Key = key;
        if (await tracks.GetAsync(track.Id) is { } stored)
        {
            stored.Key = key;
            await tracks.UpdateAsync(stored);
        }

        return key;
    }
}
