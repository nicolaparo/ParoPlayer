using System.Text.Json;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Import;

/// <summary>Computes the loudness peaks of an audio file (platform specific).</summary>
public interface IWaveformAnalyzer
{
    /// <summary>Returns <paramref name="bars"/> peaks between 0 and 1, or null when the file cannot be analysed.</summary>
    Task<float[]?> AnalyzeAsync(string filePath, int bars, CancellationToken cancellationToken = default);
}

/// <summary>Used where there is no decoder (Windows preview). The UI shows its placeholder.</summary>
public sealed class NoWaveformAnalyzer : IWaveformAnalyzer
{
    public Task<float[]?> AnalyzeAsync(string filePath, int bars, CancellationToken cancellationToken = default) =>
        Task.FromResult<float[]?>(null);
}

/// <summary>Peaks per track, computed once and cached as a small JSON file in the library folder.</summary>
public class WaveformStore(ILibraryFolder folder, IWaveformAnalyzer analyzer)
{
    public const int Bars = 150;
    public const string FolderName = "waveforms";

    private string PathFor(Guid trackId) => Path.Combine(folder.Path, FolderName, $"{trackId:N}.json");

    public bool Has(Guid trackId) => File.Exists(PathFor(trackId));

    /// <summary>Removes the cached peaks of a deleted track.</summary>
    public void Delete(Guid trackId)
    {
        var path = PathFor(trackId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async Task<float[]?> GetAsync(Guid trackId, string audioPath, CancellationToken cancellationToken = default)
    {
        var path = PathFor(trackId);
        if (File.Exists(path))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<float[]>(await File.ReadAllBytesAsync(path, cancellationToken));
                if (cached is { Length: Bars })
                {
                    return cached;
                }
            }
            catch (JsonException)
            {
                // Corrupt cache: compute again below.
            }
        }

        if (!File.Exists(audioPath))
        {
            return null;
        }

        // Analyse a throwaway copy so decoding never touches the file the player is reading.
        var copy = Path.Combine(Path.GetTempPath(), $"paroplayer-waveform-{Guid.NewGuid():N}{Path.GetExtension(audioPath)}");
        float[]? peaks;
        try
        {
            File.Copy(audioPath, copy);
            peaks = await analyzer.AnalyzeAsync(copy, Bars, cancellationToken);
        }
        finally
        {
            File.Delete(copy);
        }

        if (peaks is not { Length: Bars })
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(peaks), cancellationToken);
        return peaks;
    }
}
