using System.ComponentModel;

namespace ParoPlayer.Core.Download;

/// <summary>Where the ffmpeg executable is, or null when this platform has none.</summary>
public interface IFfmpegLocator
{
    string? ExecutablePath { get; }
}

public sealed class NoFfmpegLocator : IFfmpegLocator
{
    public string? ExecutablePath => null;
}

public record ProcessResult(int ExitCode, string ErrorOutput);

public interface IProcessRunner
{
    /// <summary>Runs a process to completion and kills it when cancelled. Throws <see cref="Win32Exception"/> when it cannot start.</summary>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

/// <summary>Builds ffmpeg arguments. Output is always an m4a (AAC in MP4): AAC input is copied, anything else is encoded.</summary>
public static class FfmpegArguments
{
    public const string OutputExtension = ".m4a";

    public static IReadOnlyList<string> ToM4a(string inputPath, string outputPath, bool inputIsAac) =>
    [
        "-y", "-nostdin", "-hide_banner", "-loglevel", "error",
        "-i", inputPath,
        "-vn", "-map", "0:a:0",
        .. inputIsAac ? (string[])["-c:a", "copy"] : ["-c:a", "aac", "-b:a", "256k"],
        "-movflags", "+faststart",
        outputPath,
    ];
}

/// <summary>Converts with the ffmpeg executable. Falls back to the pass-through converter when the platform has no ffmpeg.</summary>
public sealed class FfmpegAudioConverter(IFfmpegLocator locator, IProcessRunner runner, PassThroughAudioConverter fallback) : IAudioConverter
{
    public async Task<string> ConvertAsync(AudioConversion request, CancellationToken cancellationToken = default)
    {
        if (locator.ExecutablePath is not { } ffmpeg)
        {
            return await fallback.ConvertAsync(request, cancellationToken);
        }

        var output = request.OutputBasePath + FfmpegArguments.OutputExtension;
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(ffmpeg, FfmpegArguments.ToM4a(request.InputPath, output, request.InputIsAac), cancellationToken);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            throw new AudioConversionException($"ffmpeg could not start: {ex.Message}", ex);
        }

        if (result.ExitCode != 0 || !File.Exists(output))
        {
            throw new AudioConversionException($"ffmpeg failed (exit {result.ExitCode}): {result.ErrorOutput.Trim()}");
        }

        File.Delete(request.InputPath);
        return output;
    }
}
