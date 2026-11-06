using System.Globalization;
using System.Text.Json;

namespace ParoPlayer.Core.Tagging;

/// <summary>Plain HTTP GET behind an interface so tests never touch the network. Returns null on 404.</summary>
public interface IHttpFetcher
{
    Task<byte[]?> GetAsync(Uri url, CancellationToken cancellationToken = default);
}

/// <summary>Real fetcher: descriptive User-Agent, and at most one request per second to MusicBrainz.</summary>
public sealed class HttpClientFetcher(HttpClient http, TimeProvider time) : IHttpFetcher
{
    private const string UserAgent = "ParoPlayer/1.0 (personal offline music player; https://github.com/nicolaparo/ParoPlayer)";
    private static readonly TimeSpan MusicBrainzInterval = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextSlot = DateTimeOffset.MinValue;

    public async Task<byte[]?> GetAsync(Uri url, CancellationToken cancellationToken = default)
    {
        if (url.Host.EndsWith("musicbrainz.org", StringComparison.OrdinalIgnoreCase))
        {
            await WaitForSlotAsync(cancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task WaitForSlotAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var wait = nextSlot - time.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, cancellationToken);
            }

            nextSlot = time.GetUtcNow() + MusicBrainzInterval;
        }
        finally
        {
            gate.Release();
        }
    }
}

public record MetadataCandidate(string Source, string Title, string Artist, string Album, string Genre, int Year, int TrackNumber, Uri? CoverUrl)
{
    /// <summary>The edit this candidate proposes. Fields the source does not know keep the current value.</summary>
    public TrackEdit ApplyTo(TrackEdit current) => new(
        Title.Length > 0 ? Title : current.Title,
        Artist.Length > 0 ? Artist : current.Artist,
        Album.Length > 0 ? Album : current.Album,
        Genre.Length > 0 ? Genre : current.Genre,
        Year > 0 ? Year : current.Year,
        TrackNumber > 0 ? TrackNumber : current.TrackNumber);
}

public interface IMetadataSource
{
    Task<IReadOnlyList<MetadataCandidate>> SearchAsync(string title, string artist, CancellationToken cancellationToken = default);
}

internal static class JsonReading
{
    public static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    public static int Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : 0;

    public static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    /// <summary>Year from a date such as "2013-05-17" or "2013-05-17T07:00:00Z".</summary>
    public static int Year(string date) =>
        date.Length >= 4 && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? year : 0;
}

/// <summary>MusicBrainz recording search; cover art comes from the Cover Art Archive for the matched release.</summary>
public sealed class MusicBrainzSource(IHttpFetcher http) : IMetadataSource
{
    public async Task<IReadOnlyList<MetadataCandidate>> SearchAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        var query = $"recording:\"{Escape(title)}\"" + (artist.Length > 0 ? $" AND artist:\"{Escape(artist)}\"" : "");
        var url = new Uri($"https://musicbrainz.org/ws/2/recording?query={Uri.EscapeDataString(query)}&fmt=json&limit=5");
        var body = await http.GetAsync(url, cancellationToken);
        if (body is null)
        {
            return [];
        }

        using var json = JsonDocument.Parse(body);
        return [.. JsonReading.Items(json.RootElement, "recordings").Select(ToCandidate).OfType<MetadataCandidate>()];
    }

    private static MetadataCandidate? ToCandidate(JsonElement recording)
    {
        var title = JsonReading.Text(recording, "title");
        if (title.Length == 0)
        {
            return null;
        }

        var artist = string.Concat(JsonReading.Items(recording, "artist-credit").Select(credit => JsonReading.Text(credit, "name") + JsonReading.Text(credit, "joinphrase")));
        var release = JsonReading.Items(recording, "releases").FirstOrDefault();
        var hasRelease = release.ValueKind == JsonValueKind.Object;
        var releaseId = hasRelease ? JsonReading.Text(release, "id") : "";
        var date = hasRelease ? JsonReading.Text(release, "date") : "";
        if (date.Length == 0)
        {
            date = JsonReading.Text(recording, "first-release-date");
        }

        var trackNumber = hasRelease
            ? JsonReading.Items(release, "media").SelectMany(media => JsonReading.Items(media, "track"))
                .Select(track => int.TryParse(JsonReading.Text(track, "number"), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .FirstOrDefault()
            : 0;
        var genre = JsonReading.Items(recording, "tags")
            .OrderByDescending(tag => JsonReading.Number(tag, "count"))
            .Select(tag => JsonReading.Text(tag, "name"))
            .FirstOrDefault() ?? "";

        return new MetadataCandidate(
            "MusicBrainz",
            title,
            artist,
            hasRelease ? JsonReading.Text(release, "title") : "",
            Capitalize(genre),
            JsonReading.Year(date),
            trackNumber,
            releaseId.Length > 0 ? new Uri($"https://coverartarchive.org/release/{releaseId}/front-500") : null);
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : string.Create(CultureInfo.InvariantCulture, $"{char.ToUpperInvariant(value[0])}{value[1..]}");
}

/// <summary>iTunes Search API (no key needed). Fallback when MusicBrainz finds nothing.</summary>
public sealed class ITunesSource(IHttpFetcher http) : IMetadataSource
{
    public async Task<IReadOnlyList<MetadataCandidate>> SearchAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        var term = Uri.EscapeDataString($"{title} {artist}".Trim());
        var body = await http.GetAsync(new Uri($"https://itunes.apple.com/search?term={term}&entity=song&limit=5"), cancellationToken);
        if (body is null)
        {
            return [];
        }

        using var json = JsonDocument.Parse(body);
        return [.. JsonReading.Items(json.RootElement, "results").Select(ToCandidate).OfType<MetadataCandidate>()];
    }

    private static MetadataCandidate? ToCandidate(JsonElement item)
    {
        var title = JsonReading.Text(item, "trackName");
        if (title.Length == 0)
        {
            return null;
        }

        var artwork = JsonReading.Text(item, "artworkUrl100").Replace("100x100bb", "600x600bb");
        return new MetadataCandidate(
            "iTunes",
            title,
            JsonReading.Text(item, "artistName"),
            JsonReading.Text(item, "collectionName"),
            JsonReading.Text(item, "primaryGenreName"),
            JsonReading.Year(JsonReading.Text(item, "releaseDate")),
            JsonReading.Number(item, "trackNumber"),
            Uri.TryCreate(artwork, UriKind.Absolute, out var cover) ? cover : null);
    }
}

/// <summary>Asks each source in order (MusicBrainz, then iTunes) and returns the first non-empty answer.</summary>
public class MetadataLookupService(IEnumerable<IMetadataSource> sources, IHttpFetcher http)
{
    public async Task<IReadOnlyList<MetadataCandidate>> SearchAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        foreach (var source in sources)
        {
            try
            {
                var found = await source.SearchAsync(title.Trim(), artist.Trim(), cancellationToken);
                if (found.Count > 0)
                {
                    return found;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                // Offline or the source failed: try the next one.
            }
        }

        return [];
    }

    /// <summary>Downloads the candidate's cover, or returns null when it has none or the download fails.</summary>
    public async Task<byte[]?> FetchCoverAsync(MetadataCandidate candidate, CancellationToken cancellationToken = default)
    {
        if (candidate.CoverUrl is null)
        {
            return null;
        }

        try
        {
            return await http.GetAsync(candidate.CoverUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }
}
