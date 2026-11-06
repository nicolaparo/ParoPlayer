using ParoPlayer.Core.Bpm;

namespace ParoPlayer.Core.Tests;

public sealed class BpmEstimatorTests : IDisposable
{
    private const int SampleRate = 22_050;
    private readonly string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public BpmEstimatorTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    /// <summary>Decaying noise bursts at an exact tempo over a quiet noise floor.</summary>
    private static float[] ClickTrack(double bpm, int seconds)
    {
        var random = new Random(7);
        var samples = new float[SampleRate * seconds];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(random.NextDouble() - 0.5) * 0.01f;
        }

        var burst = SampleRate / 50;
        for (var beat = 0; ; beat++)
        {
            var start = (int)Math.Round(beat * 60.0 / bpm * SampleRate);
            if (start + burst >= samples.Length)
            {
                break;
            }

            for (var i = 0; i < burst; i++)
            {
                samples[start + i] += (float)((random.NextDouble() - 0.5) * Math.Exp(-6.0 * i / burst));
            }
        }

        return samples;
    }

    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(120)]
    [InlineData(128)]
    [InlineData(140)]
    [InlineData(174)]
    public void Estimate_ClickTrack_FindsTempo(int bpm)
    {
        var estimate = BpmEstimator.Estimate(ClickTrack(bpm, 30), SampleRate);

        Assert.NotNull(estimate);
        Assert.InRange(estimate.Value, bpm - 1, bpm + 1);
    }

    [Fact]
    public void Estimate_Silence_ReturnsNull() =>
        Assert.Null(BpmEstimator.Estimate(new float[SampleRate * 20], SampleRate));

    [Fact]
    public void Estimate_TooShort_ReturnsNull() =>
        Assert.Null(BpmEstimator.Estimate(ClickTrack(120, 1), SampleRate));

    [Fact]
    public async Task Detect_WritesBpmTagThatSurvivesReading()
    {
        var path = SilentMp3();
        var service = new BpmDetectionService(new FakeDecoder(ClickTrack(120, 30)), new TagLibBpmTag());

        var bpm = await service.DetectAsync(path);

        Assert.InRange(bpm!.Value, 119, 121);
        Assert.Equal(bpm.Value, await new TagLibBpmTag().ReadAsync(path));
    }

    [Fact]
    public async Task Detect_ExistingTag_IsKeptUnlessOverwriting()
    {
        var path = SilentMp3();
        await new TagLibBpmTag().WriteAsync(path, 90);
        var service = new BpmDetectionService(new FakeDecoder(ClickTrack(120, 30)), new TagLibBpmTag());

        Assert.Equal(90, await service.DetectAsync(path));
        Assert.InRange((await service.DetectAsync(path, overwrite: true))!.Value, 119, 121);
    }

    [Fact]
    public async Task Detect_WithoutPersist_ReturnsBpmButLeavesFileUntouched()
    {
        var path = SilentMp3();
        var before = await File.ReadAllBytesAsync(path);
        var service = new BpmDetectionService(new FakeDecoder(ClickTrack(120, 30)), new TagLibBpmTag());

        var bpm = await service.DetectAsync(path, persist: false);

        Assert.InRange(bpm!.Value, 119, 121);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Detect_UndecodableFile_ReturnsNullAndWritesNothing()
    {
        var path = SilentMp3();

        Assert.Null(await new BpmDetectionService(new NoPcmDecoder(), new TagLibBpmTag()).DetectAsync(path));
        Assert.Equal(0, await new TagLibBpmTag().ReadAsync(path));
    }

    private string SilentMp3()
    {
        var frame = new byte[417];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = 0x90;
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, Enumerable.Range(0, 8).SelectMany(_ => frame).ToArray());
        return path;
    }

    private sealed class FakeDecoder(float[] samples) : IPcmDecoder
    {
        public Task<PcmAudio?> DecodeAsync(string filePath, int maxSeconds, CancellationToken cancellationToken = default) =>
            Task.FromResult<PcmAudio?>(new PcmAudio(samples, SampleRate));
    }
}
