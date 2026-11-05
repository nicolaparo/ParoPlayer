using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;

namespace ParoPlayer.Core.Tests;

public class LibraryServiceTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly LibraryService library;

    public LibraryServiceTests()
    {
        library = new LibraryService(new InMemoryRepository<Track>(), time);
    }

    [Fact]
    public async Task AddTrack_StampsAddedAtAndStores()
    {
        var track = await library.AddTrackAsync(new Track { Title = "Song" });

        Assert.Equal(time.GetUtcNow(), track.AddedAt);
        Assert.Single(await library.GetAllAsync());
    }

    [Fact]
    public async Task Search_MatchesTitleArtistAlbumIgnoringCase()
    {
        await library.AddTrackAsync(new Track { Title = "Blue Monday", Artist = "New Order", Album = "Power" });
        await library.AddTrackAsync(new Track { Title = "Other", Artist = "Someone", Album = "Blue Album" });
        await library.AddTrackAsync(new Track { Title = "Nope", Artist = "Nobody", Album = "None" });

        Assert.Equal(2, (await library.SearchAsync("BLUE")).Count);
        Assert.Single(await library.SearchAsync("new order"));
        Assert.Equal(3, (await library.SearchAsync("  ")).Count);
    }

    [Fact]
    public async Task RecordPlay_IncrementsCountAndSetsLastPlayed()
    {
        var track = await library.AddTrackAsync(new Track { Title = "Song" });
        time.Advance(TimeSpan.FromHours(2));

        await library.RecordPlayAsync(track.Id);
        var played = await library.RecordPlayAsync(track.Id);

        Assert.Equal(2, played!.PlayCount);
        Assert.Equal(time.GetUtcNow(), played.LastPlayedAt);
    }

    [Fact]
    public async Task RecordPlay_UnknownTrackReturnsNull()
    {
        Assert.Null(await library.RecordPlayAsync(Guid.NewGuid()));
    }
}
