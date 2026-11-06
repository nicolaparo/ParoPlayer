using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Import;

/// <summary>Turns an image into a square cover (platform specific) and saves it as a file next to the audio.</summary>
public interface ICoverImageProcessor
{
    /// <summary>Returns a JPEG of <paramref name="size"/> pixels, centre-cropped to a square. Returns null when the bytes are not an image.</summary>
    Task<byte[]?> ToSquareJpegAsync(byte[] image, int size, CancellationToken cancellationToken = default);
}

/// <summary>Keeps the image as is. Used where there is no image decoder (tests, Windows preview).</summary>
public sealed class PassThroughCoverImageProcessor : ICoverImageProcessor
{
    public Task<byte[]?> ToSquareJpegAsync(byte[] image, int size, CancellationToken cancellationToken = default) =>
        Task.FromResult<byte[]?>(image);
}

public class CoverStore(ILibraryFolder folder, ICoverImageProcessor processor)
{
    public const int Size = 512;
    public const string FolderName = "covers";

    /// <summary>Saves the cover for a track and returns its path, or an empty string when there is no usable cover.</summary>
    public async Task<string> SaveAsync(Guid trackId, byte[]? image, CancellationToken cancellationToken = default)
    {
        if (image is not { Length: > 0 })
        {
            return "";
        }

        var square = await processor.ToSquareJpegAsync(image, Size, cancellationToken);
        if (square is not { Length: > 0 })
        {
            return "";
        }

        var directory = Path.Combine(folder.Path, FolderName);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{trackId:N}.jpg");
        await File.WriteAllBytesAsync(path, square, cancellationToken);
        return path;
    }
}
