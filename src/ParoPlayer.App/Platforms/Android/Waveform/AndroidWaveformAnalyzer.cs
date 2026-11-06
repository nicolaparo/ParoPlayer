using Android.Media;
using ParoPlayer.Core.Import;

namespace ParoPlayer.App.Waveform;

/// <summary>Decodes the audio with MediaExtractor and MediaCodec and keeps the RMS loudness of each time slice (a peak saturates on loud tracks).</summary>
public sealed class AndroidWaveformAnalyzer : IWaveformAnalyzer
{
    private const long TimeoutUs = 10_000;

    private static readonly TimeSpan PartialInterval = TimeSpan.FromMilliseconds(200);

    public Task<float[]?> AnalyzeAsync(string filePath, int bars, Action<float[]>? partial = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(filePath, bars, partial, cancellationToken), cancellationToken);

    private static float[]? Analyze(string filePath, int bars, Action<float[]>? partial, CancellationToken cancellationToken)
    {
        using var extractor = new MediaExtractor();
        extractor.SetDataSource(filePath);

        var track = -1;
        MediaFormat? format = null;
        for (var i = 0; i < extractor.TrackCount; i++)
        {
            var candidate = extractor.GetTrackFormat(i);
            if (candidate?.GetString(MediaFormat.KeyMime)?.StartsWith("audio/", StringComparison.Ordinal) == true)
            {
                track = i;
                format = candidate;
                break;
            }
        }

        if (format is null || !format.ContainsKey(MediaFormat.KeyDuration))
        {
            return null;
        }

        var durationUs = format.GetLong(MediaFormat.KeyDuration);
        if (durationUs <= 0)
        {
            return null;
        }

        extractor.SelectTrack(track);
        using var codec = MediaCodec.CreateDecoderByType(format.GetString(MediaFormat.KeyMime)!);
        codec.Configure(format, null, null, MediaCodecConfigFlags.None);
        codec.Start();

        var sums = new double[bars];
        var counts = new long[bars];
        var channels = 2;
        var sampleRate = 44_100;
        using var info = new MediaCodec.BufferInfo();
        var inputDone = false;
        var outputDone = false;
        var lastReport = DateTime.MinValue;

        while (!outputDone)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!inputDone)
            {
                var index = codec.DequeueInputBuffer(TimeoutUs);
                if (index >= 0)
                {
                    var size = extractor.ReadSampleData(codec.GetInputBuffer(index)!, 0);
                    if (size < 0)
                    {
                        codec.QueueInputBuffer(index, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                        inputDone = true;
                    }
                    else
                    {
                        codec.QueueInputBuffer(index, 0, size, extractor.SampleTime, 0);
                        extractor.Advance();
                    }
                }
            }

            var output = codec.DequeueOutputBuffer(info, TimeoutUs);
            if (output == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                var decoded = codec.OutputFormat;
                channels = Math.Max(1, decoded.GetInteger(MediaFormat.KeyChannelCount));
                sampleRate = Math.Max(1, decoded.GetInteger(MediaFormat.KeySampleRate));
                continue;
            }

            if (output < 0)
            {
                continue;
            }

            if (info.Size > 0)
            {
                var buffer = codec.GetOutputBuffer(output)!;
                buffer.Position(info.Offset);
                buffer.Limit(info.Offset + info.Size);
                Accumulate(buffer.AsShortBuffer()!, info.PresentationTimeUs, durationUs, channels, sampleRate, sums, counts);
                if (partial is not null && DateTime.UtcNow - lastReport > PartialInterval)
                {
                    lastReport = DateTime.UtcNow;
                    var reached = (int)Math.Clamp(info.PresentationTimeUs * bars / durationUs, 0, bars - 1);
                    partial(Levels(sums, counts, reached));
                }
            }

            outputDone = info.Flags.HasFlag(MediaCodecBufferFlags.EndOfStream);
            codec.ReleaseOutputBuffer(output, false);
        }

        codec.Stop();

        var levels = sums.Select((sum, bar) => counts[bar] == 0 ? 0f : (float)Math.Sqrt(sum / counts[bar])).ToArray();
        var max = levels.Max();
        return max <= 0 ? null : [.. levels.Select(level => level / max)];
    }

    /// <summary>The bars before <paramref name="reached"/> (the one still filling is left out), scaled by the loudest so far; the rest are unknown.</summary>
    private static float[] Levels(double[] sums, long[] counts, int reached)
    {
        var levels = new float[sums.Length];
        var max = 0f;
        for (var bar = 0; bar < levels.Length; bar++)
        {
            if (bar < reached && counts[bar] > 0)
            {
                levels[bar] = (float)Math.Sqrt(sums[bar] / counts[bar]);
                max = Math.Max(max, levels[bar]);
            }
            else
            {
                levels[bar] = WaveformStore.Unknown;
            }
        }

        if (max > 0)
        {
            for (var bar = 0; bar < reached; bar++)
            {
                levels[bar] = Math.Max(levels[bar], 0f) / max;
            }
        }

        return levels;
    }

    private static void Accumulate(Java.Nio.ShortBuffer samples, long timeUs, long durationUs, int channels, int sampleRate, double[] sums, long[] counts)
    {
        var chunk = new short[samples.Remaining()];
        samples.Get(chunk);
        var frames = chunk.Length / channels;
        for (var frame = 0; frame < frames; frame++)
        {
            var at = timeUs + frame * 1_000_000L / sampleRate;
            var bar = (int)Math.Clamp(at * sums.Length / durationUs, 0, sums.Length - 1);
            for (var channel = 0; channel < channels; channel++)
            {
                var level = Math.Abs((int)chunk[frame * channels + channel]) / 32768f;
                sums[bar] += level * level;
                counts[bar]++;
            }
        }
    }
}
