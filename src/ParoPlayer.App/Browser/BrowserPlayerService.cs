using Microsoft.JSInterop;
using ParoPlayer.Core.Models;
using ParoPlayer.Core.Playback;

namespace ParoPlayer.App.Browser;

/// <summary>
/// The player of one browser session (#26): the queue lives here, the sound comes from an HTML audio element that
/// streams <c>/media/{id}</c> from the phone. The element is the clock: its position is polled, and when a track
/// ends the queue moves on.
/// </summary>
public sealed class BrowserPlayerService : IPlayerService, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IJSRuntime js;
    private readonly Random random;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly PlayerStateBroadcaster broadcaster;
    private PlayQueue? queue;
    private bool playing;

    public BrowserPlayerService(IJSRuntime js, TimeProvider timeProvider, Random random)
    {
        this.js = js;
        this.random = random;
        broadcaster = new PlayerStateBroadcaster(ReadAsync, timeProvider, PollInterval);
    }

    public Task PlayAsync(IReadOnlyList<Track> tracks, int startIndex = 0, CancellationToken cancellationToken = default) => ChangeAsync(async () =>
    {
        queue = new PlayQueue(tracks, startIndex, random);
        playing = true;
        await LoadCurrentAsync();
    }, cancellationToken);

    public Task PauseAsync(CancellationToken cancellationToken = default) => ChangeAsync(async () =>
    {
        playing = false;
        await js.InvokeVoidAsync("paroAudio.pause");
    }, cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) => ChangeAsync(async () =>
    {
        if (queue is null)
        {
            return;
        }

        playing = true;
        await js.InvokeVoidAsync("paroAudio.play");
    }, cancellationToken);

    public Task NextAsync(CancellationToken cancellationToken = default) => ChangeAsync(async () =>
    {
        if (queue?.MoveNext() == true)
        {
            await LoadCurrentAsync();
        }
    }, cancellationToken);

    public Task PreviousAsync(CancellationToken cancellationToken = default) => ChangeAsync(async () =>
    {
        if (queue is null)
        {
            return;
        }

        var state = await ReadAudioAsync();
        if (queue.MovePrevious(TimeSpan.FromSeconds(state.Position)))
        {
            await LoadCurrentAsync();
        }
        else
        {
            await js.InvokeVoidAsync("paroAudio.seek", 0);
        }
    }, cancellationToken);

    public Task SeekToAsync(TimeSpan position, CancellationToken cancellationToken = default) => ChangeAsync(async () =>
    {
        if (queue is not null)
        {
            await js.InvokeVoidAsync("paroAudio.seek", Math.Max(position.TotalSeconds, 0));
        }
    }, cancellationToken);

    public Task SetShuffleAsync(bool shuffle, CancellationToken cancellationToken = default) => ChangeAsync(() =>
    {
        queue?.SetShuffle(shuffle);
        return Task.CompletedTask;
    }, cancellationToken);

    public Task SetRepeatAsync(RepeatMode repeat, CancellationToken cancellationToken = default) => ChangeAsync(() =>
    {
        queue?.SetRepeat(repeat);
        return Task.CompletedTask;
    }, cancellationToken);

    public Task RemoveTracksAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken = default) => ChangeAsync(async () =>
    {
        if (queue is null)
        {
            return;
        }

        var removal = queue.Remove(trackIds);
        if (removal.IsEmpty)
        {
            queue = null;
            playing = false;
            await js.InvokeVoidAsync("paroAudio.stop");
        }
        else if (removal.CurrentRemoved)
        {
            playing = playing && !removal.RanOut;
            await LoadCurrentAsync();
        }
    }, cancellationToken);

    public Task<PlayerSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => ReadAsync(cancellationToken);

    public IDisposable Subscribe(Func<PlayerSnapshot, Task> onChanged) => broadcaster.Subscribe(onChanged);

    public async ValueTask DisposeAsync()
    {
        await broadcaster.DisposeAsync();
        gate.Dispose();
    }

    private async Task ChangeAsync(Func<Task> change, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await change();
        }
        finally
        {
            gate.Release();
        }

        await broadcaster.RefreshAsync(cancellationToken);
    }

    private async Task<PlayerSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (queue is null)
            {
                return PlayerSnapshot.Empty;
            }

            var state = await ReadAudioAsync();
            if (state.Ended && playing)
            {
                if (queue.Advance())
                {
                    await LoadCurrentAsync();
                    state = await ReadAudioAsync();
                }
                else
                {
                    playing = false;
                }
            }

            var current = queue.Current;
            var duration = current.DurationSeconds > 0 ? current.DurationSeconds : state.Duration;
            return new PlayerSnapshot(
                current, TimeSpan.FromSeconds(state.Position), TimeSpan.FromSeconds(duration), playing,
                queue.Shuffle, queue.Repeat, queue.Position, queue.Order.Count);
        }
        catch (JSDisconnectedException)
        {
            return PlayerSnapshot.Empty;
        }
        finally
        {
            gate.Release();
        }
    }

    private ValueTask LoadCurrentAsync() => js.InvokeVoidAsync("paroAudio.load", $"/media/{queue!.Current.Id}", playing);

    private async Task<AudioState> ReadAudioAsync() => await js.InvokeAsync<AudioState>("paroAudio.state");

    private sealed record AudioState(double Position, double Duration, bool Paused, bool Ended);
}
