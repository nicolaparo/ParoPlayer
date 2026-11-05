using ParoPlayer.Core.Models;

namespace ParoPlayer.Core.Data;

public interface IRepository<T> where T : class, IEntity, new()
{
    Task<IReadOnlyList<T>> GetAllAsync();

    Task<T?> GetAsync(Guid id);

    Task AddAsync(T item);

    Task UpdateAsync(T item);

    Task DeleteAsync(Guid id);
}
