namespace ParoPlayer.Core.Import;

public record TrackMetadata(
    string Title,
    string Artist = "",
    string Album = "",
    string Genre = "",
    int Year = 0,
    int DurationSeconds = 0,
    int TrackNumber = 0,
    byte[]? Cover = null);

/// <summary>Reads title and tags from an audio file. Implementations never throw for unreadable files.</summary>
public interface ITrackMetadataReader
{
    Task<TrackMetadata> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
