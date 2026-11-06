namespace ParoPlayer.Core.Import;

/// <summary>Fallback reader: the title is the file name without extension.</summary>
public class FileNameMetadataReader : ITrackMetadataReader
{
    public Task<TrackMetadata> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(FromFileName(filePath));

    public static TrackMetadata FromFileName(string filePath) =>
        new(Path.GetFileNameWithoutExtension(filePath));
}
