namespace ParoPlayer.Core.Download;

/// <summary>What happened to text typed in the Download box. <see cref="Results"/> holds search hits that still need picking.</summary>
public record SubmitResult(int Queued, IReadOnlyList<MediaInfo> Results, string? Error = null);

/// <summary>Turns a pasted link into queued downloads, or a search phrase into a list of results.</summary>
public class DownloadRequests(IMediaSource source, DownloadQueue queue, IDownloadPolicy policy)
{
    public async Task<SubmitResult> SubmitAsync(string input, CancellationToken cancellationToken = default)
    {
        input = input.Trim();
        if (input.Length == 0)
        {
            return new SubmitResult(0, [], "Paste a link or type what to search for.");
        }

        if (policy.BlockReason() is { } reason)
        {
            return new SubmitResult(0, [], reason);
        }

        try
        {
            if (!DownloadInput.IsLink(input))
            {
                var results = await source.SearchAsync(input, cancellationToken);
                return new SubmitResult(0, results, results.Count == 0 ? "No results." : null);
            }

            var queued = 0;
            foreach (var media in await source.ResolveAsync(input, cancellationToken))
            {
                if (await queue.EnqueueAsync(media))
                {
                    queued++;
                }
            }

            return new SubmitResult(queued, [], queued == 0 ? "Already in the queue." : null);
        }
        catch (MediaSourceException ex)
        {
            return new SubmitResult(0, [], ex.Message);
        }
    }
}
