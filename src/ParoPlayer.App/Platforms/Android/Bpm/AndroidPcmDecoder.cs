using Android.Media;
using ParoPlayer.Core.Bpm;

namespace ParoPlayer.App.Bpm;

/// <summary>Decodes up to a given number of seconds from the middle of the track (skipping the intro) with MediaExtractor and MediaCodec, downmixed to mono.</summary>
public sealed class AndroidPcmDecoder : IPcmDecoder
{
    private const long TimeoutUs = 10_000;

    public Task<PcmAudio?> DecodeAsync(string filePath, int maxSeconds, CancellationToken cancellationToken = default) =>
        Task.Run(() => Decode(filePath, maxSeconds, cancellationToken), cancellationToken);

    private static PcmAudio? Decode(string filePath, int maxSeconds, CancellationToken cancellationToken)
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

        if (format is null)
        {
            return null;
        }

        extractor.SelectTrack(track);
        if (format.ContainsKey(MediaFormat.KeyDuration))
        {
            var durationUs = format.GetLong(MediaFormat.KeyDuration);
            if (durationUs > maxSeconds * 2_000_000L)
            {
                extractor.SeekTo(durationUs / 4, MediaExtractorSeekTo.ClosestSync);
            }
        }

        using var codec = MediaCodec.CreateDecoderByType(format.GetString(MediaFormat.KeyMime)!);
        codec.Configure(format, null, null, MediaCodecConfigFlags.None);
        codec.Start();

        var samples = new List<float>(maxSeconds * 44_100);
        var channels = 2;
        var sampleRate = 44_100;
        using var info = new MediaCodec.BufferInfo();
        var inputDone = false;
        var outputDone = false;

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
                var chunk = new short[info.Size / 2];
                buffer.AsShortBuffer()!.Get(chunk);
                for (var frame = 0; frame + channels <= chunk.Length; frame += channels)
                {
                    var sum = 0;
                    for (var channel = 0; channel < channels; channel++)
                    {
                        sum += chunk[frame + channel];
                    }

                    samples.Add(sum / (channels * 32768f));
                }
            }

            outputDone = info.Flags.HasFlag(MediaCodecBufferFlags.EndOfStream) || samples.Count >= maxSeconds * sampleRate;
            codec.ReleaseOutputBuffer(output, false);
        }

        codec.Stop();
        return samples.Count == 0 ? null : new PcmAudio([.. samples], sampleRate);
    }
}
