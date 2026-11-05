using Microsoft.Extensions.DependencyInjection;
using ParoPlayer.Core.Data;
using ParoPlayer.Core.Engine;
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
        return services;
    }
}
