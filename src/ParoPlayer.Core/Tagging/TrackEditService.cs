using ParoPlayer.Core.Data;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Tagging;

/// <summary>What a track looked like before an edit. Kept in memory until the user confirms or undoes the edit.</summary>
public record TrackEditSnapshot(Track Before, byte[]? Cover);

/// <param name="TagError">Set when the database was updated but the audio file could not be tagged.</param>
public record TrackEditResult(Track Track, TrackEditSnapshot Snapshot, string? TagError);

/// <summary>Saves manual or looked-up edits to the database and writes them back to the file tags.</summary>
public class TrackEditService(IRepository<Track> tracks, ITrackTagWriter tagWriter, CoverStore covers)
{
    public async Task<TrackEditResult?> ApplyAsync(Guid trackId, TrackEdit edit, byte[]? newCover, CancellationToken cancellationToken = default)
    {
        var track = await tracks.GetAsync(trackId);
        if (track is null)
        {
            return null;
        }

        var snapshot = new TrackEditSnapshot(Copy(track), await ReadCoverAsync(track.CoverPath, cancellationToken));

        track.Title = string.IsNullOrWhiteSpace(edit.Title) ? track.Title : edit.Title.Trim();
        track.Artist = edit.Artist.Trim();
        track.Album = edit.Album.Trim();
        track.Genre = edit.Genre.Trim();
        track.Year = Math.Max(edit.Year, 0);
        track.TrackNumber = Math.Max(edit.TrackNumber, 0);

        byte[]? embedded = null;
        if (newCover is { Length: > 0 })
        {
            var path = await covers.SaveAsync(track.Id, newCover, cancellationToken);
            if (path.Length > 0)
            {
                track.CoverPath = path;
                embedded = await File.ReadAllBytesAsync(path, cancellationToken);
            }
        }

        var tagError = await TryWriteAsync(track, embedded, cancellationToken);
        await tracks.UpdateAsync(track);
        return new TrackEditResult(track, snapshot, tagError);
    }

    /// <summary>Puts the track, its cover file and its file tags back as they were. Returns a tag error, if any.</summary>
    public async Task<string?> UndoAsync(TrackEditSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var before = Copy(snapshot.Before);
        var current = await tracks.GetAsync(before.Id);
        if (current is null)
        {
            return null;
        }

        if (snapshot.Cover is not null && before.CoverPath.Length > 0)
        {
            await File.WriteAllBytesAsync(before.CoverPath, snapshot.Cover, cancellationToken);
        }
        else if (current.CoverPath.Length > 0 && before.CoverPath.Length == 0)
        {
            File.Delete(current.CoverPath);
        }

        await tracks.UpdateAsync(before);
        return await TryWriteAsync(before, snapshot.Cover, cancellationToken);
    }

    private async Task<string?> TryWriteAsync(Track track, byte[]? cover, CancellationToken cancellationToken)
    {
        try
        {
            var tags = new TrackTags(track.Title, track.Artist, track.Album, track.Year, track.Genre, track.TrackNumber);
            await tagWriter.WriteAsync(track.FilePath, tags, cover, cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TagLib.UnsupportedFormatException or TagLib.CorruptFileException)
        {
            return ex.Message;
        }
    }

    private static async Task<byte[]?> ReadCoverAsync(string path, CancellationToken cancellationToken) =>
        path.Length > 0 && File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;

    private static Track Copy(Track t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Artist = t.Artist,
        Album = t.Album,
        Genre = t.Genre,
        Year = t.Year,
        DurationSeconds = t.DurationSeconds,
        FilePath = t.FilePath,
        Rating = t.Rating,
        PlayCount = t.PlayCount,
        AddedAt = t.AddedAt,
        LastPlayedAt = t.LastPlayedAt,
        TrackNumber = t.TrackNumber,
        CoverPath = t.CoverPath,
    };
}
