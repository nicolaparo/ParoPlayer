using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Harmony;

/// <summary>
/// Detects the key of every track that has none, one at a time, on one owned background loop. Progress is the Key
/// column itself, so after an app restart it continues with the tracks still missing a key.
/// </summary>
public sealed class KeyBackfillService(IRepository<Track> tracks, TrackKeyService keys) : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly HashSet<Guid> failed = [];
    private Task? loop;

    /// <summary>Starts the loop unless it is already running. Tracks without a result are not retried in this session.</summary>
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

    /// <summary>Detects the missing keys and returns how many were found.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var found = 0;
        while (await NextAsync() is { } track)
        {
            try
            {
                if (await keys.GetAsync(track, cancellationToken) is not null)
                {
                    found++;
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable file: skip it for this session.
            }

            failed.Add(track.Id);
        }

        return found;
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
        (await tracks.GetAllAsync()).FirstOrDefault(track => track.Key is null && !failed.Contains(track.Id) && File.Exists(track.FilePath));
}
