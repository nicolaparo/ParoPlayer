using Microsoft.Extensions.Logging;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Maui.DevFlow.Blazor;
#endif
using ParoPlayer.App.Services;
using ParoPlayer.Core;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Playback;
using ParoPlayer.Core.Storage;
#if ANDROID
using ParoPlayer.App.Downloads;
using ParoPlayer.App.Playback;
#endif

namespace ParoPlayer.App;

public static class MauiProgram
{
    private const string DatabaseFileName = "paroplayer.db3";
    private const string LibraryFolderName = "library";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddParoCore(Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName));
        builder.Services.AddSingleton<ILibraryFolder>(new LibraryFolder(Path.Combine(FileSystem.AppDataDirectory, LibraryFolderName)));
        builder.Services.AddSingleton<IPreferences>(Preferences.Default);
        builder.Services.AddSingleton<ISecureStorage>(SecureStorage.Default);
        builder.Services.AddSingleton<IFilePicker>(FilePicker.Default);
        builder.Services.AddSingleton<IShare>(Share.Default);
        builder.Services.AddSingleton<IHapticFeedback>(HapticFeedback.Default);
        builder.Services.AddSingleton<ITrackSharer, MauiTrackSharer>();
        builder.Services.AddSingleton<IDiscogsTokenStore, SecureStorageDiscogsTokenStore>();
        builder.Services.AddSingleton<IMusicImporter, MauiMusicImporter>();
        builder.Services.AddSingleton<AppSettings>();
        builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
        builder.Services.AddSingleton<IClipboard>(Clipboard.Default);
        builder.Services.AddSingleton<IDownloadPolicy, ConnectivityDownloadPolicy>();
        builder.Services.AddSingleton<INotificationPermission, MauiNotificationPermission>();
#if ANDROID
        builder.Services.AddSingleton<IFfmpegLocator, AndroidFfmpegLocator>();
        builder.Services.AddSingleton<IDownloadKeepAlive, AndroidDownloadKeepAlive>();
        builder.Services.AddSingleton<PlayerHost>();
        builder.Services.AddSingleton<IPlayerService, AndroidPlayerService>();
#else
        // Windows is a dev preview: the queue and UI work, no audio is played.
        builder.Services.AddSingleton<IPlayerService, SilentPlayerService>();
#endif
        builder.Services.AddSingleton<PlayerState>();
        builder.Services.AddSingleton<DemoLibrary>();

#if DEBUG
        builder.AddMauiDevFlowAgent();
        builder.AddMauiBlazorDevFlowTools();
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
