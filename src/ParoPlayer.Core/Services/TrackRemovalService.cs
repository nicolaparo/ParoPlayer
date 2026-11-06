using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Services;

/// <summary>Deletes tracks from the library, from manual playlists and (when no other track uses it) from disk.</summary>
public class TrackRemovalService(IRepository<Track> tracks, IRepository<PlaylistTrack> entries)
{
    public async Task RemoveAsync(IReadOnlyCollection<Guid> trackIds)
    {
        var ids = trackIds.ToHashSet();
        var all = await tracks.GetAllAsync();
        var removed = all.Where(track => ids.Contains(track.Id)).ToList();

        foreach (var entry in (await entries.GetAllAsync()).Where(entry => ids.Contains(entry.TrackId)))
        {
            await entries.DeleteAsync(entry.Id);
        }

        foreach (var track in removed)
        {
            await tracks.DeleteAsync(track.Id);
        }

        var stillUsed = all.Where(track => !ids.Contains(track.Id)).Select(track => track.FilePath).ToHashSet();
        foreach (var path in removed.Select(track => track.FilePath).Where(path => path.Length > 0 && !stillUsed.Contains(path)).Distinct())
        {
            File.Delete(path);
        }
    }
}
