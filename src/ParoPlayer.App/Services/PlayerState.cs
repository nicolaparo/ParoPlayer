using ParoPlayer.Core.Models;
using ParoPlayer.Core.Playback;

namespace ParoPlayer.App.Services;

/// <summary>
/// The latest <see cref="PlayerSnapshot"/> for the UI. It listens to <see cref="IPlayerService"/> once and lets
/// components subscribe with an awaited callback (<c>() =&gt; InvokeAsync(StateHasChanged)</c>).
/// </summary>
public sealed class PlayerState : IDisposable
{
    private readonly Lock gate = new();
    private readonly List<Func<Task>> changed = [];
    private readonly List<Func<Task>> progressed = [];
    private readonly IDisposable subscription;

    public PlayerState(IPlayerService player)
    {
        subscription = player.Subscribe(OnSnapshotAsync);
    }

    public PlayerSnapshot Snapshot { get; private set; } = PlayerSnapshot.Empty;

    public Track? Current => Snapshot.Current;

    /// <summary>Calls <paramref name="handler"/> when the track, play/pause, shuffle or repeat change (not on position ticks).</summary>
    public IDisposable Subscribe(Func<Task> handler) => Add(changed, handler);

    /// <summary>Calls <paramref name="handler"/> on every snapshot, including position ticks.</summary>
    public IDisposable SubscribeProgress(Func<Task> handler) => Add(progressed, handler);

    public void Dispose() => subscription.Dispose();

    private async Task OnSnapshotAsync(PlayerSnapshot snapshot)
    {
        var previous = Snapshot;
        Snapshot = snapshot;

        Func<Task>[] targets;
        lock (gate)
        {
            targets = previous.SameExceptPosition(snapshot) ? [.. progressed] : [.. changed, .. progressed];
        }

        foreach (var target in targets)
        {
            await target();
        }
    }

    private Subscription Add(List<Func<Task>> list, Func<Task> handler)
    {
        lock (gate)
        {
            list.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (gate)
            {
                list.Remove(handler);
            }
        });
    }

    private sealed class Subscription(Action remove) : IDisposable
    {
        public void Dispose() => remove();
    }
}
