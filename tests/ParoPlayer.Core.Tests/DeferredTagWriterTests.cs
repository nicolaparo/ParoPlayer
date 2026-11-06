using ParoPlayer.Core.Bpm;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Playback;
using ParoPlayer.Core.Tagging;

namespace ParoPlayer.Core.Tests;

public sealed class DeferredTagWriterTests : IAsyncDisposable
{
    private readonly SilentPlayerService player = new(TimeProvider.System, new Random(1));
    private readonly FakeTagWriter tags = new();
    private readonly FakeBpmTag bpm = new();
    private readonly DeferredTagWriter writer;

    public DeferredTagWriterTests() => writer = new DeferredTagWriter(player, tags, bpm);

    public async ValueTask DisposeAsync()
    {
        writer.Dispose();
        await player.DisposeAsync();
    }

    private static Track Song(string path) => new() { Title = path, FilePath = path };

    private static readonly TrackTags Edited = new("New", "Artist", "Album", 2020, "Genre", 1);

    [Fact]
    public async Task Write_FileNotPlaying_IsSavedAtOnce()
    {
        var error = await writer.WriteAsync("a.mp3", new PendingTagChange(Edited, null, 128));

        Assert.Null(error);
        Assert.Equal(Edited, tags.Tags);
        Assert.Equal(128, bpm.Written);
        Assert.Equal(0, writer.PendingCount);
    }

    [Fact]
    public async Task Write_PlayingFile_WaitsUntilAnotherTrackPlays()
    {
        await player.PlayAsync([Song("a.mp3"), Song("b.mp3")], 0);

        await writer.WriteAsync("a.mp3", new PendingTagChange(Edited));
        await writer.WriteAsync("a.mp3", new PendingTagChange(Bpm: 140));

        Assert.Null(tags.Tags);
        Assert.Null(bpm.Written);
        Assert.Equal(1, writer.PendingCount);

        await writer.FlushAsync("b.mp3");

        Assert.Equal(Edited, tags.Tags);
        Assert.Equal(140, bpm.Written);
        Assert.Equal(0, writer.PendingCount);
    }

    [Fact]
    public async Task Flush_KeepsChangesForTheFileStillPlaying()
    {
        await player.PlayAsync([Song("a.mp3")], 0);
        await writer.WriteAsync("a.mp3", new PendingTagChange(Bpm: 90));

        await writer.FlushAsync("a.mp3");

        Assert.Null(bpm.Written);
        Assert.Equal(1, writer.PendingCount);

        await writer.FlushAsync(null);

        Assert.Equal(90, bpm.Written);
    }

    [Fact]
    public async Task Write_FailingFile_ReturnsTheError()
    {
        tags.Error = new IOException("locked");

        Assert.Equal("locked", await writer.WriteAsync("a.mp3", new PendingTagChange(Edited)));
        Assert.Equal(0, writer.PendingCount);
    }

    private sealed class FakeBpmTag : IBpmTag
    {
        public int? Written { get; private set; }

        public Task<int> ReadAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task WriteAsync(string filePath, int value, CancellationToken cancellationToken = default)
        {
            Written = value;
            return Task.CompletedTask;
        }
    }
}
