using ParoPlayer.Core.Data;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Tests;

public sealed class WaveformBackfillServiceTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory().FullName;
    private readonly InMemoryRepository<Track> tracks = new();

    public void Dispose() => Directory.Delete(root, recursive: true);

    private sealed class FakeAnalyzer(Func<string, bool> succeeds) : IWaveformAnalyzer
    {
        public int Calls { get; private set; }

        public Task<float[]?> AnalyzeAsync(string filePath, int bars, Action<float[]>? partial = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(succeeds(filePath) ? new float[bars] : null);
        }
    }

    private WaveformStore Store(FakeAnalyzer analyzer) => new(new LibraryFolder(Path.Combine(root, "library")), analyzer);

    private async Task<Track> AddAsync(string name, bool fileExists = true)
    {
        var path = Path.Combine(root, $"{name}.mp3");
        if (fileExists)
        {
            await File.WriteAllTextAsync(path, "x");
        }

        var track = new Track { Title = name, FilePath = path };
        await tracks.AddAsync(track);
        return track;
    }

    [Fact]
    public async Task Run_AnalysesEveryTrackWithoutAWaveform()
    {
        var a = await AddAsync("A");
        var b = await AddAsync("B");
        var store = Store(new FakeAnalyzer(_ => true));

        var created = await new WaveformBackfillService(tracks, store).RunAsync();

        Assert.Equal(2, created);
        Assert.True(store.Has(a.Id));
        Assert.True(store.Has(b.Id));
    }

    [Fact]
    public async Task Run_ResumesAfterRestart_SkippingTracksThatAlreadyHaveOne()
    {
        await AddAsync("A");
        var analyzer = new FakeAnalyzer(_ => true);
        var store = Store(analyzer);
        await new WaveformBackfillService(tracks, store).RunAsync();
        await AddAsync("B");

        var created = await new WaveformBackfillService(tracks, store).RunAsync();

        Assert.Equal(1, created);
        Assert.Equal(2, analyzer.Calls);
    }

    [Fact]
    public async Task Run_TriesAFailingTrackOnlyOnce_AndSkipsMissingFiles()
    {
        var bad = await AddAsync("Bad"); await File.WriteAllTextAsync(bad.FilePath, "bad");
        await AddAsync("Gone", fileExists: false);
        var good = await AddAsync("Good");
        var analyzer = new FakeAnalyzer(path => File.ReadAllText(path) != "bad");
        var store = Store(analyzer);

        var created = await new WaveformBackfillService(tracks, store).RunAsync();

        Assert.Equal(1, created);
        Assert.Equal(2, analyzer.Calls);
        Assert.True(store.Has(good.Id));
    }

    [Fact]
    public async Task Run_StopsWhenCancelled()
    {
        await AddAsync("A");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WaveformBackfillService(tracks, Store(new FakeAnalyzer(_ => true))).RunAsync(cts.Token));
    }
}
