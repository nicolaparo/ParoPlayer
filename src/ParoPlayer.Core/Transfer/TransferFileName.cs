namespace ParoPlayer.Core.Transfer;

/// <summary>Turns a client supplied file name into a safe plain audio file name, or null when it is not acceptable.</summary>
public static class TransferFileName
{
    private const int MaxLength = 150;

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac", ".flac", ".wav", ".ogg", ".oga", ".opus", ".wma", ".aif", ".aiff",
    };

    public static string? Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var leaf = Path.GetFileName(name.Replace('\\', '/'));
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(leaf.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().Trim('.');
        var extension = Path.GetExtension(cleaned);
        if (!AudioExtensions.Contains(extension))
        {
            return null;
        }

        if (cleaned.Length > MaxLength)
        {
            cleaned = string.Concat(cleaned.AsSpan(0, MaxLength - extension.Length), extension);
        }

        return Path.GetFileNameWithoutExtension(cleaned).Trim().Length == 0 ? null : cleaned;
    }
}
