using ParoPlayer.Core.Download;

namespace ParoPlayer.Core.Tests;

public class DownloadHelpersTests
{
    [Theory]
    [InlineData("Daft Punk - Get Lucky (Official Video)", "Channel", "Daft Punk", "Get Lucky")]
    [InlineData("Daft Punk – Get Lucky [Official Audio]", "Channel", "Daft Punk", "Get Lucky")]
    [InlineData("Song Title (Lyrics)", "Some Band - Topic", "Some Band", "Song Title")]
    [InlineData("Song Title", "Some BandVEVO", "Some Band", "Song Title")]
    [InlineData("Artist - Song (feat. Someone)", "Channel", "Artist", "Song (feat. Someone)")]
    [InlineData("Just a title - ", "Chan", "Chan", "Just a title -")]
    public void TitleParser_SplitsArtistAndTitle(string title, string channel, string artist, string expectedTitle)
    {
        Assert.Equal((artist, expectedTitle), TitleParser.Parse(title, channel));
    }

    [Theory]
    [InlineData("https://youtu.be/abc", true)]
    [InlineData("  https://www.youtube.com/watch?v=abc  ", true)]
    [InlineData("daft punk get lucky", false)]
    [InlineData("youtube.com/watch?v=abc", false)]
    [InlineData("ftp://x/y", false)]
    public void DownloadInput_TellsLinksFromSearches(string input, bool isLink)
    {
        Assert.Equal(isLink, DownloadInput.IsLink(input));
    }

    [Theory]
    [InlineData(false, true, true, "No internet connection.")]
    [InlineData(true, true, false, "Connect to Wi-Fi, or turn off Download on Wi-Fi only in Settings.")]
    [InlineData(true, true, true, null)]
    [InlineData(true, false, false, null)]
    public void NetworkRules_HonoursWifiOnly(bool online, bool wifiOnly, bool onWifi, string? expected)
    {
        Assert.Equal(expected, NetworkRules.BlockReason(online, wifiOnly, onWifi));
    }

    [Fact]
    public void AudioStreamPicker_PrefersAacOverHigherBitrateOther()
    {
        var streams = new[] { new Stream("webm", 160), new Stream("mp4", 128), new Stream("mp4", 48) };

        var best = AudioStreamPicker.Best(streams, s => s.Container == "mp4", s => s.Rate);

        Assert.Equal(new Stream("mp4", 128), best);
    }

    [Fact]
    public void AudioStreamPicker_FallsBackToHighestBitrate_AndHandlesEmpty()
    {
        var streams = new[] { new Stream("webm", 64), new Stream("webm", 160) };

        Assert.Equal(160, AudioStreamPicker.Best(streams, s => s.Container == "mp4", s => s.Rate)!.Rate);
        Assert.Null(AudioStreamPicker.Best(Array.Empty<Stream>(), s => true, s => s.Rate));
    }

    private sealed record Stream(string Container, long Rate);
}
