namespace ParoPlayer.Core.Bpm;

/// <summary>Decodes a throwaway copy of a file so analysis never touches the file the player is reading.</summary>
public static class PcmAudioLoader
{
    public static async Task<PcmAudio?> LoadAsync(IPcmDecoder decoder, string filePath, int maxSeconds, CancellationToken cancellationToken = default)
    {
        var copy = Path.Combine(Path.GetTempPath(), $"paroplayer-pcm-{Guid.NewGuid():N}{Path.GetExtension(filePath)}");
        try
        {
            File.Copy(filePath, copy);
            return await decoder.DecodeAsync(copy, maxSeconds, cancellationToken);
        }
        finally
        {
            File.Delete(copy);
        }
    }
}
