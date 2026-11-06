using Microsoft.Extensions.Hosting;

namespace ParoPlayer.App.Browser;

/// <summary>The app, not the console, owns the process: no signal handlers (they are not supported on Android).</summary>
internal sealed class AppHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
