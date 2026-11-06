using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using ParoPlayer.Core.Download;

namespace ParoPlayer.App.Downloads;

/// <summary>
/// Foreground service (type dataSync) with a progress notification, so downloads survive the screen turning off.
/// It follows <see cref="DownloadQueue"/> and stops itself when nothing is queued or running.
/// </summary>
[Service(Name = ServiceName, Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public class DownloadService : Service
{
    public const string ServiceName = "com.nicolaparo.paroplayer.DownloadService";

    private const string ChannelId = "downloads";
    private const int NotificationId = 4201;

    private IDisposable? subscription;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var queue = IPlatformApplication.Current!.Services.GetRequiredService<DownloadQueue>();
        EnsureChannel();
        var notification = Build(queue.Items);
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        subscription ??= queue.Subscribe(() =>
        {
            Refresh(queue.Items);
            return Task.CompletedTask;
        });
        Refresh(queue.Items);
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        subscription?.Dispose();
        subscription = null;
        base.OnDestroy();
    }

    private void Refresh(IReadOnlyList<DownloadItem> items)
    {
        if (!items.Any(item => item.State == DownloadState.Queued || item.IsRunning))
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return;
        }

        if (GetSystemService(NotificationService) is NotificationManager manager)
        {
            manager.Notify(NotificationId, Build(items));
        }
    }

    private Notification Build(IReadOnlyList<DownloadItem> items)
    {
        var running = items.FirstOrDefault(item => item.IsRunning) ?? items.FirstOrDefault(item => item.State == DownloadState.Queued);
        var open = PendingIntent.GetActivity(
            this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var waiting = items.Count(item => item.State == DownloadState.Queued);
        var text = running is null
            ? "Starting"
            : waiting > 0 ? $"{running.Media.Title} (+{waiting} waiting)" : running.Media.Title;

        return new NotificationCompat.Builder(this, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.StatSysDownload)!
            .SetContentTitle("Downloading music")!
            .SetContentText(text)!
            .SetProgress(100, (int)((running?.Progress ?? 0) * 100), false)!
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .SetContentIntent(open)!
            .Build()!;
    }

    private void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) || GetSystemService(NotificationService) is not NotificationManager manager)
        {
            return;
        }

        manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Downloads", NotificationImportance.Low)
        {
            Description = "Progress of music downloads",
        });
    }
}
