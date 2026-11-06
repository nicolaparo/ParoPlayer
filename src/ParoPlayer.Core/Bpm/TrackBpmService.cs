using System.Collections.Concurrent;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Tagging;

namespace ParoPlayer.Core.Bpm;

/// <summary>
/// The BPM of a track for display: the tag when the file has one, otherwise an estimate. An estimate is kept in memory
/// and handed to <see cref="DeferredTagWriter"/>, which saves it to the file once the track is no longer playing.
/// </summary>
public class TrackBpmService(IBpmTag tag, BpmDetectionService detection, DeferredTagWriter writer)
{
    private readonly ConcurrentDictionary<Guid, int> known = [];

    /// <summary>The BPM of <paramref name="track"/>, or null when it has none and none could be estimated.</summary>
    public async Task<int?> GetAsync(Track track, CancellationToken cancellationToken = default)
    {
        if (known.TryGetValue(track.Id, out var cached))
        {
            return cached;
        }

        var stored = await tag.ReadAsync(track.FilePath, cancellationToken);
        if (stored > 0)
        {
            known[track.Id] = stored;
            return stored;
        }

        var estimate = await detection.DetectAsync(track.FilePath, persist: false, cancellationToken: cancellationToken);
        if (estimate is not { } bpm)
        {
            return null;
        }

        known[track.Id] = bpm;
        await writer.WriteAsync(track.FilePath, new PendingTagChange(Bpm: bpm), cancellationToken);
        return bpm;
    }
}
