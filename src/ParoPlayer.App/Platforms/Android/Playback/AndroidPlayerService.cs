using Android.Content;
using AndroidX.Media3.Common;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Playback;
using RepeatMode = ParoPlayer.Core.Playback.RepeatMode;

namespace ParoPlayer.App.Playback;

/// <summary>
/// <see cref="IPlayerService"/> over the in-process ExoPlayer from <see cref="PlayerHost"/>. The foreground
/// <see cref="PlaybackService"/> wraps the same player in a MediaSession, so the notification, lock screen and
/// headset buttons control exactly what this class controls. ExoPlayer holds the whole queue in play order
/// (computed by <see cref="PlayQueue"/>), so those external controls can skip tracks without any C# running.
/// All player access is marshalled to the main thread.
/// </summary>
public sealed class AndroidPlayerService : IPlayerService, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    // Media3 Player constants (the binding exposes them on an unnamed InterfaceConsts type, so they are mirrored here).
    private const int RepeatModeOff = 0;
    private const int RepeatModeOne = 1;
    private const int RepeatModeAll = 2;
    private const int StateEnded = 4;

    private readonly PlayerHost host;
    private readonly Random random;
    private readonly PlayerStateBroadcaster broadcaster;
    private PlayQueue? queue;
    private bool notificationPermissionAsked;

    public AndroidPlayerService(PlayerHost host, TimeProvider timeProvider, Random random)
    {
        this.host = host;
        this.random = random;
        broadcaster = new PlayerStateBroadcaster(ReadAsync, timeProvider, PollInterval);
    }

    public async Task PlayAsync(IReadOnlyList<Track> tracks, int startIndex = 0, CancellationToken cancellationToken = default)
    {
        await EnsureNotificationPermissionAsync();
        await OnMainThreadAsync(() =>
        {
            var newQueue = new PlayQueue(tracks, startIndex, random);
            var player = host.Player;

            // Start the service first: it wraps the player in a MediaSession and shows the notification.
            Android.App.Application.Context.StartService(new Intent(Android.App.Application.Context, typeof(PlaybackService)));

            queue = newQueue;
            player.RepeatMode = RepeatModeOff;
            player.ClearMediaItems();
            foreach (var track in newQueue.Order)
            {
                player.AddMediaItem(ToMediaItem(track));
            }

            player.SeekTo(newQueue.Position, 0);
            player.Prepare();
            player.Play();
        });
        await broadcaster.RefreshAsync(cancellationToken);
    }

    public Task PauseAsync(CancellationToken cancellationToken = default) => ChangeAsync(player => player.Pause(), cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) => ChangeAsync(player =>
    {
        if (player.MediaItemCount > 0)
        {
            if (player.PlaybackState == StateEnded)
            {
                player.SeekTo(0, 0);
            }

            player.Play();
        }
    }, cancellationToken);

    public Task NextAsync(CancellationToken cancellationToken = default) => ChangeAsync(player => player.SeekToNext(), cancellationToken);

    public Task PreviousAsync(CancellationToken cancellationToken = default) => ChangeAsync(player => player.SeekToPrevious(), cancellationToken);

    public Task SeekToAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        ChangeAsync(player => player.SeekTo((long)position.TotalMilliseconds), cancellationToken);

    public Task SetRepeatAsync(RepeatMode repeat, CancellationToken cancellationToken = default) => ChangeAsync(player =>
    {
        if (queue is null)
        {
            return;
        }

        queue.SetRepeat(repeat);
        player.RepeatMode = repeat switch
        {
            RepeatMode.One => RepeatModeOne,
            RepeatMode.All => RepeatModeAll,
            _ => RepeatModeOff,
        };
    }, cancellationToken);

    /// <summary>
    /// Re-orders the ExoPlayer playlist around the current item without interrupting it:
    /// drop everything before and after it, then insert the new head and tail.
    /// </summary>
    public Task SetShuffleAsync(bool shuffle, CancellationToken cancellationToken = default) => ChangeAsync(player =>
    {
        if (queue is null || queue.Shuffle == shuffle)
        {
            return;
        }

        queue.MoveTo(player.CurrentMediaItemIndex);
        queue.SetShuffle(shuffle);

        var items = queue.Order.Select(ToMediaItem).ToList();
        var current = queue.Position;
        player.RemoveMediaItems(player.CurrentMediaItemIndex + 1, player.MediaItemCount);
        player.RemoveMediaItems(0, player.CurrentMediaItemIndex);
        for (var i = 0; i < current; i++)
        {
            player.AddMediaItem(i, items[i]);
        }

        foreach (var item in items.Skip(current + 1))
        {
            player.AddMediaItem(item);
        }
    }, cancellationToken);

    public Task<PlayerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => ReadAsync(cancellationToken);

    public IDisposable Subscribe(Func<PlayerSnapshot, Task> onChanged) => broadcaster.Subscribe(onChanged);

    public ValueTask DisposeAsync() => broadcaster.DisposeAsync();

    private async Task ChangeAsync(Action<AndroidX.Media3.ExoPlayer.IExoPlayer> action, CancellationToken cancellationToken)
    {
        await OnMainThreadAsync(() => action(host.Player));
        await broadcaster.RefreshAsync(cancellationToken);
    }

    private Task<PlayerSnapshot> ReadAsync(CancellationToken cancellationToken) => OnMainThread<PlayerSnapshot>(() =>
    {
        if (queue is null || !host.IsCreated)
        {
            return PlayerSnapshot.Empty;
        }

        var player = host.Player;
        if (player.MediaItemCount == 0)
        {
            return PlayerSnapshot.Empty;
        }

        // The notification and headset buttons skip tracks without telling us: follow the player.
        var index = Math.Clamp(player.CurrentMediaItemIndex, 0, queue.Order.Count - 1);
        queue.MoveTo(index);
        var current = queue.Current;
        var duration = player.Duration;
        var length = duration == C.TimeUnset
            ? TimeSpan.FromSeconds(Math.Max(current.DurationSeconds, 0))
            : TimeSpan.FromMilliseconds(duration);

        return new PlayerSnapshot(
            current, TimeSpan.FromMilliseconds(Math.Max(player.CurrentPosition, 0)), length, player.IsPlaying,
            queue.Shuffle, queue.Repeat, index, queue.Order.Count);
    });

    private async Task EnsureNotificationPermissionAsync()
    {
        // Android 13+: without it the playback notification stays hidden (the service still runs).
        if (notificationPermissionAsked || !OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        notificationPermissionAsked = true;
        await MainThread.InvokeOnMainThreadAsync(async () => await Permissions.RequestAsync<Permissions.PostNotifications>());
    }

    private static MediaItem ToMediaItem(Track track)
    {
        var builder = new MediaMetadata.Builder()
            .SetTitle(track.Title)!
            .SetArtist(track.Artist)!
            .SetAlbumTitle(track.Album)!;
        if (track.CoverPath.Length > 0)
        {
            builder.SetArtworkUri(Android.Net.Uri.FromFile(new Java.IO.File(track.CoverPath)));
        }

        var metadata = builder.Build()!;

        return new MediaItem.Builder()
            .SetMediaId(track.Id.ToString())!
            .SetUri(Android.Net.Uri.FromFile(new Java.IO.File(track.FilePath)))!
            .SetMediaMetadata(metadata)!
            .Build()!;
    }

    private static Task OnMainThreadAsync(Action action) => MainThread.InvokeOnMainThreadAsync(action);

    private static Task<T> OnMainThread<T>(Func<T> func) => MainThread.InvokeOnMainThreadAsync(func);
}
