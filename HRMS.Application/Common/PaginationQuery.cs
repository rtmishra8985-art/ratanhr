namespace HRMS.Application.Common
{
    /// <summary>
    /// Base pagination parameters used across all paginated list endpoints.
    /// </summary>
    public class PaginationQuery
    {
        private int _page = 1;
        private int _pageSize = 25;

        /// <summary>1-based page number. Defaults to 1.</summary>
        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        /// <summary>Number of items per page. Clamped to 1–100. Defaults to 25.</summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 1 : value > 100 ? 100 : value;
        }

        /// <summary>Optional free-text search term.</summary>
        public string? Search { get; set; }

        /// <summary>Column/property name to sort by (case-insensitive).</summary>
        public string? SortBy { get; set; }

        /// <summary>Sort direction: "asc" or "desc". Defaults to "asc".</summary>
        public string? SortDirection { get; set; } = "asc";
    }
}
