using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Import;

/// <summary>
/// Analyses the waveform of every track that has none, one at a time, on one owned background loop. Progress is the
/// cache folder itself, so after an app restart it simply continues with the tracks still missing a waveform.
/// </summary>
public sealed class WaveformBackfillService(IRepository<Track> tracks, WaveformStore waveforms) : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly HashSet<Guid> failed = [];
    private Task? loop;

    /// <summary>Starts the loop unless it is already running. Tracks that could not be analysed are not retried in this session.</summary>
    public void Start()
    {
        lock (gate)
        {
            if (loop is null || loop.IsCompleted)
            {
                loop = RunAsync(stopping.Token);
            }
        }
    }

    /// <summary>Analyses the missing waveforms and returns how many were created.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var created = 0;
        while (await NextAsync() is { } track)
        {
            try
            {
                if (await waveforms.GetAsync(track.Id, track.FilePath, cancellationToken: cancellationToken) is not null)
                {
                    created++;
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable file or a concurrent write of the same cache file: skip it for this session.
            }

            failed.Add(track.Id);
        }

        return created;
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        if (loop is not null)
        {
            try
            {
#pragma warning disable VSTHRD003 // The loop is started and owned by this class.
                await loop;
#pragma warning restore VSTHRD003
            }
            catch (OperationCanceledException)
            {
                // Stopped while analysing.
            }
        }

        stopping.Dispose();
    }

    private async Task<Track?> NextAsync() =>
        (await tracks.GetAllAsync()).FirstOrDefault(track => !failed.Contains(track.Id) && !waveforms.Has(track.Id) && File.Exists(track.FilePath));
}
