using ParoPlayer.Core.Download;

namespace ParoPlayer.Core.Tests;

public sealed class DownloadRequestsTests : IAsyncDisposable
{
    private readonly DownloadHarness harness = new();
    private readonly DownloadRequests requests;

    public DownloadRequestsTests() => requests = new DownloadRequests(harness.Source, harness.Queue, harness.Policy);

    public async ValueTask DisposeAsync()
    {
        await harness.Queue.DisposeAsync();
        harness.Dispose();
    }

    [Fact]
    public async Task Link_QueuesEveryResolvedItem()
    {
        harness.Source.ResolveResult = [DownloadHarness.Media("a"), DownloadHarness.Media("b")];

        var result = await requests.SubmitAsync(" https://youtube.com/playlist?list=x ");
        await harness.Queue.WaitIdleAsync();

        Assert.Equal((2, null), (result.Queued, result.Error));
        Assert.Equal(2, harness.Queue.Items.Count);
    }

    [Fact]
    public async Task Link_AlreadyQueued_ReportsIt()
    {
        harness.Source.Gate = new TaskCompletionSource();
        harness.Source.ResolveResult = [DownloadHarness.Media("a")];
        await requests.SubmitAsync("https://youtu.be/a");

        var again = await requests.SubmitAsync("https://youtu.be/a");
        harness.Source.Gate.SetResult();

        Assert.Equal((0, "Already in the queue."), (again.Queued, again.Error));
    }

    [Fact]
    public async Task Query_ReturnsResultsWithoutQueueing()
    {
        harness.Source.SearchResult = [DownloadHarness.Media("a")];

        var result = await requests.SubmitAsync("daft punk");

        Assert.Single(result.Results);
        Assert.Equal(0, result.Queued);
        Assert.Empty(harness.Queue.Items);
    }

    [Fact]
    public async Task Query_WithoutHits_SaysSo()
    {
        var result = await requests.SubmitAsync("zzz");

        Assert.Equal("No results.", result.Error);
    }

    [Fact]
    public async Task SourceError_BecomesMessage()
    {
        harness.Source.ResolveError = new MediaSourceException("Could not read that link: 404");

        var result = await requests.SubmitAsync("https://example.com/x");

        Assert.Equal("Could not read that link: 404", result.Error);
    }

    [Fact]
    public async Task Blocked_ByPolicy_DoesNotTouchTheNetwork()
    {
        harness.Policy.Reason = "No internet connection.";
        harness.Source.SearchResult = [DownloadHarness.Media("a")];

        var result = await requests.SubmitAsync("daft punk");

        Assert.Equal("No internet connection.", result.Error);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task EmptyInput_AsksForOne()
    {
        var result = await requests.SubmitAsync("   ");

        Assert.NotNull(result.Error);
    }
}
