namespace ParoPlayer.App.Services;

public interface IDiscogsTokenStore
{
    Task<string?> GetAsync();

    Task SetAsync(string token);

    Task ClearAsync();
}

public class SecureStorageDiscogsTokenStore(ISecureStorage secureStorage) : IDiscogsTokenStore
{
    private const string Key = "discogs-token";

    public Task<string?> GetAsync() => secureStorage.GetAsync(Key);

    public Task SetAsync(string token) => secureStorage.SetAsync(Key, token);

    public Task ClearAsync()
    {
        secureStorage.Remove(Key);
        return Task.CompletedTask;
    }
}
