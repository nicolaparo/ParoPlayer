using ParoPlayer.Core.Models;

namespace ParoPlayer.App.Services;

public interface ITrackSharer
{
    Task ShareAsync(IReadOnlyCollection<Track> tracks);
}

public class MauiTrackSharer(IShare share) : ITrackSharer
{
    public Task ShareAsync(IReadOnlyCollection<Track> tracks) =>
        share.RequestAsync(new ShareMultipleFilesRequest
        {
            Title = tracks.Count == 1 ? tracks.First().Title : $"{tracks.Count} songs",
            Files = [.. tracks.Select(track => new ShareFile(track.FilePath))],
        });
}
