using ParoPlayer.Core.Data;
using ParoPlayer.Core.Engine;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Services;

public class PlaylistService(
    IRepository<Playlist> playlists,
    IRepository<PlaylistTrack> entries,
    IRepository<Track> tracks,
    ISmartPlaylistEngine engine)
{
    public async Task<Playlist> CreateManualAsync(string name)
    {
        var playlist = new Playlist { Name = name };
        await playlists.AddAsync(playlist);
        return playlist;
    }

    public async Task<Playlist> CreateSmartAsync(string name, SmartPlaylistDefinition definition)
    {
        var playlist = new Playlist { Name = name, IsSmart = true, SmartDefinition = definition.ToJson() };
        await playlists.AddAsync(playlist);
        return playlist;
    }

    public async Task AddTrackAsync(Guid playlistId, Guid trackId)
    {
        var current = await GetEntriesAsync(playlistId);
        await entries.AddAsync(new PlaylistTrack { PlaylistId = playlistId, TrackId = trackId, Position = current.Count });
    }

    public async Task RemoveTrackAsync(Guid playlistId, Guid trackId)
    {
        var current = await GetEntriesAsync(playlistId);
        var removed = current.FirstOrDefault(entry => entry.TrackId == trackId);
        if (removed is null)
        {
            return;
        }

        await entries.DeleteAsync(removed.Id);
        await RenumberAsync(current.Where(entry => entry.Id != removed.Id));
    }

    /// <summary>Applies the given track order. Tracks not listed keep their relative order after the listed ones.</summary>
    public async Task ReorderAsync(Guid playlistId, IReadOnlyList<Guid> orderedTrackIds)
    {
        var current = await GetEntriesAsync(playlistId);
        var ordered = orderedTrackIds
            .Select(trackId => current.FirstOrDefault(entry => entry.TrackId == trackId))
            .OfType<PlaylistTrack>()
            .Distinct()
            .ToList();

        await RenumberAsync(ordered.Concat(current.Except(ordered)));
    }

    public async Task<IReadOnlyList<Track>> GetTracksAsync(Guid playlistId)
    {
        var playlist = await playlists.GetAsync(playlistId)
            ?? throw new KeyNotFoundException($"Playlist {playlistId} not found.");
        var allTracks = await tracks.GetAllAsync();

        if (playlist.IsSmart && playlist.SmartDefinition is { } json)
        {
            return engine.Evaluate(allTracks, SmartPlaylistDefinition.FromJson(json));
        }

        var byId = allTracks.ToDictionary(track => track.Id);
        var current = await GetEntriesAsync(playlistId);
        return [.. current.Where(entry => byId.ContainsKey(entry.TrackId)).Select(entry => byId[entry.TrackId])];
    }

    private async Task<List<PlaylistTrack>> GetEntriesAsync(Guid playlistId)
    {
        var all = await entries.GetAllAsync();
        return [.. all.Where(entry => entry.PlaylistId == playlistId).OrderBy(entry => entry.Position)];
    }

    private async Task RenumberAsync(IEnumerable<PlaylistTrack> ordered)
    {
        var position = 0;
        foreach (var entry in ordered)
        {
            entry.Position = position++;
            await entries.UpdateAsync(entry);
        }
    }
}
