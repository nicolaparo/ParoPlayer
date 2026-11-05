using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Tests;

public class LibraryFolderTests
{
    [Fact]
    public async Task EnsureCreatedAsync_CreatesFolderWithNoMediaMarker()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName(), "library");
        var folder = new LibraryFolder(root);

        await folder.EnsureCreatedAsync();
        await folder.EnsureCreatedAsync();

        Assert.True(File.Exists(System.IO.Path.Combine(root, LibraryFolder.NoMediaFileName)));
        Directory.Delete(System.IO.Path.GetDirectoryName(root)!, recursive: true);
    }
}
