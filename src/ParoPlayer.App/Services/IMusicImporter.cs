using ParoPlayer.Core.Import;

namespace ParoPlayer.App.Services;

public interface IMusicImporter
{
    /// <summary>Opens the system file picker and imports the chosen audio files. Returns how many were imported.</summary>
    Task<int> PickAndImportAsync();
}

public class MauiMusicImporter(IFilePicker filePicker, TrackImportService importer) : IMusicImporter
{
    private static readonly PickOptions Options = new()
    {
        PickerTitle = "Import music",
        FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = new[] { "audio/*" },
            [DevicePlatform.WinUI] = new[] { ".mp3", ".m4a", ".aac", ".flac", ".ogg", ".opus", ".wav" },
        }),
    };

    public async Task<int> PickAndImportAsync()
    {
        var files = await filePicker.PickMultipleAsync(Options);
        var count = 0;
        foreach (var file in files ?? [])
        {
            await using var stream = await file.OpenReadAsync();
            await importer.ImportAsync(file.FileName, stream);
            count++;
        }

        return count;
    }
}
