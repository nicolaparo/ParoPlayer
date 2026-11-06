namespace ParoPlayer.Core.Storage;

public static class LibraryStorage
{
    /// <summary>Total size in bytes of the files in the library folder (0 when it does not exist yet).</summary>
    public static Task<long> MeasureAsync(ILibraryFolder folder, CancellationToken cancellationToken = default) =>
        Task.Run(
            () => Directory.Exists(folder.Path)
                ? Directory.EnumerateFiles(folder.Path).Sum(file => new FileInfo(file).Length)
                : 0L,
            cancellationToken);
}
