using Android.Content;
using ParoPlayer.Core.Download;

namespace ParoPlayer.App.Downloads;

/// <summary>Starts <see cref="DownloadService"/>, which keeps the process in the foreground until the queue is idle.</summary>
public class AndroidDownloadKeepAlive : IDownloadKeepAlive
{
    public void Begin()
    {
        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(DownloadService));
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
        catch (Java.Lang.IllegalStateException)
        {
            // Android 12+ refuses to start a foreground service from the background (retry from the UI). Downloading
            // continues for as long as the process lives.
        }
    }
}
