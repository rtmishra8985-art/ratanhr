namespace HRMS.Application.Common;

/// <summary>
/// Centralized pagination bounds validation and normalization.
/// Prevents duplication across 20+ controllers and ensures consistent behavior.
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    /// Normalizes pagination parameters within safe bounds.
    /// </summary>
    /// <param name="page">1-based page number. Values &lt; 1 default to 1.</param>
    /// <param name="pageSize">Items per page. Values &lt; 1 default to 25; &gt; maxPageSize capped at maxPageSize.</param>
    /// <param name="maxPageSize">Maximum allowed page size (default 200). Prevents memory exhaustion attacks.</param>
    /// <returns>Normalized (page, pageSize) tuple, both guaranteed within safe bounds.</returns>
    /// <example>
    /// var (page, pageSize) = PaginationHelper.Normalize(queryPage: -1, queryPageSize: 500);
    /// // Returns: (1, 200)
    /// </example>
    public static (int page, int pageSize) Normalize(
        int page = 1,
        int pageSize = 25,
        int maxPageSize = 200)
    {
        // Ensure page >= 1
        page = page < 1 ? 1 : page;

        // Ensure 1 <= pageSize <= maxPageSize
        pageSize = pageSize < 1 ? 25 : pageSize > maxPageSize ? maxPageSize : pageSize;

        return (page, pageSize);
    }

    /// <summary>
    /// Calculates the number of records to skip for a given page.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <returns>Number of records to skip in LINQ Skip() clause.</returns>
    public static int CalculateSkip(int page, int pageSize) => (page - 1) * pageSize;
}
