using Microsoft.Extensions.DependencyInjection;
using ParoPlayer.App.Services;
using ParoPlayer.Core.Bpm;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Harmony;
using ParoPlayer.Core.Import;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Navigation;
using ParoPlayer.Core.Playback;
using ParoPlayer.Core.Services;
using ParoPlayer.Core.Storage;
using ParoPlayer.Core.Tagging;

namespace ParoPlayer.App.Browser;

/// <summary>
/// Wires the browser host's container. The data and services are the phone's own singletons (the same SQLite
/// connection, so an edit made in the browser is an edit on the phone); what belongs to one person's session
/// (player, back stack, view state, cover cache) is scoped to the circuit, and phone-only abilities are stubs.
/// </summary>
public static class BrowserServices
{
    public static void Add(IServiceCollection services, IServiceProvider app)
    {
        Share<TimeProvider>(services, app);
        Share<Random>(services, app);
        Share<IRepository<Track>>(services, app);
        Share<LibraryService>(services, app);
        Share<PlaylistService>(services, app);
        Share<TrackRemovalService>(services, app);
        Share<TrackEditService>(services, app);
        Share<TrackImportService>(services, app);
        Share<TrackKeyService>(services, app);
        Share<TrackBpmService>(services, app);
        Share<KeyBackfillService>(services, app);
        Share<CoverBackfillService>(services, app);
        Share<WaveformBackfillService>(services, app);
        Share<WaveformStore>(services, app);
        Share<MetadataLookupService>(services, app);
        Share<ISmartPlaylistEngine>(services, app);
        Share<ILibraryFolder>(services, app);
        Share<DownloadQueue>(services, app);
        Share<DownloadRequests>(services, app);
        Share<IDownloadPolicy>(services, app);
        Share<AppSettings>(services, app);
        Share<DemoLibrary>(services, app);

        services.AddSingleton(new HostShell(ShowSettings: false));
        services.AddSingleton<IHapticFeedback, BrowserHapticFeedback>();
        services.AddSingleton<IClipboard, BrowserClipboard>();
        services.AddSingleton<IFilePicker, BrowserFilePicker>();
        services.AddSingleton<ITrackSharer, BrowserTrackSharer>();
        services.AddSingleton<IMusicImporter, BrowserMusicImporter>();
        services.AddSingleton<INotificationPermission, BrowserNotificationPermission>();

        services.AddScoped<IPlayerService, BrowserPlayerService>();
        services.AddScoped<PlayerState>();
        services.AddScoped<BackStack>();
        services.AddScoped<LibraryViewState>();
        services.AddScoped<CoverImages>();
    }

    // Registered as an instance on purpose: a container disposes the singletons it created from a factory when it
    // is disposed, and these belong to the phone, which keeps using them after the browser host stops.
    private static void Share<T>(IServiceCollection services, IServiceProvider app) where T : class =>
        services.AddSingleton(typeof(T), app.GetRequiredService<T>());
}
