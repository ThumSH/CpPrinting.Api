namespace CpPrinting.Api.DTOs
{
    /// <summary>
    /// Generic wrapper for paginated list endpoints. Used opt-in via ?paginated=true query param.
    /// Existing array-style callers continue to work unchanged when paginated=false (the default).
    /// </summary>
    public class PaginatedResponseDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalPages { get; set; }
    }
}