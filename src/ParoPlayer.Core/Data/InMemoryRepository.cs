using System.Collections.Concurrent;
using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Data;

public class InMemoryRepository<T> : IRepository<T> where T : class, IEntity, new()
{
    private readonly ConcurrentDictionary<Guid, T> items = new();

    public Task<IReadOnlyList<T>> GetAllAsync() => Task.FromResult<IReadOnlyList<T>>([.. items.Values]);

    public Task<T?> GetAsync(Guid id) => Task.FromResult(items.GetValueOrDefault(id));

    public Task AddAsync(T item)
    {
        items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T item)
    {
        items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        items.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
