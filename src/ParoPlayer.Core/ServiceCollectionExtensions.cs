using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Services;
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
        services.AddSingleton<TrackImportService>();
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
