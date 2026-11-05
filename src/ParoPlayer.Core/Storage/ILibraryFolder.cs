namespace ParoPlayer.Core.Storage;

/// <summary>The private folder that holds downloaded and imported audio files.</summary>
public interface ILibraryFolder
{
    string Path { get; }

    /// <summary>Creates the folder and its <c>.nomedia</c> marker if missing. Safe to call repeatedly.</summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}
