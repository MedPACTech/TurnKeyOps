namespace MedInsights.Lib
{
    /// <summary>
    /// Standard paged response wrapper for API responses.
    /// </summary>
    /// <typeparam name="T">The type of items in the response.</typeparam>

    public class ApiPagedResponse<T> : ApiResponse<IEnumerable<T>>
    {
        public int PageSize { get; set; }
        public string? ContinuationToken { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}