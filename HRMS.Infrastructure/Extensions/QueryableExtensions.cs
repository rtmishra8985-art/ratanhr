using Microsoft.EntityFrameworkCore;
using HRMS.Application.Common;

namespace HRMS.Infrastructure.Extensions;

/// <summary>
/// EF Core–compatible pagination extension that issues exactly two SQL queries:
/// one COUNT and one SELECT with OFFSET/FETCH (Skip/Take). No in-memory pagination.
/// </summary>
public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<T>.Create(items, totalCount, page, pageSize);
    }
}
