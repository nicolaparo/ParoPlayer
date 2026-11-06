using Android.Graphics;
using ParoPlayer.Core.Import;

namespace ParoPlayer.App.Covers;

/// <summary>Decodes with BitmapFactory, centre-crops to a square and re-encodes as JPEG.</summary>
public sealed class AndroidCoverImageProcessor : ICoverImageProcessor
{
    public Task<byte[]?> ToSquareJpegAsync(byte[] image, int size, CancellationToken cancellationToken = default) =>
        Task.Run(() => Convert(image, size), cancellationToken);

    private static byte[]? Convert(byte[] image, int size)
    {
        using var source = BitmapFactory.DecodeByteArray(image, 0, image.Length);
        if (source is null)
        {
            return null;
        }

        var side = Math.Min(source.Width, source.Height);
        using var cropped = Bitmap.CreateBitmap(source, (source.Width - side) / 2, (source.Height - side) / 2, side, side);
        using var scaled = Bitmap.CreateScaledBitmap(cropped, size, size, true);
        using var output = new MemoryStream();
        return scaled!.Compress(Bitmap.CompressFormat.Jpeg!, 88, output) ? output.ToArray() : null;
    }
}
