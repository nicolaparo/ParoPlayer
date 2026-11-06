namespace ParoPlayer.Core.Import;

/// <summary>Reads embedded tags with TagLibSharp; falls back to the file name when tags are missing or the file is unreadable.</summary>
public class TagLibMetadataReader : ITrackMetadataReader
{
    public Task<TrackMetadata> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(filePath), cancellationToken);

    private static TrackMetadata Read(string filePath)
    {
        var fallback = FileNameMetadataReader.FromFileName(filePath);
        try
        {
            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;
            return new TrackMetadata(
                string.IsNullOrWhiteSpace(tag.Title) ? fallback.Title : tag.Title.Trim(),
                tag.FirstPerformer?.Trim() ?? "",
                tag.Album?.Trim() ?? "",
                tag.FirstGenre?.Trim() ?? "",
                (int)tag.Year,
                (int)Math.Round(file.Properties.Duration.TotalSeconds),
                (int)tag.Track);
        }
        catch (Exception ex) when (ex is TagLib.CorruptFileException or TagLib.UnsupportedFormatException or IOException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }
}
