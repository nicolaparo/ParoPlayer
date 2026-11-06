namespace ParoPlayer.App.Services;

/// <summary>Turns a cover file into a data URI the WebView can show. Results are cached because lists redraw often.</summary>
public class CoverImages
{
    private readonly Dictionary<string, string?> cache = [];

    /// <summary>Forgets a cover so the next <see cref="GetAsync"/> reads the file again (the path stays the same when a cover is replaced).</summary>
    public void Invalidate(string path) => cache.Remove(path);

    public async Task<string?> GetAsync(string path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        if (cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var uri = File.Exists(path) ? $"data:image/jpeg;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(path))}" : null;
        cache[path] = uri;
        return uri;
    }
}
