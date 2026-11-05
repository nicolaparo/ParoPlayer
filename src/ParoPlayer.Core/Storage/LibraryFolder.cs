namespace ParoPlayer.Core.Storage;

public sealed class LibraryFolder(string path) : ILibraryFolder
{
    public const string NoMediaFileName = ".nomedia";

    public string Path { get; } = path;

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path);
        var marker = System.IO.Path.Combine(Path, NoMediaFileName);
        if (!File.Exists(marker))
        {
            await File.WriteAllBytesAsync(marker, [], cancellationToken);
        }
    }
}
