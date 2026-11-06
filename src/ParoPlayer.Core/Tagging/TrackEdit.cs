using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Tagging;

/// <summary>The user-editable fields of a song.</summary>
public record TrackEdit(string Title, string Artist, string Album, string Genre, int Year, int TrackNumber)
{
    public static TrackEdit From(Track track) =>
        new(track.Title, track.Artist, track.Album, track.Genre, track.Year, track.TrackNumber);
}

public record FieldChange(string Field, string Before, string After);

public static class TrackEditDiff
{
    /// <summary>Fields whose value differs, for the preview shown before a lookup result is applied.</summary>
    public static IReadOnlyList<FieldChange> Between(TrackEdit before, TrackEdit after)
    {
        List<FieldChange> changes = [];
        Add(changes, "Title", before.Title, after.Title);
        Add(changes, "Artist", before.Artist, after.Artist);
        Add(changes, "Album", before.Album, after.Album);
        Add(changes, "Genre", before.Genre, after.Genre);
        Add(changes, "Year", Number(before.Year), Number(after.Year));
        Add(changes, "Track", Number(before.TrackNumber), Number(after.TrackNumber));
        return changes;
    }

    private static string Number(int value) => value > 0 ? value.ToString() : "";

    private static void Add(List<FieldChange> changes, string field, string before, string after)
    {
        if (!string.Equals(before.Trim(), after.Trim(), StringComparison.Ordinal))
        {
            changes.Add(new FieldChange(field, before, after));
        }
    }
}
