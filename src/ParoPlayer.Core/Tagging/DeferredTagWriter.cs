using ParoPlayer.Core.Bpm;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Playback;

namespace ParoPlayer.Core.Tagging;

/// <summary>Changes waiting to be saved to the tags of one audio file. A null part means "no change".</summary>
public record PendingTagChange(TrackTags? Tags = null, byte[]? Cover = null, int? Bpm = null)
{
    /// <summary>This change with <paramref name="newer"/> applied on top of it.</summary>
    public PendingTagChange Then(PendingTagChange newer) =>
        new(newer.Tags ?? Tags, newer.Cover ?? Cover, newer.Bpm ?? Bpm);
}

/// <summary>
/// Saves tag changes without touching the file the player has loaded: saving a tag can rewrite the whole file, which
/// would glitch or break playback. A change for another file is written at once. A change for the current track is
/// kept in memory and written when the player moves on (or stops). Anything still pending when the app closes is
/// lost; the database already has the edit and BPM is simply estimated again.
/// </summary>
public sealed class DeferredTagWriter : IDisposable
{
    private readonly IPlayerService player;
    private readonly ITrackTagWriter tagWriter;
    private readonly IBpmTag bpmTag;
    private readonly Lock gate = new();
    private readonly Dictionary<string, PendingTagChange> pending = [];
    private readonly SemaphoreSlim writing = new(1, 1);
    private readonly IDisposable subscription;

    public DeferredTagWriter(IPlayerService player, ITrackTagWriter tagWriter, IBpmTag bpmTag)
    {
        this.player = player;
        this.tagWriter = tagWriter;
        this.bpmTag = bpmTag;
        subscription = player.Subscribe(snapshot => FlushAsync(snapshot.Current?.FilePath));
    }

    /// <summary>Number of files with changes still waiting.</summary>
    public int PendingCount
    {
        get
        {
            lock (gate)
            {
                return pending.Count;
            }
        }
    }

    /// <summary>
    /// Saves <paramref name="change"/> now, or keeps it until the file is no longer the playing one. Returns an error
    /// message when a write that was attempted now failed, null otherwise (including when the write was deferred).
    /// </summary>
    public async Task<string?> WriteAsync(string filePath, PendingTagChange change, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            pending[filePath] = pending.TryGetValue(filePath, out var earlier) ? earlier.Then(change) : change;
        }

        var playing = (await player.GetSnapshotAsync(cancellationToken)).Current?.FilePath;
        return filePath == playing ? null : await FlushFileAsync(filePath, cancellationToken);
    }

    /// <summary>Writes every pending change except the ones for <paramref name="playingPath"/>.</summary>
    public async Task FlushAsync(string? playingPath, CancellationToken cancellationToken = default)
    {
        string[] files;
        lock (gate)
        {
            files = [.. pending.Keys.Where(file => file != playingPath)];
        }

        foreach (var file in files)
        {
            await FlushFileAsync(file, cancellationToken);
        }
    }

    public void Dispose()
    {
        subscription.Dispose();
        writing.Dispose();
    }

    private async Task<string?> FlushFileAsync(string filePath, CancellationToken cancellationToken)
    {
        await writing.WaitAsync(cancellationToken);
        try
        {
            PendingTagChange? change;
            lock (gate)
            {
                if (!pending.Remove(filePath, out change))
                {
                    return null;
                }
            }

            try
            {
                if (change.Tags is not null)
                {
                    await tagWriter.WriteAsync(filePath, change.Tags, change.Cover, cancellationToken);
                }

                if (change.Bpm is { } bpm)
                {
                    await bpmTag.WriteAsync(filePath, bpm, cancellationToken);
                }

                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TagLib.UnsupportedFormatException or TagLib.CorruptFileException)
            {
                return ex.Message;
            }
        }
        finally
        {
            writing.Release();
        }
    }
}
