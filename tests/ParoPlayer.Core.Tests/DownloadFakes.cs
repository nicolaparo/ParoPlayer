using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Tests;

internal sealed class FakeMediaSource : IMediaSource
{
    public List<MediaInfo> ResolveResult { get; set; } = [];

    public List<MediaInfo> SearchResult { get; set; } = [];

    public Exception? ResolveError { get; set; }

    public Exception? DownloadError { get; set; }

    public bool IsAac { get; set; } = true;

    public byte[]? Cover { get; set; } = [1, 2, 3];

    /// <summary>When set, downloads wait for it (to test cancel and ordering).</summary>
    public TaskCompletionSource? Gate { get; set; }

    public List<string> Downloaded { get; } = [];

    public Task<IReadOnlyList<MediaInfo>> ResolveAsync(string url, CancellationToken cancellationToken = default) =>
        ResolveError is null ? Task.FromResult<IReadOnlyList<MediaInfo>>(ResolveResult) : Task.FromException<IReadOnlyList<MediaInfo>>(ResolveError);

    public Task<IReadOnlyList<MediaInfo>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MediaInfo>>(SearchResult);

    public async Task<DownloadedAudio> DownloadAudioAsync(MediaInfo media, string basePath, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        Downloaded.Add(media.Id);
        progress.Report(0.5);
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        if (DownloadError is not null)
        {
            throw DownloadError;
        }

        var path = basePath + (IsAac ? ".mp4" : ".webm");
        await File.WriteAllTextAsync(path, "audio of " + media.Id, cancellationToken);
        progress.Report(1);
        return new DownloadedAudio(path, IsAac);
    }

    public Task<byte[]?> FetchCoverAsync(MediaInfo media, CancellationToken cancellationToken = default) => Task.FromResult(Cover);
}

internal sealed class FakeConverter : IAudioConverter
{
    public AudioConversion? Last { get; private set; }

    public Exception? Error { get; set; }

    public Task<string> ConvertAsync(AudioConversion request, CancellationToken cancellationToken = default)
    {
        Last = request;
        if (Error is not null)
        {
            return Task.FromException<string>(Error);
        }

        var output = request.OutputBasePath + ".m4a";
        File.Move(request.InputPath, output);
        return Task.FromResult(output);
    }
}

internal sealed class FakeTagWriter : ITrackTagWriter
{
    public TrackTags? Tags { get; private set; }

    public byte[]? Cover { get; private set; }

    public Exception? Error { get; set; }

    public Task WriteAsync(string filePath, TrackTags tags, byte[]? cover, CancellationToken cancellationToken = default)
    {
        Tags = tags;
        Cover = cover;
        return Error is null ? Task.CompletedTask : Task.FromException(Error);
    }
}

internal sealed class FakePolicy : IDownloadPolicy
{
    public string? Reason { get; set; }

    public string? BlockReason() => Reason;
}

internal sealed class FakeKeepAlive : IDownloadKeepAlive
{
    public int Begins { get; private set; }

    public void Begin() => Begins++;
}

/// <summary>A temp folder with a library, an importer and a ready pipeline around the given fakes.</summary>
internal sealed class DownloadHarness : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public DownloadHarness()
    {
        Library = new LibraryService(new InMemoryRepository<Track>(), new FakeTimeProvider());
        Folder = new LibraryFolder(Path.Combine(root, "library"));
        WorkPath = Path.Combine(root, "work");
        Pipeline = new DownloadPipeline(
            Source,
            Converter,
            new PassThroughAudioConverter(),
            Tags,
            new TrackImportService(Folder, new FileNameMetadataReader(), Library, new CoverStore(Folder, new PassThroughCoverImageProcessor())),
            new DownloadWorkFolder(WorkPath));
        Queue = new DownloadQueue(Pipeline, Policy, KeepAlive, TimeProvider.System);
    }

    public FakeMediaSource Source { get; } = new();

    public FakeConverter Converter { get; } = new();

    public FakeTagWriter Tags { get; } = new();

    public FakePolicy Policy { get; } = new();

    public FakeKeepAlive KeepAlive { get; } = new();

    public LibraryService Library { get; }

    public LibraryFolder Folder { get; }

    public string WorkPath { get; }

    public DownloadPipeline Pipeline { get; }

    public DownloadQueue Queue { get; }

    public static MediaInfo Media(string id, string title = "Artist - Song", string author = "Channel") =>
        new(id, title, author, TimeSpan.FromMinutes(3), "https://youtu.be/" + id, "https://img/" + id);

    public bool WorkFolderIsEmpty() => !Directory.Exists(WorkPath) || Directory.GetFileSystemEntries(WorkPath).Length == 0;

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
