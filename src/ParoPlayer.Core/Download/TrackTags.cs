namespace ParoPlayer.Core.Download;

public record TrackTags(string Title, string Artist, string Album = "", int Year = 0);

/// <summary>Writes tags and cover art into an audio file. Throws when the format cannot be tagged.</summary>
public interface ITrackTagWriter
{
    Task WriteAsync(string filePath, TrackTags tags, byte[]? cover, CancellationToken cancellationToken = default);
}

public sealed class TagLibTrackTagWriter : ITrackTagWriter
{
    public Task WriteAsync(string filePath, TrackTags tags, byte[]? cover, CancellationToken cancellationToken = default) =>
        Task.Run(() => Write(filePath, tags, cover), cancellationToken);

    private static void Write(string filePath, TrackTags tags, byte[]? cover)
    {
        using var file = TagLib.File.Create(filePath);
        file.Tag.Title = tags.Title;
        file.Tag.Performers = string.IsNullOrWhiteSpace(tags.Artist) ? [] : [tags.Artist];
        file.Tag.Album = tags.Album;
        file.Tag.Year = (uint)Math.Max(tags.Year, 0);
        if (cover is { Length: > 0 })
        {
            file.Tag.Pictures = [new TagLib.Picture(new TagLib.ByteVector(cover)) { Type = TagLib.PictureType.FrontCover }];
        }

        file.Save();
    }
}
