using ParoPlayer.Core.Download;

namespace ParoPlayer.Core.Tests;

public sealed class TagLibTrackTagWriterTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public TagLibTrackTagWriterTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    /// <summary>A few silent MPEG-1 Layer III frames (128 kbps, 44.1 kHz, 417 bytes each): enough for TagLib to accept the file.</summary>
    private string SilentMp3()
    {
        var frame = new byte[417];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = 0x90;
        var bytes = Enumerable.Range(0, 8).SelectMany(_ => frame).ToArray();
        var path = Path.Combine(dir, "silent.mp3");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task Write_StoresTagsAndCover()
    {
        var path = SilentMp3();

        await new TagLibTrackTagWriter().WriteAsync(path, new TrackTags("Get Lucky", "Daft Punk", "RAM", 2013), [0xFF, 0xD8, 0xFF, 0xD9]);

        using var file = TagLib.File.Create(path);
        Assert.Equal(("Get Lucky", "Daft Punk", "RAM", 2013u), (file.Tag.Title, file.Tag.FirstPerformer, file.Tag.Album, file.Tag.Year));
        Assert.Single(file.Tag.Pictures);
    }

    [Fact]
    public async Task Write_UnreadableFile_Throws()
    {
        var path = Path.Combine(dir, "broken.m4a");
        await File.WriteAllTextAsync(path, "not audio");

        await Assert.ThrowsAnyAsync<Exception>(() => new TagLibTrackTagWriter().WriteAsync(path, new TrackTags("t", "a"), null));
    }
}
