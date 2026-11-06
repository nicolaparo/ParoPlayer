using Android.App;
using Android.Content.PM;
using Android.Views;

namespace ParoPlayer.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private Task backPress = Task.CompletedTask;
    private bool swallowBackUp;

    /// <summary>Starts whatever Back should close first (a sheet, the selection), or returns null when Back should go on as usual.</summary>
    public Func<Task?>? BackInterceptor { get; set; }

    // The WebView takes Back for its own page history before the activity's OnBackPressed or any back callback
    // runs, so Back is intercepted here, ahead of every view. The matching key-up is swallowed too.
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is { KeyCode: Keycode.Back })
        {
            if (e.Action == KeyEventActions.Down && e.RepeatCount == 0 && BackInterceptor?.Invoke() is { } pending)
            {
                // Key events are synchronous, so the handler's task is kept and observed instead of awaited;
                // a failure is logged, not lost.
                backPress = ObserveAsync(pending);
                swallowBackUp = true;
                return true;
            }

            if (e.Action == KeyEventActions.Up && swallowBackUp)
            {
                swallowBackUp = false;
                return true;
            }
        }

        return base.DispatchKeyEvent(e);
    }

    private static async Task ObserveAsync(Task pending)
    {
        try
        {
            await pending;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Back handler failed: {ex}");
        }
    }
}
