using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Services;

public class LibraryService(IRepository<Track> tracks, TimeProvider timeProvider)
{
    public async Task<Track> AddTrackAsync(Track track)
    {
        track.AddedAt = timeProvider.GetUtcNow();
        await tracks.AddAsync(track);
        return track;
    }

    public Task<IReadOnlyList<Track>> GetAllAsync() => tracks.GetAllAsync();

    public async Task<IReadOnlyList<Track>> SearchAsync(string query)
    {
        var all = await tracks.GetAllAsync();
        if (string.IsNullOrWhiteSpace(query))
        {
            return all;
        }

        return [.. all.Where(track =>
            track.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || track.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)
            || track.Album.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Increments the play count and stamps LastPlayedAt. Returns null when the track does not exist.</summary>
    public async Task<Track?> RecordPlayAsync(Guid trackId)
    {
        var track = await tracks.GetAsync(trackId);
        if (track is null)
        {
            return null;
        }

        track.PlayCount++;
        track.LastPlayedAt = timeProvider.GetUtcNow();
        await tracks.UpdateAsync(track);
        return track;
    }
}
