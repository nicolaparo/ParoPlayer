namespace ParoPlayer.Core.Download;

/// <summary>
/// Keeps the process alive while downloads run (an Android foreground service). Called from a background thread when
/// the queue starts working. The platform stops itself when the queue is idle, which avoids start/stop races.
/// </summary>
public interface IDownloadKeepAlive
{
    void Begin();
}

public sealed class NoKeepAlive : IDownloadKeepAlive
{
    public void Begin()
    {
    }
}
