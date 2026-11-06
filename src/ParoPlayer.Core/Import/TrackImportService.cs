using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.Core.Import;

/// <summary>Copies picked audio into the private library folder and creates a <see cref="Track"/> for each file.</summary>
public class TrackImportService(ILibraryFolder folder, ITrackMetadataReader reader, LibraryService library, CoverStore covers)
{
    public async Task<Track> ImportAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        await folder.EnsureCreatedAsync(cancellationToken);

        var targetPath = UniquePath(SafeFileName(fileName));
        await using (var target = File.Create(targetPath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var metadata = await reader.ReadAsync(targetPath, cancellationToken);
        var track = new Track
        {
            Title = string.IsNullOrWhiteSpace(metadata.Title) ? Path.GetFileNameWithoutExtension(targetPath) : metadata.Title,
            Artist = metadata.Artist,
            Album = metadata.Album,
            Genre = metadata.Genre,
            Year = metadata.Year,
            DurationSeconds = metadata.DurationSeconds,
            TrackNumber = metadata.TrackNumber,
            FilePath = targetPath,
        };
        track.CoverPath = await covers.SaveAsync(track.Id, metadata.Cover, cancellationToken);
        return await library.AddTrackAsync(track);
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "track" : name;
    }

    private string UniquePath(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(folder.Path, fileName);
        for (var n = 2; File.Exists(candidate); n++)
        {
            candidate = Path.Combine(folder.Path, $"{baseName} ({n}){extension}");
        }

        return candidate;
    }
}
