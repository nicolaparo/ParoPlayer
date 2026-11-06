using System.Text;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Bpm;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Playback;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Storage;
using ParoPlayer.Core.Tagging;

namespace ParoPlayer.Core.Tests;

public sealed class TrackEditTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly LibraryFolder folder;
    private readonly InMemoryRepository<Track> repository = new();
    private readonly FakeTagWriter writer = new();
    private readonly TrackEditService service;

    public TrackEditTests()
    {
        folder = new LibraryFolder(Path.Combine(root, "library"));
        service = new TrackEditService(repository, new DeferredTagWriter(new SilentPlayerService(TimeProvider.System, new Random(1)), writer, new TagLibBpmTag()), new CoverStore(folder, new PassThroughCoverImageProcessor()));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<Track> AddTrackAsync(string coverPath = "")
    {
        var track = new Track { Title = "Old", Artist = "Someone", Album = "Before", Year = 1999, FilePath = Path.Combine(root, "a.mp3"), CoverPath = coverPath };
        await repository.AddAsync(track);
        return track;
    }

    [Fact]
    public async Task Apply_UpdatesTrackAndWritesAllTags()
    {
        var track = await AddTrackAsync();

        var result = await service.ApplyAsync(track.Id, new TrackEdit(" New ", "Artist", "Album", "Pop", 2020, 4), null);

        Assert.NotNull(result);
        Assert.Null(result.TagError);
        var saved = await repository.GetAsync(track.Id);
        Assert.Equal(("New", "Artist", "Album", "Pop", 2020, 4), (saved!.Title, saved.Artist, saved.Album, saved.Genre, saved.Year, saved.TrackNumber));
        Assert.Equal(new TrackTags("New", "Artist", "Album", 2020, "Pop", 4), writer.Tags);
    }

    [Fact]
    public async Task Apply_WhenFileCannotBeTagged_StillSavesDatabaseAndReportsError()
    {
        var track = await AddTrackAsync();
        writer.Error = new IOException("read only");

        var result = await service.ApplyAsync(track.Id, new TrackEdit("New", "", "", "", 0, 0), null);

        Assert.Equal("read only", result!.TagError);
        Assert.Equal("New", (await repository.GetAsync(track.Id))!.Title);
    }

    [Fact]
    public async Task Apply_WithNewCover_SavesItAndEmbedsIt()
    {
        var track = await AddTrackAsync();

        var result = await service.ApplyAsync(track.Id, TrackEdit.From(track), [1, 2, 3]);

        Assert.StartsWith(folder.Path, result!.Track.CoverPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, writer.Cover);
    }

    [Fact]
    public async Task ApplyBulk_ChangesOnlyTheGivenFieldsOnEverySong()
    {
        var first = await AddTrackAsync();
        var second = await AddTrackAsync();
        second.Title = "Other";
        second.Album = "Kept";
        await repository.UpdateAsync(second);

        var results = await service.ApplyBulkAsync([first.Id, second.Id], new BulkTrackEdit(Genre: "Techno", Year: 2024));

        Assert.Equal(2, results.Count);
        foreach (var id in new[] { first.Id, second.Id })
        {
            var saved = await repository.GetAsync(id);
            Assert.Equal(("Techno", 2024, "Someone"), (saved!.Genre, saved.Year, saved.Artist));
        }

        Assert.Equal("Other", (await repository.GetAsync(second.Id))!.Title);
        Assert.Equal("Kept", (await repository.GetAsync(second.Id))!.Album);
        Assert.Equal("Techno", writer.Tags!.Genre);
    }

    [Fact]
    public async Task ApplyBulk_WithNothingToChange_TouchesNothing()
    {
        var track = await AddTrackAsync();

        var results = await service.ApplyBulkAsync([track.Id], new BulkTrackEdit());

        Assert.Empty(results);
        Assert.Null(writer.Tags);
    }

    [Fact]
    public async Task ApplyBulk_SkipsMissingSongs()
    {
        var track = await AddTrackAsync();

        var results = await service.ApplyBulkAsync([Guid.NewGuid(), track.Id], new BulkTrackEdit(Artist: "New"));

        Assert.Single(results);
    }

    [Fact]
    public async Task UndoBulk_RestoresEverySong()
    {
        var first = await AddTrackAsync();
        var second = await AddTrackAsync();
        var results = await service.ApplyBulkAsync([first.Id, second.Id], new BulkTrackEdit(Artist: "Changed", Year: 2000));

        var error = await service.UndoBulkAsync(results.Select(result => result.Snapshot));

        Assert.Null(error);
        foreach (var id in new[] { first.Id, second.Id })
        {
            var saved = await repository.GetAsync(id);
            Assert.Equal(("Someone", 1999), (saved!.Artist, saved.Year));
        }
    }

    [Fact]
    public async Task Undo_RestoresFieldsAndOldCover()
    {
        Directory.CreateDirectory(folder.Path);
        var coverPath = Path.Combine(folder.Path, "covers", "x.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(coverPath)!);
        await File.WriteAllBytesAsync(coverPath, [9, 9]);
        var track = await AddTrackAsync(coverPath);
        var result = await service.ApplyAsync(track.Id, new TrackEdit("New", "A", "B", "C", 2001, 2), [1, 2, 3]);

        await service.UndoAsync(result!.Snapshot);

        var restored = await repository.GetAsync(track.Id);
        Assert.Equal(("Old", "Someone", "Before", 1999), (restored!.Title, restored.Artist, restored.Album, restored.Year));
        Assert.Equal(coverPath, restored.CoverPath);
        Assert.Equal(new byte[] { 9, 9 }, await File.ReadAllBytesAsync(coverPath));
        Assert.Equal("Old", writer.Tags!.Title);
    }

    [Fact]
    public void Diff_ListsOnlyChangedFields()
    {
        var changes = TrackEditDiff.Between(new TrackEdit("A", "B", "C", "", 0, 0), new TrackEdit("A", "B2", "C", "Rock", 2000, 0));

        Assert.Equal(["Artist", "Genre", "Year"], changes.Select(change => change.Field));
    }

    [Fact]
    public void Candidate_KeepsCurrentValuesForUnknownFields()
    {
        var candidate = new MetadataCandidate("iTunes", "Title", "Artist", "", "", 0, 0, null);

        Assert.Equal(new TrackEdit("Title", "Artist", "Album", "Jazz", 1990, 3), candidate.ApplyTo(new TrackEdit("x", "y", "Album", "Jazz", 1990, 3)));
    }

    private sealed class FakeHttp(Dictionary<string, string> responses) : IHttpFetcher
    {
        public List<Uri> Requests { get; } = [];

        public Task<byte[]?> GetAsync(Uri url, CancellationToken cancellationToken = default)
        {
            Requests.Add(url);
            var match = responses.FirstOrDefault(pair => url.Host == pair.Key);
            return Task.FromResult(match.Key is null ? null : Encoding.UTF8.GetBytes(match.Value));
        }
    }

    private const string MusicBrainzJson = """
        {"recordings":[{"title":"Get Lucky","first-release-date":"2013-04-19",
          "artist-credit":[{"name":"Daft Punk","joinphrase":""}],
          "tags":[{"count":1,"name":"funk"},{"count":5,"name":"disco"}],
          "releases":[{"id":"abc","title":"Random Access Memories","date":"2013-05-17","media":[{"track":[{"number":"8"}]}]}]}]}
        """;

    private const string ITunesJson = """
        {"results":[{"trackName":"Get Lucky","artistName":"Daft Punk","collectionName":"RAM","primaryGenreName":"Electronic","releaseDate":"2013-04-19T07:00:00Z","trackNumber":8,"artworkUrl100":"https://img/100x100bb.jpg"}]}
        """;

    [Fact]
    public async Task Lookup_ParsesMusicBrainzAndBuildsCoverArchiveUrl()
    {
        var http = new FakeHttp(new() { ["musicbrainz.org"] = MusicBrainzJson });

        var found = await new MetadataLookupService([new MusicBrainzSource(http), new ITunesSource(http)], http).SearchAsync("Get Lucky", "Daft Punk");

        var candidate = Assert.Single(found);
        Assert.Equal(new MetadataCandidate("MusicBrainz", "Get Lucky", "Daft Punk", "Random Access Memories", "Disco", 2013, 8, new Uri("https://coverartarchive.org/release/abc/front-500")), candidate);
        Assert.DoesNotContain(http.Requests, url => url.Host == "itunes.apple.com");
    }

    [Fact]
    public async Task Lookup_FallsBackToITunesWhenMusicBrainzIsEmpty()
    {
        var http = new FakeHttp(new() { ["musicbrainz.org"] = """{"recordings":[]}""", ["itunes.apple.com"] = ITunesJson });

        var found = await new MetadataLookupService([new MusicBrainzSource(http), new ITunesSource(http)], http).SearchAsync("Get Lucky", "");

        var candidate = Assert.Single(found);
        Assert.Equal(("iTunes", "Electronic", 2013), (candidate.Source, candidate.Genre, candidate.Year));
        Assert.Equal(new Uri("https://img/600x600bb.jpg"), candidate.CoverUrl);
    }

    private sealed class ThrowingSource : IMetadataSource
    {
        public Task<IReadOnlyList<MetadataCandidate>> SearchAsync(string title, string artist, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("offline");
    }

    [Fact]
    public async Task Lookup_WhenOffline_ReturnsNothing()
    {
        var found = await new MetadataLookupService([new ThrowingSource()], new FakeHttp([])).SearchAsync("a", "b");

        Assert.Empty(found);
    }
}
