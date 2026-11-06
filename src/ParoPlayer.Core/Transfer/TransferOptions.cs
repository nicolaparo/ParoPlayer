namespace ParoPlayer.Core.Transfer;

/// <summary>Limits of the Wi-Fi transfer server.</summary>
public sealed record TransferOptions
{
    public long MaxUploadBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>The session ends after this long without a request.</summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>An unanswered approval request is treated as denied after this long.</summary>
    public TimeSpan PendingTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public int MaxAccessRequestsPerMinute { get; init; } = 5;
}
