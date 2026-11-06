using ParoPlayer.Core.Download;

namespace ParoPlayer.App.Downloads;

/// <summary>
/// ffmpeg ships as <c>lib/arm64-v8a/libffmpeg.so</c> (an executable renamed so the package installer extracts it).
/// Android only allows executing files from the app's native library directory. Missing on x86_64 emulators.
/// </summary>
public class AndroidFfmpegLocator : IFfmpegLocator
{
    private const string FileName = "libffmpeg.so";

    public string? ExecutablePath
    {
        get
        {
            var directory = Android.App.Application.Context.ApplicationInfo?.NativeLibraryDir;
            var path = directory is null ? null : Path.Combine(directory, FileName);
            return path is not null && File.Exists(path) ? path : null;
        }
    }
}
