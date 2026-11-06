using ParoPlayer.App.Services;
using ParoPlayer.Core.Models;

namespace ParoPlayer.App.Browser;

// Phone-only abilities the shared components ask for. In a desktop browser they do nothing (the browser has its own
// paste, share and file dialogs), so the components work unchanged.

/// <summary>What the host shows: the browser session has no Settings or Wi-Fi transfer screen.</summary>
public sealed record HostShell(bool ShowSettings);

internal sealed class BrowserHapticFeedback : IHapticFeedback
{
    public bool IsSupported => false;

    public void Perform(HapticFeedbackType type)
    {
    }
}

// Reading the clipboard needs a secure page, and the LAN address is plain HTTP; pasting with Ctrl+V still works.
internal sealed class BrowserClipboard : IClipboard
{
    public bool HasText => false;

    public event EventHandler<EventArgs>? ClipboardContentChanged
    {
        add { }
        remove { }
    }

    public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);

    public Task SetTextAsync(string? text) => Task.CompletedTask;
}

internal sealed class BrowserFilePicker : IFilePicker
{
    public Task<FileResult?> PickAsync(PickOptions? options = null) => Task.FromResult<FileResult?>(null);

    public Task<IEnumerable<FileResult>?> PickMultipleAsync(PickOptions? options = null) => Task.FromResult<IEnumerable<FileResult>?>([]);
}

internal sealed class BrowserTrackSharer : ITrackSharer
{
    public Task ShareAsync(IReadOnlyCollection<Track> tracks) => Task.CompletedTask;
}

internal sealed class BrowserMusicImporter : IMusicImporter
{
    public Task<int> PickAndImportAsync() => Task.FromResult(0);
}

internal sealed class BrowserNotificationPermission : INotificationPermission
{
    public Task EnsureAsync() => Task.CompletedTask;
}
