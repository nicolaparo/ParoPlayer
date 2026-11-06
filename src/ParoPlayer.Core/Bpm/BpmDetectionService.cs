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

    /// <summary>
    /// Returns the BPM of the file: the existing tag, or a newly detected value (null when none could be found).
    /// A detected value is written to the tag only when <paramref name="persist"/> is true: do not persist for the
    /// track that is playing, because saving a tag can rewrite the file the player is reading.
    /// </summary>
    public async Task<int?> DetectAsync(string filePath, bool overwrite = false, bool persist = true, CancellationToken cancellationToken = default)
    {
        if (!overwrite)
        {
            var existing = await tag.ReadAsync(filePath, cancellationToken);
            if (existing > 0)
            {
                return existing;
            }
        }

        // Decode a throwaway copy so analysis never touches the file the player is reading.
        var copy = Path.Combine(Path.GetTempPath(), $"paroplayer-bpm-{Guid.NewGuid():N}{Path.GetExtension(filePath)}");
        PcmAudio? audio;
        try
        {
            File.Copy(filePath, copy);
            audio = await decoder.DecodeAsync(copy, AnalysisSeconds, cancellationToken);
        }
        finally
        {
            File.Delete(copy);
        }

        if (audio is null)
        {
            return null;
        }

        var bpm = await Task.Run(() => BpmEstimator.Estimate(audio.Samples, audio.SampleRate), cancellationToken);
        if (bpm is null)
        {
            return null;
        }

        if (persist)
        {
            await tag.WriteAsync(filePath, bpm.Value, cancellationToken);
        }

        return bpm;
    }
}
