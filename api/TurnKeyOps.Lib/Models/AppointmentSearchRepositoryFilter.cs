
namespace MedInsights.Lib.Models
{

    /// <summary>
    /// Filter criteria for searching appointments in the repository.
    /// </summary>
    public sealed class AppointmentSearchRepositoryFilter
    {
        public string TenantPartitionKey { get; set; } = default!;

        public string? PatientRowKey { get; set; }  // RepositoryKeyHelper.ToRowKey(patientId)
        public string? ProviderRowKey { get; set; } // RepositoryKeyHelper.ToRowKey(providerId)

        // Half-open [FromUtc, ToExclusiveUtc) window (UTC expected).
        public DateTime? FromUtc { get; set; }
        public DateTime? ToExclusiveUtc { get; set; }

        // Optional: in-repo sort/paging (done in-memory – see notes).
        public string? Sort { get; set; }  // "start" | "created" | "updated"
        public string? Order { get; set; } // "asc" | "desc"
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}