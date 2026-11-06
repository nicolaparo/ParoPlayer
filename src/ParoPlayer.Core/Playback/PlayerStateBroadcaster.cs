namespace ParoPlayer.Core.Playback;

/// <summary>
/// Polls a platform player and awaits every subscriber whenever the snapshot changes. Platform players only
/// expose position by reading it, and callbacks from Java must not start fire-and-forget tasks, so one owned loop
/// does all the notifying. The loop starts with the first subscriber and is awaited by <see cref="DisposeAsync"/>.
/// </summary>
public sealed class PlayerStateBroadcaster(Func<CancellationToken, Task<PlayerSnapshot>> read, TimeProvider timeProvider, TimeSpan interval) : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource stopping = new();
    private readonly List<Func<PlayerSnapshot, Task>> handlers = [];
    private PlayerSnapshot? last;
    private Task? loop;

    public IDisposable Subscribe(Func<PlayerSnapshot, Task> handler)
    {
        lock (gate)
        {
            handlers.Add(handler);
            loop ??= RunAsync(stopping.Token);
        }

        return new Subscription(this, handler);
    }

    /// <summary>Reads the player now and notifies subscribers if anything changed since the last read.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Func<PlayerSnapshot, Task>[] targets;
        lock (gate)
        {
            targets = [.. handlers];
        }

        if (targets.Length == 0)
        {
            return;
        }

        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await read(cancellationToken);
            if (snapshot == last)
            {
                return;
            }

            last = snapshot;
            foreach (var target in targets)
            {
                await target(snapshot);
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        if (loop is not null)
        {
#pragma warning disable VSTHRD003 // The loop is started and owned by this class.
            await loop;
#pragma warning restore VSTHRD003
        }

        stopping.Dispose();
        refreshGate.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await RefreshAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failing subscriber or a transient read error must not stop the ticker; retry on the next tick.
                    last = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    private void Unsubscribe(Func<PlayerSnapshot, Task> handler)
    {
        lock (gate)
        {
            handlers.Remove(handler);
        }
    }

    private sealed class Subscription(PlayerStateBroadcaster owner, Func<PlayerSnapshot, Task> handler) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(handler);
    }
}
