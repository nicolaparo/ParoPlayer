using System.Globalization;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Library;

/// <summary>A row of the Albums, Artists, Genres or Folders tab.</summary>
public record LibraryGroup(string Name, string Subtitle, int Count);

/// <summary>Pure helpers behind the Library screen: search, grouping and display formatting.</summary>
public static class LibraryQuery
{
    public const string Unknown = "Unknown";

    public static IReadOnlyList<Track> Search(IEnumerable<Track> tracks, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. tracks];
        }

        return [.. tracks.Where(track =>
            track.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || track.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)
            || track.Album.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Albums with the artist of the first track as subtitle.</summary>
    public static IReadOnlyList<LibraryGroup> Albums(IEnumerable<Track> tracks) =>
        [.. tracks.GroupBy(track => Label(track.Album))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LibraryGroup(group.Key, Label(group.First().Artist), group.Count()))];

    public static IReadOnlyList<LibraryGroup> Artists(IEnumerable<Track> tracks) => Group(tracks, track => track.Artist);

    public static IReadOnlyList<LibraryGroup> Genres(IEnumerable<Track> tracks) => Group(tracks, track => track.Genre);

    public static IReadOnlyList<LibraryGroup> Folders(IEnumerable<Track> tracks) =>
        Group(tracks, track => Path.GetFileName(Path.GetDirectoryName(track.FilePath) ?? ""));

    public static string FormatDuration(int seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(seconds, 0));
        return span.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}:{span.Seconds:00}");
    }

    public static string FormatTotalDuration(IEnumerable<Track> tracks)
    {
        var minutes = (int)Math.Round(tracks.Sum(track => track.DurationSeconds) / 60.0);
        return minutes >= 60
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} h {minutes % 60} min")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes} min");
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 30):0.0} GB"),
        >= 1L << 20 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 20):0.0} MB"),
        >= 1L << 10 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 10):0} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
    };

    /// <summary>"Artist - Album", skipping whichever part is empty.</summary>
    public static string Subtitle(Track track) =>
        string.Join(" - ", new[] { track.Artist, track.Album }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public static string Pluralize(int count, string singular) => count == 1 ? $"1 {singular}" : $"{count} {singular}s";

    private static IReadOnlyList<LibraryGroup> Group(IEnumerable<Track> tracks, Func<Track, string> key) =>
        [.. tracks.GroupBy(track => Label(key(track)))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LibraryGroup(group.Key, Pluralize(group.Count(), "song"), group.Count()))];

    private static string Label(string value) => string.IsNullOrWhiteSpace(value) ? Unknown : value;
}
