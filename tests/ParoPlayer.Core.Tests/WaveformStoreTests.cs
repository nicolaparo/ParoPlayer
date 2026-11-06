using ParoPlayer.Core.Import;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Tests;

public sealed class WaveformStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly LibraryFolder folder;
    private readonly string audio;

    public WaveformStoreTests()
    {
        folder = new LibraryFolder(Path.Combine(root, "library"));
        Directory.CreateDirectory(root);
        audio = Path.Combine(root, "song.mp3");
        File.WriteAllText(audio, "audio");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeAnalyzer(float[]? result) : IWaveformAnalyzer
    {
        public int Calls { get; private set; }

        public Task<float[]?> AnalyzeAsync(string filePath, int bars, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private static float[] Peaks() => [.. Enumerable.Range(0, WaveformStore.Bars).Select(i => i / (float)WaveformStore.Bars)];

    [Fact]
    public async Task Get_ComputesOnceThenReadsFromCache()
    {
        var analyzer = new FakeAnalyzer(Peaks());
        var store = new WaveformStore(folder, analyzer);
        var id = Guid.NewGuid();

        var first = await store.GetAsync(id, audio);
        var second = await store.GetAsync(id, audio);

        Assert.Equal(Peaks(), first);
        Assert.Equal(Peaks(), second);
        Assert.Equal(1, analyzer.Calls);
    }

    [Fact]
    public async Task Get_WhenAnalyzerFails_ReturnsNullAndCachesNothing()
    {
        var store = new WaveformStore(folder, new FakeAnalyzer(null));

        Assert.Null(await store.GetAsync(Guid.NewGuid(), audio));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, WaveformStore.FolderName)));
    }

    [Fact]
    public async Task Get_WhenAudioIsMissing_ReturnsNullWithoutAnalysing()
    {
        var analyzer = new FakeAnalyzer(Peaks());
        var store = new WaveformStore(folder, analyzer);

        Assert.Null(await store.GetAsync(Guid.NewGuid(), Path.Combine(root, "missing.mp3")));
        Assert.Equal(0, analyzer.Calls);
    }

    [Fact]
    public async Task Get_WithCorruptCache_ComputesAgain()
    {
        var analyzer = new FakeAnalyzer(Peaks());
        var store = new WaveformStore(folder, analyzer);
        var id = Guid.NewGuid();
        var directory = Path.Combine(folder.Path, WaveformStore.FolderName);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"{id:N}.json"), "{not json");

        Assert.Equal(Peaks(), await store.GetAsync(id, audio));
        Assert.Equal(1, analyzer.Calls);
    }
}
