using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace ParoPlayer.Core.Download;

/// <summary>YoutubeExplode adapter. Thin on purpose: all decisions that can be tested live elsewhere.</summary>
public sealed class YoutubeMediaSource(HttpClient http) : IMediaSource
{
    private const int MaxPlaylistItems = 100;
    private const int MaxSearchResults = 10;

    private readonly YoutubeClient youtube = new(http);

    public async Task<IReadOnlyList<MediaInfo>> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            if (VideoId.TryParse(url) is { } videoId)
            {
                return [ToInfo(await youtube.Videos.GetAsync(videoId, cancellationToken))];
            }

            if (PlaylistId.TryParse(url) is { } playlistId)
            {
                var videos = await youtube.Playlists.GetVideosAsync(playlistId, cancellationToken).Take(MaxPlaylistItems).ToListAsync(cancellationToken);
                return [.. videos.Select(ToInfo)];
            }
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            throw new MediaSourceException($"Could not read that link: {ex.Message}", ex);
        }

        throw new MediaSourceException("That is not a supported video or playlist link.");
    }

    public async Task<IReadOnlyList<MediaInfo>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await youtube.Search.GetVideosAsync(query, cancellationToken).Take(MaxSearchResults).ToListAsync(cancellationToken);
            return [.. results.Select(ToInfo)];
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            throw new MediaSourceException($"Search failed: {ex.Message}", ex);
        }
    }

    public async Task<DownloadedAudio> DownloadAudioAsync(MediaInfo media, string basePath, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        try
        {
            var manifest = await youtube.Videos.Streams.GetManifestAsync(media.Id, cancellationToken);
            var stream = AudioStreamPicker.Best(manifest.GetAudioOnlyStreams(), s => s.Bitrate.BitsPerSecond)
                ?? throw new MediaSourceException("No audio stream is available for this video.");
            Trace.WriteLine($"Download {media.Id}: itag {Itag(stream.Url)}, codec {stream.AudioCodec}, {stream.Bitrate.KiloBitsPerSecond:0} kbps, {stream.Container.Name}");
            var path = $"{basePath}.{stream.Container.Name}";
            await youtube.Videos.Streams.DownloadAsync(stream, path, progress, cancellationToken);
            return new DownloadedAudio(path, IsAac(stream));
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            throw new MediaSourceException($"Download failed: {ex.Message}", ex);
        }
    }

    /// <summary>Network trouble, including the HttpClient timeout, which surfaces as a cancellation the caller did not ask for.</summary>
    private static bool IsNetworkError(Exception ex, CancellationToken cancellationToken) =>
        ex is YoutubeExplodeException or HttpRequestException or IOException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static string Itag(string url) => System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["itag"] ?? "?";

    private static bool IsAac(AudioOnlyStreamInfo stream) => stream.AudioCodec.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase);

    public async Task<byte[]?> FetchCoverAsync(MediaInfo media, CancellationToken cancellationToken = default)
    {
        if (media.ThumbnailUrl is null)
        {
            return null;
        }

        try
        {
            return await http.GetByteArrayAsync(media.ThumbnailUrl, cancellationToken);
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            return null;
        }
    }

    private static MediaInfo ToInfo(IVideo video) => new(
        video.Id.Value,
        video.Title,
        video.Author.ChannelTitle,
        video.Duration ?? TimeSpan.Zero,
        video.Url,
        video.Thumbnails.OrderByDescending(thumbnail => thumbnail.Resolution.Area).FirstOrDefault()?.Url);
}

/// <summary>Picks the audio stream to download.</summary>
public static class AudioStreamPicker
{
    /// <summary>The highest-bitrate stream, whatever the codec. Null when empty.</summary>
    public static T? Best<T>(IEnumerable<T> streams, Func<T, long> bitrate) where T : class => streams.MaxBy(bitrate);
}
