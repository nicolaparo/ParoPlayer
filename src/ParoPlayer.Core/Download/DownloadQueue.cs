namespace ParoPlayer.Core.Download;

/// <summary>
/// Runs downloads one at a time. State changes are awaited by subscribers right away; progress ticks are coalesced and
/// delivered by a timer loop, because progress callbacks are synchronous. The worker task is owned here (started with the
/// first queued item, stopped when the queue is idle) and awaited by <see cref="WaitIdleAsync"/> and <see cref="DisposeAsync"/>.
/// </summary>
public sealed class DownloadQueue(DownloadPipeline pipeline, IDownloadPolicy policy, IDownloadKeepAlive keepAlive, TimeProvider timeProvider) : IAsyncDisposable
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    private readonly Lock gate = new();
    private readonly List<DownloadItem> items = [];
    private readonly List<Func<Task>> handlers = [];
    private readonly CancellationTokenSource stopping = new();
    private Task? worker;
    private Guid? activeId;
    private CancellationTokenSource? activeCancellation;
    private bool progressDirty;

    public IReadOnlyList<DownloadItem> Items
    {
        get
        {
            lock (gate)
            {
                return [.. items];
            }
        }
    }

    public IDisposable Subscribe(Func<Task> handler)
    {
        lock (gate)
        {
            handlers.Add(handler);
        }

        return new Subscription(this, handler);
    }

    /// <summary>Adds the item to the end of the queue. Returns false when the same media is already waiting or running.</summary>
    public async Task<bool> EnqueueAsync(MediaInfo media)
    {
        lock (gate)
        {
            if (items.Any(item => item.Media.Id == media.Id && !item.IsFinished))
            {
                return false;
            }

            items.Add(new DownloadItem(Guid.NewGuid(), media));
            StartWorkerIfNeeded();
        }

        await NotifyAsync();
        return true;
    }

    /// <summary>Cancels a queued or running item.</summary>
    public async Task CancelAsync(Guid id)
    {
        var changed = false;
        lock (gate)
        {
            var item = Find(id);
            if (item is { State: DownloadState.Queued })
            {
                Replace(item with { State = DownloadState.Canceled, Message = "Canceled" });
                changed = true;
            }
            else if (item is { IsRunning: true } && activeId == id)
            {
                activeCancellation?.Cancel();
            }
        }

        if (changed)
        {
            await NotifyAsync();
        }
    }

    /// <summary>Queues a failed or canceled item again.</summary>
    public async Task RetryAsync(Guid id)
    {
        lock (gate)
        {
            if (Find(id) is not { State: DownloadState.Failed or DownloadState.Canceled } item)
            {
                return;
            }

            Replace(item with { State = DownloadState.Queued, Progress = 0, Message = null });
            StartWorkerIfNeeded();
        }

        await NotifyAsync();
    }

    public async Task ClearFinishedAsync()
    {
        lock (gate)
        {
            items.RemoveAll(item => item.IsFinished);
        }

        await NotifyAsync();
    }

    /// <summary>Completes when no download is running or waiting.</summary>
    public async Task WaitIdleAsync()
    {
        Task? current;
        lock (gate)
        {
            current = worker;
        }

        if (current is not null)
        {
            await current;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        await WaitIdleAsync();
        stopping.Dispose();
    }

    // Called with the lock held.
    private void StartWorkerIfNeeded()
    {
        if (worker is not null)
        {
            return;
        }

        keepAlive.Begin();
        worker = Task.Run(RunWorkerAsync);
    }

    private async Task RunWorkerAsync()
    {
        using var ticksStop = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
        var ticks = TickLoopAsync(ticksStop.Token);
        try
        {
            while (NextQueued() is var (item, cancellation) && item is not null)
            {
                using (cancellation)
                {
                    await ProcessAsync(item, cancellation!);
                }
            }
        }
        finally
        {
            await ticksStop.CancelAsync();
            await ticks;
            await NotifyAsync();
        }
    }

    // Takes the next queued item and marks it running, or ends the worker. Both happen under one lock so an item queued
    // right now either gets picked up by this loop or starts a new worker.
    private (DownloadItem? Item, CancellationTokenSource? Cancellation) NextQueued()
    {
        lock (gate)
        {
            if (!stopping.IsCancellationRequested && items.FirstOrDefault(item => item.State == DownloadState.Queued) is { } next)
            {
                var running = next with { State = DownloadState.Downloading };
                Replace(running);
                activeId = running.Id;
                activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                return (running, activeCancellation);
            }

            worker = null;
            return (null, null);
        }
    }

    private async Task ProcessAsync(DownloadItem item, CancellationTokenSource cancellation)
    {
        try
        {
            if (policy.BlockReason() is { } reason)
            {
                await FinishAsync(item.Id, DownloadState.Failed, reason);
                return;
            }

            await NotifyAsync();
            var outcome = await pipeline.RunAsync(item.Media, (state, progress) => Report(item.Id, state, progress), cancellation.Token);
            await FinishAsync(item.Id, DownloadState.Done, outcome.Note, outcome.Track.Id);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await FinishAsync(item.Id, DownloadState.Canceled, "Canceled");
        }
        catch (Exception ex)
        {
            // Last line of defence: whatever goes wrong with one item must not stop the queue.
            await FinishAsync(item.Id, DownloadState.Failed, ex.Message);
        }
        finally
        {
            lock (gate)
            {
                activeId = null;
                activeCancellation = null;
            }
        }
    }

    private void Report(Guid id, DownloadState state, double progress)
    {
        lock (gate)
        {
            if (Find(id) is { IsFinished: false } item)
            {
                Replace(item with { State = state, Progress = progress });
                progressDirty = true;
            }
        }
    }

    private async Task FinishAsync(Guid id, DownloadState state, string? message, Guid? trackId = null)
    {
        lock (gate)
        {
            if (Find(id) is { } item)
            {
                Replace(item with { State = state, Progress = state == DownloadState.Done ? 1 : item.Progress, Message = message, TrackId = trackId });
            }
        }

        await NotifyAsync();
    }

    private async Task TickLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ProgressInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                bool dirty;
                lock (gate)
                {
                    dirty = progressDirty;
                    progressDirty = false;
                }

                if (dirty)
                {
                    await NotifyAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The worker is done.
        }
    }

    private async Task NotifyAsync()
    {
        Func<Task>[] targets;
        lock (gate)
        {
            progressDirty = false;
            targets = [.. handlers];
        }

        foreach (var target in targets)
        {
            await target();
        }
    }

    // Called with the lock held.
    private DownloadItem? Find(Guid id) => items.FirstOrDefault(item => item.Id == id);

    private void Replace(DownloadItem item) => items[items.FindIndex(existing => existing.Id == item.Id)] = item;

    private sealed class Subscription(DownloadQueue queue, Func<Task> handler) : IDisposable
    {
        public void Dispose()
        {
            lock (queue.gate)
            {
                queue.handlers.Remove(handler);
            }
        }
    }
}
