using ParoPlayer.Core.Bpm;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Harmony;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Tests;

public sealed class KeyEstimatorTests
{
    private const int SampleRate = 22_050;

    /// <summary>Sines for (MIDI note, amplitude) pairs, shifted by <paramref name="transpose"/> semitones.</summary>
    private static float[] Render((int Note, double Amplitude)[] notes, int transpose = 0, int seconds = 10)
    {
        var samples = new float[SampleRate * seconds];
        foreach (var (note, amplitude) in notes)
        {
            var hz = 440 * Math.Pow(2, (note + transpose - 69) / 12.0);
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] += (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / SampleRate));
            }
        }

        return samples;
    }

    // C4 = 60. Tonic-weighted triads with the tonic doubled an octave below.
    private static readonly (int, double)[] CMajor = [(48, 0.3), (60, 0.3), (64, 0.15), (67, 0.2)];
    private static readonly (int, double)[] AMinor = [(45, 0.3), (57, 0.3), (60, 0.15), (64, 0.2)];

    [Fact]
    public void Estimate_CMajorTriad_ReturnsC() =>
        Assert.Equal("C", KeyEstimator.Estimate(Render(CMajor), SampleRate));

    [Fact]
    public void Estimate_AMinorTriad_ReturnsAm() =>
        Assert.Equal("Am", KeyEstimator.Estimate(Render(AMinor), SampleRate));

    [Theory]
    [InlineData(1, "C#")]
    [InlineData(2, "D")]
    [InlineData(7, "G")]
    [InlineData(11, "B")]
    public void Estimate_TransposedMajor_ShiftsTheKey(int semitones, string expected) =>
        Assert.Equal(expected, KeyEstimator.Estimate(Render(CMajor, semitones), SampleRate));

    [Theory]
    [InlineData(1, "A#m")]
    [InlineData(4, "C#m")]
    [InlineData(5, "Dm")]
    public void Estimate_TransposedMinor_ShiftsTheKey(int semitones, string expected) =>
        Assert.Equal(expected,KeyEstimator.Estimate(Render(AMinor, semitones), SampleRate));

    [Fact]
    public void Estimate_Silence_ReturnsNull() =>
        Assert.Null(KeyEstimator.Estimate(new float[SampleRate * 10], SampleRate));

    [Fact]
    public void Estimate_TooShort_ReturnsNull() =>
        Assert.Null(KeyEstimator.Estimate(Render(CMajor, seconds: 1).AsSpan(0, 100), SampleRate));

    [Fact]
    public async Task Detect_DecodesAndEstimates()
    {
        var path = Path.GetTempFileName();
        try
        {
            var service = new KeyDetectionService(new FakeDecoder(Render(CMajor)));

            Assert.Equal("C", await service.DetectAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TrackKeyService_DetectsOnceAndStoresTheKey()
    {
        var path = Path.GetTempFileName();
        try
        {
            var repository = new InMemoryRepository<Track>();
            var track = new Track { FilePath = path };
            await repository.AddAsync(track);
            var decoder = new FakeDecoder(Render(AMinor));
            var service = new TrackKeyService(repository, new KeyDetectionService(decoder));

            Assert.Equal("Am", await service.GetAsync(track));
            Assert.Equal("Am", await service.GetAsync(track));

            Assert.Equal(1, decoder.Calls);
            Assert.Equal("Am", (await repository.GetAsync(track.Id))!.Key);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Backfill_AnalysesOnlyTracksWithoutKey()
    {
        var path = Path.GetTempFileName();
        try
        {
            var repository = new InMemoryRepository<Track>();
            await repository.AddAsync(new Track { FilePath = path });
            await repository.AddAsync(new Track { FilePath = path, Key = "D" });
            var decoder = new FakeDecoder(Render(CMajor));
            var backfill = new KeyBackfillService(repository, new TrackKeyService(repository, new KeyDetectionService(decoder)));

            Assert.Equal(1, await backfill.RunAsync());
            Assert.Equal(1, decoder.Calls);
            Assert.All(await repository.GetAllAsync(), track => Assert.NotNull(track.Key));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FakeDecoder(float[] samples) : IPcmDecoder
    {
        public int Calls { get; private set; }

        public Task<PcmAudio?> DecodeAsync(string filePath, int maxSeconds, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<PcmAudio?>(new PcmAudio(samples, SampleRate));
        }
    }
}
