using SQLite;

namespace ParoPlayer.Core.Models;

public class PlaylistTrack : IEntity
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed]
    public Guid PlaylistId { get; set; }

    public Guid TrackId { get; set; }

    public int Position { get; set; }
}
