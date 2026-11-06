namespace ParoPlayer.Core.Download;

/// <summary>Tells a pasted link from a search phrase.</summary>
public static class DownloadInput
{
    public static bool IsLink(string input) =>
        Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
