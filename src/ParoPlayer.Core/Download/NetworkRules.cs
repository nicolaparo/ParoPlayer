namespace ParoPlayer.Core.Download;

/// <summary>Decides whether the current connection may be used for downloading.</summary>
public static class NetworkRules
{
    /// <summary>Returns why downloading is blocked, or null when it is allowed.</summary>
    public static string? BlockReason(bool online, bool wifiOnly, bool onWifiOrEthernet)
    {
        if (!online)
        {
            return "No internet connection.";
        }

        return wifiOnly && !onWifiOrEthernet ? "Connect to Wi-Fi, or turn off Download on Wi-Fi only in Settings." : null;
    }
}
