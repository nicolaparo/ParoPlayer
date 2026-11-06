using SQLite;

namespace ParoPlayer.Core.Models;

public class Track : IEntity
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "";

    public string Artist { get; set; } = "";

    public string Album { get; set; } = "";

    public string Genre { get; set; } = "";

    public int Year { get; set; }

    public int DurationSeconds { get; set; }

    public string FilePath { get; set; } = "";

    /// <summary>0 (unrated) to 5.</summary>
    public int Rating { get; set; }

    public int PlayCount { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public DateTimeOffset? LastPlayedAt { get; set; }

    public int TrackNumber { get; set; }

    /// <summary>Square cover image inside the library folder, or empty when the track has none.</summary>
    public string CoverPath { get; set; } = "";
}
