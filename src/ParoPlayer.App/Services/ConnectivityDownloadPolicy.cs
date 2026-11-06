using ParoPlayer.Core.Download;

namespace ParoPlayer.App.Services;

/// <summary>Blocks downloads when offline, or off Wi-Fi/Ethernet while "Download on Wi-Fi only" is on.</summary>
public class ConnectivityDownloadPolicy(AppSettings settings, IConnectivity connectivity) : IDownloadPolicy
{
    public string? BlockReason()
    {
        var profiles = connectivity.ConnectionProfiles;
        return NetworkRules.BlockReason(
            connectivity.NetworkAccess == NetworkAccess.Internet,
            settings.WifiOnly,
            profiles.Contains(ConnectionProfile.WiFi) || profiles.Contains(ConnectionProfile.Ethernet));
    }
}
