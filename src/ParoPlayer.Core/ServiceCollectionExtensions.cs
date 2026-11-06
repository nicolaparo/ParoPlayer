using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Services;
using ParoPlayer.Core.Bpm;
using ParoPlayer.Core.Harmony;
using ParoPlayer.Core.Tagging;
using ParoPlayer.Core.Transfer;
using SQLite;

namespace ParoPlayer.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddParoCore(this IServiceCollection services, string dbPath)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);
        services.AddSingleton(new SQLiteAsyncConnection(dbPath));
        services.AddSingleton(typeof(IRepository<>), typeof(SqliteRepository<>));
        services.AddSingleton<ISmartPlaylistEngine, SmartPlaylistEngine>();
        services.AddSingleton<LibraryService>();
        services.AddSingleton<PlaylistService>();
        services.AddSingleton<TrackRemovalService>();
        services.AddSingleton<ITrackMetadataReader, TagLibMetadataReader>();
        services.TryAddSingleton<ICoverImageProcessor, PassThroughCoverImageProcessor>();
        services.AddSingleton<CoverStore>();
        services.TryAddSingleton<IWaveformAnalyzer, NoWaveformAnalyzer>();
        services.AddSingleton<WaveformStore>();
        services.TryAddSingleton<IPcmDecoder, NoPcmDecoder>();
        services.AddSingleton<IBpmTag, TagLibBpmTag>();
        services.AddSingleton<BpmDetectionService>();
        services.AddSingleton<TrackBpmService>();
        services.AddSingleton<KeyDetectionService>();
        services.AddSingleton<TrackKeyService>();
        services.AddSingleton<KeyBackfillService>();
        services.AddSingleton<DeferredTagWriter>();
        services.AddSingleton<CoverBackfillService>();
        services.AddSingleton<WaveformBackfillService>();
        services.AddSingleton<TrackImportService>();
        services.AddSingleton<TrackEditService>();
        services.AddSingleton<IHttpFetcher, HttpClientFetcher>();
        services.AddSingleton<IMetadataSource, MusicBrainzSource>();
        services.AddSingleton<IMetadataSource, ITunesSource>();
        services.AddSingleton<MetadataLookupService>();
        services.AddSingleton(new TransferOptions());
        services.AddSingleton<TransferSessions>();
        services.AddSingleton<TransferServer>();
        return services.AddParoDownload();
    }

    /// <summary>Download pipeline. The platform replaces the policy, keep-alive and ffmpeg locator after this call.</summary>
    private static IServiceCollection AddParoDownload(this IServiceCollection services)
    {
        services.TryAddSingleton<IDownloadPolicy, AlwaysAllowedDownloadPolicy>();
        services.TryAddSingleton<IDownloadKeepAlive, NoKeepAlive>();
        services.TryAddSingleton<IFfmpegLocator, NoFfmpegLocator>();
        services.AddSingleton(new DownloadWorkFolder(Path.Combine(Path.GetTempPath(), "paroplayer-downloads")));
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IMediaSource, YoutubeMediaSource>();
        services.AddSingleton<IProcessRunner, SystemProcessRunner>();
        services.AddSingleton<PassThroughAudioConverter>();
        services.AddSingleton<IAudioConverter, FfmpegAudioConverter>();
        services.AddSingleton<ITrackTagWriter, TagLibTrackTagWriter>();
        services.AddSingleton<DownloadPipeline>();
        services.AddSingleton<DownloadQueue>();
        services.AddSingleton<DownloadRequests>();
        return services;
    }
}
