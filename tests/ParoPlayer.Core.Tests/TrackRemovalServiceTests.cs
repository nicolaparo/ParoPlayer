using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;

namespace ParoPlayer.Core.Tests;

public sealed class TrackRemovalServiceTests : IDisposable
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly string folder = Directory.CreateTempSubdirectory().FullName;
    private readonly InMemoryRepository<Track> tracks = new();
    private readonly InMemoryRepository<PlaylistTrack> entries = new();
    private readonly LibraryService library;
    private readonly PlaylistService playlists;
    private readonly TrackRemovalService removal;

    public TrackRemovalServiceTests()
    {
        library = new LibraryService(tracks, time);
        playlists = new PlaylistService(new InMemoryRepository<Playlist>(), entries, tracks, new SmartPlaylistEngine(time, new Random(1)));
        removal = new TrackRemovalService(tracks, entries);
    }

    public void Dispose() => Directory.Delete(folder, true);

    private async Task<Track> AddAsync(string name, string? file = null)
    {
        var path = Path.Combine(folder, file ?? $"{name}.mp3");
        await File.WriteAllTextAsync(path, "x");
        return await library.AddTrackAsync(new Track { Title = name, FilePath = path });
    }

    [Fact]
    public async Task Remove_DeletesTrackPlaylistEntriesAndFile()
    {
        var a = await AddAsync("A");
        var b = await AddAsync("B");
        var list = await playlists.CreateManualAsync("Mix");
        await playlists.AddTrackAsync(list.Id, a.Id);
        await playlists.AddTrackAsync(list.Id, b.Id);

        await removal.RemoveAsync([a.Id]);

        Assert.Equal(["B"], (await playlists.GetTracksAsync(list.Id)).Select(track => track.Title));
        Assert.Equal(["B"], (await library.GetAllAsync()).Select(track => track.Title));
        Assert.False(File.Exists(a.FilePath));
        Assert.True(File.Exists(b.FilePath));
    }

    [Fact]
    public async Task Remove_KeepsFileStillUsedByAnotherTrack()
    {
        var a = await AddAsync("A", "shared.wav");
        var b = await AddAsync("B", "shared.wav");

        await removal.RemoveAsync([a.Id]);
        Assert.True(File.Exists(b.FilePath));

        await removal.RemoveAsync([b.Id]);
        Assert.False(File.Exists(b.FilePath));
    }

    [Fact]
    public async Task DeletePlaylist_RemovesItsEntriesButNotTracks()
    {
        var a = await AddAsync("A");
        var list = await playlists.CreateManualAsync("Mix");
        await playlists.AddTrackAsync(list.Id, a.Id);

        await playlists.DeleteAsync(list.Id);

        Assert.Null(await playlists.GetAsync(list.Id));
        Assert.Empty(await entries.GetAllAsync());
        Assert.Single(await library.GetAllAsync());
    }
}
