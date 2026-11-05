using SQLite;

namespace ParoPlayer.Core.Models;

public class Playlist : IEntity
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public bool IsSmart { get; set; }

    /// <summary>JSON of a <see cref="SmartPlaylistDefinition"/>; null for manual playlists.</summary>
    public string? SmartDefinition { get; set; }
}
