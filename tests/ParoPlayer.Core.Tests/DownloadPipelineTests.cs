using ParoPlayer.Core.Download;

namespace ParoPlayer.Core.Tests;

public sealed class DownloadPipelineTests : IDisposable
{
    private readonly DownloadHarness harness = new();

    public void Dispose() => harness.Dispose();

    private static void Ignore(DownloadState state, double progress)
    {
    }

    [Fact]
    public async Task Run_DownloadsConvertsTagsAndImportsIntoLibrary()
    {
        var states = new List<DownloadState>();

        var outcome = await harness.Pipeline.RunAsync(DownloadHarness.Media("a", "Daft Punk - Get Lucky (Official Video)"), (state, _) => states.Add(state));

        Assert.Equal("Get Lucky", harness.Tags.Tags?.Title);
        Assert.Equal("Daft Punk", harness.Tags.Tags?.Artist);
        Assert.Equal(new byte[] { 1, 2, 3 }, harness.Tags.Cover);
        Assert.Null(outcome.Note);
        Assert.StartsWith(harness.Folder.Path, outcome.Track.FilePath);
        Assert.EndsWith("Daft Punk - Get Lucky.m4a", outcome.Track.FilePath);
        Assert.Equal("audio of a", await File.ReadAllTextAsync(outcome.Track.FilePath));
        Assert.Single(await harness.Library.GetAllAsync());
        Assert.Equal([DownloadState.Downloading, DownloadState.Downloading, DownloadState.Downloading, DownloadState.Converting, DownloadState.Tagging, DownloadState.Importing], states);
        Assert.True(harness.WorkFolderIsEmpty());
    }

    [Fact]
    public async Task Run_ReportsIncreasingProgress()
    {
        var values = new List<double>();

        await harness.Pipeline.RunAsync(DownloadHarness.Media("a"), (_, progress) => values.Add(progress));

        Assert.Equal(values.Order(), values);
        Assert.InRange(values[^1], 0.9, 1);
    }

    [Fact]
    public async Task Run_PassesAacFlagToConverter()
    {
        harness.Source.IsAac = false;

        await harness.Pipeline.RunAsync(DownloadHarness.Media("a"), Ignore);

        Assert.False(harness.Converter.Last?.InputIsAac);
    }

    [Fact]
    public async Task Run_ConversionFails_KeepsOriginalFileWithNote()
    {
        harness.Converter.Error = new AudioConversionException("ffmpeg failed");

        var outcome = await harness.Pipeline.RunAsync(DownloadHarness.Media("a", "Artist - Song"), Ignore);

        Assert.EndsWith("Artist - Song.mp4", outcome.Track.FilePath);
        Assert.Contains("ffmpeg failed", outcome.Note);
        Assert.True(harness.WorkFolderIsEmpty());
    }

    [Fact]
    public async Task Run_TaggingFails_StillImports()
    {
        harness.Tags.Error = new TagLib.UnsupportedFormatException("no");

        var outcome = await harness.Pipeline.RunAsync(DownloadHarness.Media("a"), Ignore);

        Assert.StartsWith("Saved without tags", outcome.Note);
        Assert.Single(await harness.Library.GetAllAsync());
    }

    [Fact]
    public async Task Run_NoArtistInTitle_UsesChannelAndPlainFileName()
    {
        var outcome = await harness.Pipeline.RunAsync(DownloadHarness.Media("a", "Song", "Band - Topic"), Ignore);

        Assert.Equal("Band", harness.Tags.Tags?.Artist);
        Assert.Equal("Band - Song.m4a", Path.GetFileName(outcome.Track.FilePath));
    }

    [Fact]
    public async Task Run_DownloadFails_CleansUpAndImportsNothing()
    {
        harness.Source.DownloadError = new MediaSourceException("offline");

        await Assert.ThrowsAsync<MediaSourceException>(() => harness.Pipeline.RunAsync(DownloadHarness.Media("a"), Ignore));

        Assert.Empty(await harness.Library.GetAllAsync());
        Assert.True(harness.WorkFolderIsEmpty());
    }

    [Fact]
    public async Task Run_Canceled_CleansUp()
    {
        harness.Source.Gate = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Pipeline.RunAsync(DownloadHarness.Media("a"), Ignore, cancellation.Token));
        Assert.Empty(await harness.Library.GetAllAsync());
        Assert.True(harness.WorkFolderIsEmpty());
    }
}
