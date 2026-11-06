using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Playback;

namespace ParoPlayer.Core.Tests;

public class PlayerServiceExtensionsTests
{
    private static Track Song(string title, string path = "/music/a.wav") => new() { Title = title, FilePath = path, DurationSeconds = 60 };

    [Fact]
    public async Task PlayFromStartsAtTheTappedTrackAndSkipsTracksWithoutAFile()
    {
        await using var player = new SilentPlayerService(new FakeTimeProvider(), new Random(1));
        var tapped = Song("C");
        List<Track> list = [Song("A"), Song("B", path: ""), tapped];

        await player.PlayFromAsync(list, tapped);
        var snapshot = await player.GetSnapshotAsync();

        Assert.Equal("C", snapshot.Current?.Title);
        Assert.Equal(2, snapshot.QueueCount);
        Assert.Equal(1, snapshot.QueueIndex);
    }

    [Fact]
    public async Task PlayFromDoesNothingWhenTheTappedTrackHasNoFile()
    {
        await using var player = new SilentPlayerService(new FakeTimeProvider(), new Random(1));
        var silent = Song("B", path: "");

        await player.PlayFromAsync([Song("A"), silent], silent);

        Assert.False((await player.GetSnapshotAsync()).HasTrack);
    }

    [Fact]
    public async Task PlayShuffledTurnsShuffleOnAndPlays()
    {
        await using var player = new SilentPlayerService(new FakeTimeProvider(), new Random(1));

        await player.PlayShuffledAsync([Song("A"), Song("B"), Song("C"), Song("D")], new Random(5));
        var snapshot = await player.GetSnapshotAsync();

        Assert.True(snapshot.Shuffle);
        Assert.True(snapshot.IsPlaying);
        Assert.Equal(4, snapshot.QueueCount);
    }
}
