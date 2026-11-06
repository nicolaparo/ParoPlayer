namespace ParoPlayer.Core.Download;

/// <summary>Platform rules that can block network use (connectivity, Wi-Fi only setting).</summary>
public interface IDownloadPolicy
{
    /// <summary>Returns a user-facing reason when downloading is not allowed right now, otherwise null.</summary>
    string? BlockReason();
}

public sealed class AlwaysAllowedDownloadPolicy : IDownloadPolicy
{
    public string? BlockReason() => null;
}
