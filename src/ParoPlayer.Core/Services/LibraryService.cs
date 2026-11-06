using ParoPlayer.Core.Data;
using ParoPlayer.Core.Library;
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
        return LibraryQuery.Search(await tracks.GetAllAsync(), query);
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
