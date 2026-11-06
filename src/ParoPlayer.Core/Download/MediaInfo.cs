namespace ParoPlayer.Core.Download;

/// <summary>A downloadable item as resolved from a link or a search.</summary>
public record MediaInfo(string Id, string Title, string Author, TimeSpan Duration, string Url, string? ThumbnailUrl = null);
