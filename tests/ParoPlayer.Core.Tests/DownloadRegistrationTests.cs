using ParoPlayer.Core.Playback;
using Microsoft.Extensions.DependencyInjection;
using ParoPlayer.Core.Download;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Tests;

public sealed class DownloadRegistrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AddParoCore_ResolvesTheWholeDownloadGraph()
    {
        Directory.CreateDirectory(root);
        var services = new ServiceCollection();
        services.AddParoCore(Path.Combine(root, "test.db3"));
        services.AddSingleton<ILibraryFolder>(new LibraryFolder(Path.Combine(root, "library")));
        services.AddSingleton<IPlayerService>(new SilentPlayerService(TimeProvider.System, new Random(1)));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<DownloadRequests>());
        Assert.IsType<FfmpegAudioConverter>(provider.GetRequiredService<IAudioConverter>());
        Assert.Null(provider.GetRequiredService<IFfmpegLocator>().ExecutablePath);
    }
}
