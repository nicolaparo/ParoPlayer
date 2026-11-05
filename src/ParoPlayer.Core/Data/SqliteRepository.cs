using ParoPlayer.Core.Models;
using SQLite;

namespace ParoPlayer.Core.Data;

public class SqliteRepository<T>(SQLiteAsyncConnection connection) : IRepository<T>
    where T : class, IEntity, new()
{
    private Task<CreateTableResult> EnsureTableAsync() => connection.CreateTableAsync<T>();

    public async Task<IReadOnlyList<T>> GetAllAsync()
    {
        await EnsureTableAsync();
        return await connection.Table<T>().ToListAsync();
    }

    public async Task<T?> GetAsync(Guid id)
    {
        await EnsureTableAsync();
        return await connection.FindAsync<T>(id);
    }

    public async Task AddAsync(T item)
    {
        await EnsureTableAsync();
        await connection.InsertAsync(item);
    }

    public async Task UpdateAsync(T item)
    {
        await EnsureTableAsync();
        await connection.UpdateAsync(item);
    }

    public async Task DeleteAsync(Guid id)
    {
        await EnsureTableAsync();
        await connection.DeleteAsync<T>(id);
    }
}
