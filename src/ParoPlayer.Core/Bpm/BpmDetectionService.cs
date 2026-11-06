namespace ParoPlayer.Core.Bpm;

/// <summary>Mono PCM audio, samples between -1 and 1.</summary>
public record PcmAudio(float[] Samples, int SampleRate);

/// <summary>Decodes an audio file to mono PCM (platform specific).</summary>
public interface IPcmDecoder
{
    /// <summary>Decodes at most <paramref name="maxSeconds"/> from the track, or returns null when the file cannot be decoded.</summary>
    Task<PcmAudio?> DecodeAsync(string filePath, int maxSeconds, CancellationToken cancellationToken = default);
}

/// <summary>Used where there is no decoder (Windows preview): detection finds nothing.</summary>
public sealed class NoPcmDecoder : IPcmDecoder
{
    public Task<PcmAudio?> DecodeAsync(string filePath, int maxSeconds, CancellationToken cancellationToken = default) =>
        Task.FromResult<PcmAudio?>(null);
}

/// <summary>Reads and writes the BPM tag of an audio file.</summary>
public interface IBpmTag
{
    /// <summary>The stored BPM, or 0 when the file has none.</summary>
    Task<int> ReadAsync(string filePath, CancellationToken cancellationToken = default);

    Task WriteAsync(string filePath, int bpm, CancellationToken cancellationToken = default);
}

public sealed class TagLibBpmTag : IBpmTag
{
    public Task<int> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var file = TagLib.File.Create(filePath);
            return (int)file.Tag.BeatsPerMinute;
        }, cancellationToken);

    public Task WriteAsync(string filePath, int bpm, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var file = TagLib.File.Create(filePath);
            file.Tag.BeatsPerMinute = (uint)Math.Max(bpm, 0);
            file.Save();
        }, cancellationToken);
}

/// <summary>Detects the BPM of a track that has none and writes it to the file tag. Call it from the tag editor or a bulk job.</summary>
public class BpmDetectionService(IPcmDecoder decoder, IBpmTag tag)
{
    public const int AnalysisSeconds = 60;

    /// <summary>Returns the BPM now stored in the file: the existing tag, a newly detected value, or null when none could be found.</summary>
    public async Task<int?> DetectAsync(string filePath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (!overwrite)
        {
            var existing = await tag.ReadAsync(filePath, cancellationToken);
            if (existing > 0)
            {
                return existing;
            }
        }

        var audio = await decoder.DecodeAsync(filePath, AnalysisSeconds, cancellationToken);
        if (audio is null)
        {
            return null;
        }

        var bpm = await Task.Run(() => BpmEstimator.Estimate(audio.Samples, audio.SampleRate), cancellationToken);
        if (bpm is null)
        {
            return null;
        }

        await tag.WriteAsync(filePath, bpm.Value, cancellationToken);
        return bpm;
    }
}
