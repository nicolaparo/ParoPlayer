using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Media3.Session;

namespace ParoPlayer.App.Playback;

/// <summary>
/// Foreground media service. Media3 posts the playback notification (transport controls, lock screen), promotes the
/// service to the foreground while playing and handles media buttons. The player itself is owned by <see cref="PlayerHost"/>.
/// To serve Android Auto later, change the base class to MediaLibraryService, build a MediaLibrarySession with a
/// callback that returns the browse tree and add the browser-service intent filter.
/// </summary>
[Service(Name = ServiceName, Exported = true, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
[IntentFilter(["androidx.media3.session.MediaSessionService"])]
public class PlaybackService : MediaSessionService
{
    /// <summary>Stable Java class name, referenced from the manifest (and later from Android Auto).</summary>
    public const string ServiceName = "com.nicolaparo.paroplayer.PlaybackService";

    private MediaSession? session;

    public override void OnCreate()
    {
        base.OnCreate();

        // Android instantiates services itself, so constructor injection is not possible here.
        var host = IPlatformApplication.Current!.Services.GetRequiredService<PlayerHost>();
        var openApp = PendingIntent.GetActivity(
            this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        session = new MediaSession.Builder(this, host.Player)
            .SetId("paroplayer")!
            .SetSessionActivity(openApp!)!
            .Build();
        AddSession(session);
    }

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo) => session;

    /// <summary>The user swiped the app away: keep playing if music is playing, otherwise stop the service.</summary>
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        var host = IPlatformApplication.Current!.Services.GetRequiredService<PlayerHost>();
        if (!host.Player.PlayWhenReady || host.Player.MediaItemCount == 0)
        {
            StopSelf();
        }
    }

    public override void OnDestroy()
    {
        session?.Release();
        session?.Dispose();
        session = null;
        base.OnDestroy();
    }
}
