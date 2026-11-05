using Microsoft.Extensions.DependencyInjection;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;
using SQLite;

namespace ParoPlayer.Core.Tests;

public class SqlitePersistenceTests : IAsyncLifetime
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"paroplayer-{Guid.NewGuid():N}.db3");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        return Task.CompletedTask;
    }

    private ServiceProvider CreateProvider() => new ServiceCollection().AddParoCore(dbPath).BuildServiceProvider();

    [Fact]
    public async Task DataSurvivesReopeningTheDatabase()
    {
        Guid playlistId;
        await using (var first = CreateProvider())
        {
            var library = first.GetRequiredService<LibraryService>();
            var playlists = first.GetRequiredService<PlaylistService>();
            var a = await library.AddTrackAsync(new Track { Title = "A", Artist = "X" });
            var b = await library.AddTrackAsync(new Track { Title = "B", Artist = "Y" });
            await library.RecordPlayAsync(b.Id);
            var playlist = await playlists.CreateManualAsync("Mix");
            await playlists.AddTrackAsync(playlist.Id, b.Id);
            await playlists.AddTrackAsync(playlist.Id, a.Id);
            playlistId = playlist.Id;
            await first.GetRequiredService<SQLiteAsyncConnection>().CloseAsync();
        }

        await using var second = CreateProvider();
        var reopenedLibrary = second.GetRequiredService<LibraryService>();
        var reopenedPlaylists = second.GetRequiredService<PlaylistService>();

        var tracks = await reopenedPlaylists.GetTracksAsync(playlistId);
        Assert.Equal(["B", "A"], tracks.Select(track => track.Title));
        Assert.Equal(1, tracks[0].PlayCount);
        Assert.NotNull(tracks[0].LastPlayedAt);
        Assert.Single(await reopenedLibrary.SearchAsync("Y"));

        await second.GetRequiredService<SQLiteAsyncConnection>().CloseAsync();
    }

    [Fact]
    public async Task SmartPlaylistRoundTripsThroughSqlite()
    {
        await using var provider = CreateProvider();
        var library = provider.GetRequiredService<LibraryService>();
        var playlists = provider.GetRequiredService<PlaylistService>();
        await library.AddTrackAsync(new Track { Title = "Rocker", Genre = "Rock" });
        await library.AddTrackAsync(new Track { Title = "Smooth", Genre = "Jazz" });
        var definition = new SmartPlaylistDefinition(
            MatchMode.Any,
            [new SmartRule(SmartField.Genre, SmartOperator.Equals, "rock"), new SmartRule(SmartField.AddedDaysAgo, SmartOperator.LessThan, "1")],
            SmartSort.Title);

        var playlist = await playlists.CreateSmartAsync("Rock", definition);
        var tracks = await playlists.GetTracksAsync(playlist.Id);

        Assert.Equal(2, tracks.Count);
        await provider.GetRequiredService<SQLiteAsyncConnection>().CloseAsync();
    }
}
