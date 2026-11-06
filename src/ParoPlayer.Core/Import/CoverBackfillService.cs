using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Import;

/// <summary>Reads embedded covers for tracks imported before covers were supported. Safe to run again: tracks that already have one are skipped.</summary>
public class CoverBackfillService(IRepository<Track> tracks, ITrackMetadataReader reader, CoverStore covers)
{
    private bool done;

    /// <summary>Returns how many tracks received a cover. Runs once per app session; later calls return 0.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (done)
        {
            return 0;
        }

        done = true;
        var updated = 0;
        foreach (var track in (await tracks.GetAllAsync()).Where(track => track.CoverPath.Length == 0 && File.Exists(track.FilePath)))
        {
            var metadata = await reader.ReadAsync(track.FilePath, cancellationToken);
            track.CoverPath = await covers.SaveAsync(track.Id, metadata.Cover, cancellationToken);
            if (track.CoverPath.Length > 0)
            {
                await tracks.UpdateAsync(track);
                updated++;
            }
        }

        return updated;
    }
}
