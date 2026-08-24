using System.Linq.Expressions;

namespace HRMS.Infrastructure.Repositories;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Paged read — avoids loading entire tables into memory.
    /// Page is 1-based. PageSize is capped at 500 to prevent runaway queries.
    /// </summary>
    Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int page, int pageSize);

    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    Task SaveChangesAsync();
}
