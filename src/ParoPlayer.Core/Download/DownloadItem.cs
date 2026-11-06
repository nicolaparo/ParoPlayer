namespace ParoPlayer.Core.Download;

public enum DownloadState
{
    Queued,
    Downloading,
    Converting,
    Tagging,
    Importing,
    Done,
    Failed,
    Canceled,
}

/// <summary>Immutable snapshot of one queue entry. <see cref="Progress"/> is 0 to 1 over all stages.</summary>
public record DownloadItem(Guid Id, MediaInfo Media, DownloadState State = DownloadState.Queued, double Progress = 0, string? Message = null, Guid? TrackId = null)
{
    public bool IsFinished => State is DownloadState.Done or DownloadState.Failed or DownloadState.Canceled;

    public bool IsRunning => State is DownloadState.Downloading or DownloadState.Converting or DownloadState.Tagging or DownloadState.Importing;
}
