using ParoPlayer.Core.Bpm;

namespace ParoPlayer.Core.Harmony;

/// <summary>Detects the musical key of an audio file from a stretch of decoded audio.</summary>
public class KeyDetectionService(IPcmDecoder decoder)
{
    public const int AnalysisSeconds = 60;

    /// <summary>The key such as "Am", or null when the file cannot be decoded or has no tonal content.</summary>
    public async Task<string?> DetectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var audio = await PcmAudioLoader.LoadAsync(decoder, filePath, AnalysisSeconds, cancellationToken);
        return audio is null ? null : await Task.Run(() => KeyEstimator.Estimate(audio.Samples, audio.SampleRate), cancellationToken);
    }
}
