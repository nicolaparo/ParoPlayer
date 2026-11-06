namespace ParoPlayer.Core.Download;

/// <summary>An audio file saved by an <see cref="IMediaSource"/>. <see cref="IsAac"/> tells the converter whether it can copy the stream.</summary>
public record DownloadedAudio(string FilePath, bool IsAac);

public class MediaSourceException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Resolves links and searches to media and downloads their audio. Implementations throw <see cref="MediaSourceException"/> for network and service errors.</summary>
public interface IMediaSource
{
    /// <summary>Resolves a video or playlist link.</summary>
    Task<IReadOnlyList<MediaInfo>> ResolveAsync(string url, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaInfo>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Downloads the best audio stream to <c>{basePath}.{extension}</c>.</summary>
    Task<DownloadedAudio> DownloadAudioAsync(MediaInfo media, string basePath, IProgress<double> progress, CancellationToken cancellationToken = default);

    /// <summary>Downloads the cover image, or returns null when there is none or it cannot be fetched.</summary>
    Task<byte[]?> FetchCoverAsync(MediaInfo media, CancellationToken cancellationToken = default);
}
