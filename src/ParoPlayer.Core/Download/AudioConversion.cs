namespace ParoPlayer.Core.Download;

public record AudioConversion(string InputPath, string OutputBasePath, bool InputIsAac);

public class AudioConversionException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Turns a downloaded audio file into a taggable file. Returns the path of the result.</summary>
public interface IAudioConverter
{
    /// <exception cref="AudioConversionException">The conversion could not be done.</exception>
    Task<string> ConvertAsync(AudioConversion request, CancellationToken cancellationToken = default);
}

/// <summary>Keeps the downloaded file as it is, only moving it to the requested name.</summary>
public sealed class PassThroughAudioConverter : IAudioConverter
{
    public Task<string> ConvertAsync(AudioConversion request, CancellationToken cancellationToken = default)
    {
        var target = request.OutputBasePath + Path.GetExtension(request.InputPath);
        File.Move(request.InputPath, target, overwrite: true);
        return Task.FromResult(target);
    }
}
