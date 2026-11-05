using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;

namespace ParoPlayer.Core.Tests;

public class PlaylistServiceTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly LibraryService library;
    private readonly PlaylistService playlists;

    public PlaylistServiceTests()
    {
        var tracks = new InMemoryRepository<Track>();
        library = new LibraryService(tracks, time);
        playlists = new PlaylistService(
            new InMemoryRepository<Playlist>(),
            new InMemoryRepository<PlaylistTrack>(),
            tracks,
            new SmartPlaylistEngine(time, new Random(1)));
    }

    private static string[] Titles(IEnumerable<Track> tracks) => [.. tracks.Select(track => track.Title)];

    [Fact]
    public async Task Manual_AddsTracksInOrder()
    {
        var a = await library.AddTrackAsync(new Track { Title = "A" });
        var b = await library.AddTrackAsync(new Track { Title = "B" });
        var playlist = await playlists.CreateManualAsync("Mix");

        await playlists.AddTrackAsync(playlist.Id, b.Id);
        await playlists.AddTrackAsync(playlist.Id, a.Id);

        Assert.False(playlist.IsSmart);
        Assert.Equal(["B", "A"], Titles(await playlists.GetTracksAsync(playlist.Id)));
    }

    [Fact]
    public async Task Reorder_AppliesNewOrder()
    {
        var a = await library.AddTrackAsync(new Track { Title = "A" });
        var b = await library.AddTrackAsync(new Track { Title = "B" });
        var c = await library.AddTrackAsync(new Track { Title = "C" });
        var playlist = await playlists.CreateManualAsync("Mix");
        foreach (var track in new[] { a, b, c })
        {
            await playlists.AddTrackAsync(playlist.Id, track.Id);
        }

        await playlists.ReorderAsync(playlist.Id, [c.Id, a.Id, b.Id]);

        Assert.Equal(["C", "A", "B"], Titles(await playlists.GetTracksAsync(playlist.Id)));
    }

    [Fact]
    public async Task Remove_DropsTrackAndKeepsOrder()
    {
        var a = await library.AddTrackAsync(new Track { Title = "A" });
        var b = await library.AddTrackAsync(new Track { Title = "B" });
        var c = await library.AddTrackAsync(new Track { Title = "C" });
        var playlist = await playlists.CreateManualAsync("Mix");
        foreach (var track in new[] { a, b, c })
        {
            await playlists.AddTrackAsync(playlist.Id, track.Id);
        }

        await playlists.RemoveTrackAsync(playlist.Id, b.Id);
        await playlists.AddTrackAsync(playlist.Id, b.Id);

        Assert.Equal(["A", "C", "B"], Titles(await playlists.GetTracksAsync(playlist.Id)));
    }

    [Fact]
    public async Task Smart_ResolvesTracksWithEngine()
    {
        await library.AddTrackAsync(new Track { Title = "Old", PlayCount = 1 });
        await library.AddTrackAsync(new Track { Title = "Hit", PlayCount = 50 });
        var definition = new SmartPlaylistDefinition(
            MatchMode.All,
            [new SmartRule(SmartField.PlayCount, SmartOperator.GreaterThan, "10")],
            SmartSort.MostPlayed,
            5);

        var playlist = await playlists.CreateSmartAsync("Favourites", definition);

        Assert.True(playlist.IsSmart);
        Assert.Equal(["Hit"], Titles(await playlists.GetTracksAsync(playlist.Id)));
    }

    [Fact]
    public async Task GetTracks_UnknownPlaylistThrows()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => playlists.GetTracksAsync(Guid.NewGuid()));
    }
}
