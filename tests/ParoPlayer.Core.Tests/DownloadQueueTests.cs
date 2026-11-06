using ParoPlayer.Core.Download;

namespace ParoPlayer.Core.Tests;

public sealed class DownloadQueueTests : IAsyncDisposable
{
    private readonly DownloadHarness harness = new();

    public async ValueTask DisposeAsync()
    {
        harness.Source.Gate?.TrySetResult();
        await harness.Queue.DisposeAsync();
        harness.Dispose();
    }

    private DownloadItem Item(string id) => harness.Queue.Items.Single(item => item.Media.Id == id);

    [Fact]
    public async Task Enqueue_DownloadsInOrder_AndMarksDone()
    {
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("b"));
        await harness.Queue.WaitIdleAsync();

        Assert.Equal(["a", "b"], harness.Source.Downloaded);
        Assert.All(harness.Queue.Items, item => Assert.Equal((DownloadState.Done, 1d), (item.State, item.Progress)));
        Assert.All(harness.Queue.Items, item => Assert.NotNull(item.TrackId));
        Assert.Equal(2, (await harness.Library.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Enqueue_SameMediaWhileWaiting_IsIgnored_ButAllowedAfterFinishing()
    {
        harness.Source.Gate = new TaskCompletionSource();

        Assert.True(await harness.Queue.EnqueueAsync(DownloadHarness.Media("a")));
        Assert.False(await harness.Queue.EnqueueAsync(DownloadHarness.Media("a")));
        harness.Source.Gate.SetResult();
        await harness.Queue.WaitIdleAsync();

        Assert.True(await harness.Queue.EnqueueAsync(DownloadHarness.Media("a")));
    }

    [Fact]
    public async Task Failure_IsRecorded_AndNextItemStillRuns()
    {
        harness.Source.DownloadError = new MediaSourceException("Download failed: offline");
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.WaitIdleAsync();
        harness.Source.DownloadError = null;
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("b"));
        await harness.Queue.WaitIdleAsync();

        Assert.Equal((DownloadState.Failed, "Download failed: offline"), (Item("a").State, Item("a").Message));
        Assert.Equal(DownloadState.Done, Item("b").State);
    }

    [Fact]
    public async Task Timeout_IsAFailure_NotACancellation()
    {
        harness.Source.DownloadError = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.WaitIdleAsync();

        Assert.Equal(DownloadState.Failed, Item("a").State);
    }

    [Fact]
    public async Task Retry_RunsFailedItemAgain()
    {
        harness.Source.DownloadError = new MediaSourceException("offline");
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.WaitIdleAsync();
        harness.Source.DownloadError = null;

        await harness.Queue.RetryAsync(Item("a").Id);
        await harness.Queue.WaitIdleAsync();

        Assert.Equal(DownloadState.Done, Item("a").State);
        Assert.Null(Item("a").Message);
    }

    [Fact]
    public async Task Cancel_RunningItem_StopsIt_AndLetsNextOneRun()
    {
        harness.Source.Gate = new TaskCompletionSource();
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("b"));
        await WaitForAsync(() => harness.Source.Downloaded.Contains("a"));

        await harness.Queue.CancelAsync(Item("a").Id);
        await WaitForAsync(() => Item("a").State == DownloadState.Canceled);
        harness.Source.Gate.SetResult();
        await harness.Queue.WaitIdleAsync();

        Assert.Equal(DownloadState.Canceled, Item("a").State);
        Assert.Equal(DownloadState.Done, Item("b").State);
        Assert.Single(await harness.Library.GetAllAsync());
    }

    [Fact]
    public async Task Cancel_QueuedItem_SkipsIt()
    {
        harness.Source.Gate = new TaskCompletionSource();
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("b"));
        await WaitForAsync(() => harness.Source.Downloaded.Contains("a"));

        await harness.Queue.CancelAsync(Item("b").Id);
        harness.Source.Gate.SetResult();
        await harness.Queue.WaitIdleAsync();

        Assert.Equal(["a"], harness.Source.Downloaded);
        Assert.Equal(DownloadState.Canceled, Item("b").State);
        Assert.Equal(DownloadState.Done, Item("a").State);
    }

    [Fact]
    public async Task Policy_Blocked_FailsItemWithReason_WithoutDownloading()
    {
        harness.Policy.Reason = "Connect to Wi-Fi";

        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.WaitIdleAsync();

        Assert.Empty(harness.Source.Downloaded);
        Assert.Equal((DownloadState.Failed, "Connect to Wi-Fi"), (Item("a").State, Item("a").Message));
    }

    [Fact]
    public async Task ClearFinished_KeepsOnlyUnfinishedItems()
    {
        harness.Source.Gate = new TaskCompletionSource();
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.CancelAsync(Item("a").Id);
        await WaitForAsync(() => harness.Queue.Items.Single().IsFinished);
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("b"));

        await harness.Queue.ClearFinishedAsync();

        Assert.Equal(["b"], harness.Queue.Items.Select(item => item.Media.Id));
    }

    [Fact]
    public async Task KeepAlive_BeginsWhenTheWorkerStarts_NotForEveryItem()
    {
        harness.Source.Gate = new TaskCompletionSource();
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("b"));
        harness.Source.Gate.SetResult();
        await harness.Queue.WaitIdleAsync();

        Assert.Equal(1, harness.KeepAlive.Begins);
    }

    [Fact]
    public async Task Subscribers_AreNotifiedOnStateChanges_UntilDisposed()
    {
        var notifications = 0;
        var subscription = harness.Queue.Subscribe(() =>
        {
            notifications++;
            return Task.CompletedTask;
        });

        await harness.Queue.EnqueueAsync(DownloadHarness.Media("a"));
        await harness.Queue.WaitIdleAsync();
        var seen = notifications;
        subscription.Dispose();
        await harness.Queue.EnqueueAsync(DownloadHarness.Media("b"));
        await harness.Queue.WaitIdleAsync();

        Assert.True(seen >= 3);
        Assert.Equal(seen, notifications);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
