using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Download;

/// <summary>Where partial downloads are kept until they are imported. Never inside the library folder.</summary>
public record DownloadWorkFolder(string Path);

public record DownloadOutcome(Track Track, string? Note);

/// <summary>Downloads one item, converts it, writes its tags and imports it into the library through <see cref="TrackImportService"/>.</summary>
public class DownloadPipeline(
    IMediaSource source,
    IAudioConverter converter,
    PassThroughAudioConverter passThrough,
    ITrackTagWriter tagWriter,
    TrackImportService importer,
    DownloadWorkFolder workFolder)
{
    private const double DownloadShare = 0.85;

    public async Task<DownloadOutcome> RunAsync(MediaInfo media, Action<DownloadState, double> report, CancellationToken cancellationToken = default)
    {
        var work = Path.Combine(workFolder.Path, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            report(DownloadState.Downloading, 0);
            var downloaded = await source.DownloadAudioAsync(
                media,
                Path.Combine(work, "source"),
                new SyncProgress(share => report(DownloadState.Downloading, share * DownloadShare)),
                cancellationToken);

            report(DownloadState.Converting, DownloadShare);
            var (path, note) = await ConvertAsync(downloaded, work, cancellationToken);

            report(DownloadState.Tagging, 0.93);
            var (artist, title) = TitleParser.Parse(media.Title, media.Author);
            var cover = await source.FetchCoverAsync(media, cancellationToken);
            try
            {
                await tagWriter.WriteAsync(path, new TrackTags(title, artist), cover, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tags are a bonus: the importer falls back to the file name, so the track still lands in the library.
                note ??= $"Saved without tags: {ex.Message}";
            }

            // A local copy of a few MB: not cancellable, because stopping halfway would leave a partial file in the library.
            report(DownloadState.Importing, 0.97);
            await using var stream = File.OpenRead(path);
            var track = await importer.ImportAsync(FileNameFor(artist, title) + Path.GetExtension(path), stream, CancellationToken.None);
            return new DownloadOutcome(track, note);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    private async Task<(string Path, string? Note)> ConvertAsync(DownloadedAudio downloaded, string work, CancellationToken cancellationToken)
    {
        var request = new AudioConversion(downloaded.FilePath, Path.Combine(work, "audio"), downloaded.IsAac);
        try
        {
            return (await converter.ConvertAsync(request, cancellationToken), null);
        }
        catch (AudioConversionException ex)
        {
            return (await passThrough.ConvertAsync(request, cancellationToken), $"Saved without conversion: {ex.Message}");
        }
    }

    private static string FileNameFor(string artist, string title) => string.IsNullOrWhiteSpace(artist) ? title : $"{artist} - {title}";

    private sealed class SyncProgress(Action<double> handler) : IProgress<double>
    {
        public void Report(double value) => handler(value);
    }
}
