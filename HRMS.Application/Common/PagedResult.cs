using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Common
{
    /// <summary>
    /// Generic paginated result wrapper returned by all list endpoints.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>Items on the current page.</summary>
        public List<T> Items { get; init; } = new();

        /// <summary>Total number of items across all pages.</summary>
        public int TotalCount { get; init; }

        /// <summary>Current page number (1-based).</summary>
        public int Page { get; init; }

        /// <summary>Maximum items per page.</summary>
        public int PageSize { get; init; }

        /// <summary>Total number of pages.</summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

        /// <summary>Whether a next page exists.</summary>
        public bool HasNext => Page < TotalPages;

        /// <summary>Whether a previous page exists.</summary>
        public bool HasPrevious => Page > 1;

        /// <summary>
        /// The column that was sorted on (echoed from the request).
        /// Null when the default sort was applied.
        /// </summary>
        public string? SortBy { get; init; }

        /// <summary>
        /// The sort direction applied: "asc" or "desc" (echoed from the request).
        /// </summary>
        public string? SortDirection { get; init; }

        /// <summary>
        /// Factory method — creates a <see cref="PagedResult{T}"/> from a pre-fetched item list.
        /// Use this overload when you have already materialised the page from a query.
        /// </summary>
        public static PagedResult<T> Create(
            List<T> items,
            int totalCount,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortDirection = null) =>
            new()
            {
                Items         = items,
                TotalCount    = totalCount,
                Page          = page,
                PageSize      = pageSize,
                SortBy        = sortBy,
                SortDirection = sortDirection,
            };
    }

    /// <summary>
    /// Extension methods that materialize an <see cref="IQueryable{T}"/> into a <see cref="PagedResult{T}"/>.
    /// </summary>
    public static class PagedResultExtensions
    {
        /// <summary>
        /// Asynchronously counts and slices an EF Core queryable into a paged result.
        /// </summary>
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> source,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortDirection = null,
            CancellationToken ct = default)
        {
            var totalCount = await source.CountAsync(ct);
            var items = await source
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<T>
            {
                Items         = items,
                TotalCount    = totalCount,
                Page          = page,
                PageSize      = pageSize,
                SortBy        = sortBy,
                SortDirection = sortDirection,
            };
        }

        /// <summary>
        /// Synchronous in-memory version — use only for tests or small in-memory collections.
        /// </summary>
        public static PagedResult<T> ToPagedResult<T>(
            this IEnumerable<T> source,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortDirection = null)
        {
            var list = source.ToList();
            return new PagedResult<T>
            {
                Items         = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount    = list.Count,
                Page          = page,
                PageSize      = pageSize,
                SortBy        = sortBy,
                SortDirection = sortDirection,
            };
        }
    }
}
