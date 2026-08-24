using System.Linq.Expressions;
using System.Reflection;

namespace HRMS.Application.Common;

/// <summary>
/// FIX 5 – Safe, SQL-injection-proof IQueryable sort extension.
///
/// Usage:
///   query = query.ApplySorting("name", "asc", defaultKeySelector: x => x.Name);
///
/// The allowed column map is built per-call from the <typeparamref name="T"/>
/// parameter's public properties, so no caller can inject arbitrary SQL.
/// Unknown column names fall back to the supplied default selector.
/// </summary>
public static class QueryableSortExtensions
{
    /// <summary>
    /// Applies an ORDER BY clause to <paramref name="query"/> using the supplied
    /// column name and direction, falling back to <paramref name="defaultSelector"/>
    /// when the column name is absent or not whitelisted.
    /// </summary>
    /// <typeparam name="T">Entity / projection type.</typeparam>
    /// <typeparam name="TDefault">Key type of the default sort expression.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="sortBy">Column name from the request query string (case-insensitive).</param>
    /// <param name="sortDirection">"asc" or "desc" (case-insensitive). Defaults to "asc".</param>
    /// <param name="defaultSelector">Fallback sort key used when <paramref name="sortBy"/> is null or invalid.</param>
    /// <param name="allowedColumns">
    ///   Optional explicit whitelist of property names (case-insensitive).
    ///   When null, every public property of <typeparamref name="T"/> is allowed.
    /// </param>
    public static IQueryable<T> ApplySorting<T, TDefault>(
        this IQueryable<T> query,
        string?            sortBy,
        string?            sortDirection,
        Expression<Func<T, TDefault>> defaultSelector,
        IEnumerable<string>? allowedColumns = null)
    {
        var descending = string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(sortBy))
            return descending
                ? query.OrderByDescending(defaultSelector)
                : query.OrderBy(defaultSelector);

        // Build the whitelist: either the explicitly supplied set or all public properties.
        var whitelist = (allowedColumns?.Select(c => c.ToLowerInvariant()).ToHashSet())
            ?? typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => p.Name.ToLowerInvariant())
                        .ToHashSet();

        var normalised = sortBy.Trim().ToLowerInvariant();

        // Security: reject any column name not in the whitelist.
        if (!whitelist.Contains(normalised))
        {
            // Fall back silently — callers get a predictable result without an error.
            return descending
                ? query.OrderByDescending(defaultSelector)
                : query.OrderBy(defaultSelector);
        }

        // Resolve the actual PropertyInfo using the original (non-lowercased) name.
        var prop = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => string.Equals(p.Name, normalised, StringComparison.OrdinalIgnoreCase));

        if (prop == null)
            return descending
                ? query.OrderByDescending(defaultSelector)
                : query.OrderBy(defaultSelector);

        // Build a dynamic lambda: x => x.<PropertyName>
        var param     = Expression.Parameter(typeof(T), "x");
        var member    = Expression.Property(param, prop);
        var converted = Expression.Convert(member, typeof(object));
        var lambda    = Expression.Lambda<Func<T, object>>(converted, param);

        return descending
            ? query.OrderByDescending(lambda)
            : query.OrderBy(lambda);
    }

    /// <summary>
    /// Convenience overload using <see cref="string"/> as the default key type
    /// (covers the majority of "sort by name" cases).
    /// </summary>
    public static IQueryable<T> ApplySortingByName<T>(
        this IQueryable<T>           query,
        string?                      sortBy,
        string?                      sortDirection,
        Expression<Func<T, string>>  defaultSelector,
        IEnumerable<string>?         allowedColumns = null)
        => query.ApplySorting(sortBy, sortDirection, defaultSelector, allowedColumns);

    /// <summary>
    /// Convenience overload where the default sort key is a <see cref="DateTime"/>.
    /// </summary>
    public static IQueryable<T> ApplySortingByDate<T>(
        this IQueryable<T>               query,
        string?                          sortBy,
        string?                          sortDirection,
        Expression<Func<T, DateTime>>    defaultSelector,
        IEnumerable<string>?             allowedColumns = null)
        => query.ApplySorting(sortBy, sortDirection, defaultSelector, allowedColumns);
}
