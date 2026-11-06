using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Tests;

public sealed class TrackImportServiceTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
    private readonly LibraryFolder folder;
    private readonly LibraryService library = new(new InMemoryRepository<Track>(), new FakeTimeProvider());

    public TrackImportServiceTests()
    {
        folder = new LibraryFolder(System.IO.Path.Combine(root, "library"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeReader(TrackMetadata metadata) : ITrackMetadataReader
    {
        public Task<TrackMetadata> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(metadata);
    }

    private static MemoryStream Bytes(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Import_CopiesFileIntoLibraryAndCreatesTrackFromMetadata()
    {
        var service = new TrackImportService(folder, new FakeReader(new("Hurry Up", "M83", "Dreaming", "Synth", 2011, 305, 3)), library, new CoverStore(folder, new PassThroughCoverImageProcessor()));

        var track = await service.ImportAsync("hurry.mp3", Bytes("audio"));

        Assert.StartsWith(folder.Path, track.FilePath);
        Assert.Equal("audio", await File.ReadAllTextAsync(track.FilePath));
        Assert.Equal(("Hurry Up", "M83", "Dreaming", "Synth", 2011, 305), (track.Title, track.Artist, track.Album, track.Genre, track.Year, track.DurationSeconds));
        Assert.Single(await library.GetAllAsync());
    }

    [Fact]
    public async Task Import_WithEmbeddedCover_SavesItInLibraryFolder()
    {
        var service = new TrackImportService(folder, new FakeReader(new("Hurry Up", Cover: [1, 2, 3])), library, new CoverStore(folder, new PassThroughCoverImageProcessor()));

        var track = await service.ImportAsync("hurry.mp3", Bytes("audio"));

        Assert.StartsWith(folder.Path, track.CoverPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(track.CoverPath));
    }

    [Fact]
    public async Task Import_WithoutCover_LeavesCoverPathEmpty()
    {
        var service = new TrackImportService(folder, new FileNameMetadataReader(), library, new CoverStore(folder, new PassThroughCoverImageProcessor()));

        var track = await service.ImportAsync("plain.mp3", Bytes("audio"));

        Assert.Equal("", track.CoverPath);
    }

    [Fact]
    public async Task Import_SameNameTwice_KeepsBothFiles()
    {
        var service = new TrackImportService(folder, new FileNameMetadataReader(), library, new CoverStore(folder, new PassThroughCoverImageProcessor()));

        var first = await service.ImportAsync("song.mp3", Bytes("one"));
        var second = await service.ImportAsync("song.mp3", Bytes("two"));

        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.Equal("two", await File.ReadAllTextAsync(second.FilePath));
    }

    [Fact]
    public async Task Import_EmptyTitleFromReader_FallsBackToFileName()
    {
        var service = new TrackImportService(folder, new FakeReader(new("")), library, new CoverStore(folder, new PassThroughCoverImageProcessor()));

        var track = await service.ImportAsync("My Song.flac", Bytes("x"));

        Assert.Equal("My Song", track.Title);
    }

    [Fact]
    public async Task FileNameReader_UsesNameWithoutExtension()
    {
        var metadata = await new FileNameMetadataReader().ReadAsync(@"C:\music\Blue Monday.mp3");

        Assert.Equal("Blue Monday", metadata.Title);
    }

    [Fact]
    public async Task TagLibReader_OnUnreadableFile_FallsBackToFileName()
    {
        Directory.CreateDirectory(root);
        var path = System.IO.Path.Combine(root, "Broken Track.mp3");
        await File.WriteAllTextAsync(path, "this is not audio");

        var metadata = await new TagLibMetadataReader().ReadAsync(path);

        Assert.Equal("Broken Track", metadata.Title);
    }
}
