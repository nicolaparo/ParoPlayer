using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Engine;

public interface ISmartPlaylistEngine
{
    IReadOnlyList<Track> Evaluate(IEnumerable<Track> tracks, SmartPlaylistDefinition definition);
}
