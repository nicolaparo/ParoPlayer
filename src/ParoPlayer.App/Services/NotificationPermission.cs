namespace ParoPlayer.App.Services;

/// <summary>Asks for the notification permission (Android 13+) so the download progress notification is visible.</summary>
public interface INotificationPermission
{
    Task EnsureAsync();
}

public class MauiNotificationPermission : INotificationPermission
{
    public Task EnsureAsync() =>
#if ANDROID
        EnsureOnAndroidAsync();

    private static async Task EnsureOnAndroidAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await Permissions.RequestAsync<Permissions.PostNotifications>());
        }
    }
#else
        Task.CompletedTask;
#endif
}
