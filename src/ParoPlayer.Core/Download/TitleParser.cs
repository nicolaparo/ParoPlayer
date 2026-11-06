using System.Text.RegularExpressions;

namespace ParoPlayer.Core.Download;

/// <summary>Guesses artist and title from a video title and its channel name.</summary>
public static partial class TitleParser
{
    private static readonly string[] Separators = [" - ", " – ", " — "];

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*\b(official|video|audio|lyrics?|visuali[sz]er|hd|hq|4k|remaster(ed)?)\b[^\)\]]*[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex Noise();

    [GeneratedRegex(@"\s*(- Topic|VEVO)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelSuffix();

    public static (string Artist, string Title) Parse(string videoTitle, string channel)
    {
        var cleaned = Noise().Replace(videoTitle, "").Trim();
        foreach (var separator in Separators)
        {
            var index = cleaned.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0 && index + separator.Length < cleaned.Length)
            {
                return (cleaned[..index].Trim(), cleaned[(index + separator.Length)..].Trim());
            }
        }

        var artist = ChannelSuffix().Replace(channel, "").Trim();
        return (artist, cleaned.Length == 0 ? videoTitle.Trim() : cleaned);
    }
}
