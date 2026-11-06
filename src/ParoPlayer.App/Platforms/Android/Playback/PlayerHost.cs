using Android.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;

namespace ParoPlayer.App.Playback;

/// <summary>
/// Owns the one ExoPlayer of the process. It outlives the service (which only builds a MediaSession around it),
/// so the app can queue music before the service has started. Everything touching <see cref="Player"/> must run on
/// the main thread, the application looper of the player.
/// </summary>
public sealed class PlayerHost
{
    private IExoPlayer? player;

    /// <summary>False until something asked for the player, so an idle app never builds an ExoPlayer.</summary>
    public bool IsCreated => player is not null;

    public IExoPlayer Player => player ??= Create();

    private static IExoPlayer Create()
    {
        var context = Android.App.Application.Context;
        var audio = new AndroidX.Media3.Common.AudioAttributes.Builder()
            .SetUsage(C.UsageMedia)!
            .SetContentType(C.AudioContentTypeMusic)!
            .Build()!;

        return new ExoPlayerBuilder(context)
            .SetAudioAttributes(audio, true)!          // audio focus: pause on calls, duck for notifications
            .SetHandleAudioBecomingNoisy(true)!        // pause when headphones are unplugged
            .SetWakeMode(C.WakeModeLocal)!             // CPU wake lock while playing (needs WAKE_LOCK)
            .Build()!;
    }
}
