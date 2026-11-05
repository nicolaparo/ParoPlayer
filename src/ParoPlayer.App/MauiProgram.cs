using Microsoft.Extensions.Logging;
using ParoPlayer.Core;
using ParoPlayer.Core.Storage;

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

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
