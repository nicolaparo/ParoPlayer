namespace ParoPlayer.Core.Navigation;

/// <summary>
/// What the Back button closes before the app leaves the page: the newest open sheet first, then the selection.
/// A layer is registered only while it is on screen and can handle Back; disposing it removes it.
/// </summary>
public sealed class BackStack
{
    private readonly List<Layer> layers = [];

    public IDisposable Register(Func<Task> handle)
    {
        var layer = new Layer(this, handle);
        layers.Add(layer);
        return layer;
    }

    /// <summary>Starts the newest layer's handler. Returns null when none is open and Back should leave the page.</summary>
    public Task? PressAsync() => layers.Count == 0 ? null : layers[^1].Handle();

    private sealed class Layer(BackStack owner, Func<Task> handle) : IDisposable
    {
        public Func<Task> Handle { get; } = handle;

        public void Dispose() => owner.layers.Remove(this);
    }
}
