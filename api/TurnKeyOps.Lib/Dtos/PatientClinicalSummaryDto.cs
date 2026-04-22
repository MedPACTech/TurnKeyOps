namespace MedInsights.Lib.Dtos
{
    public sealed class PatientClinicalSummaryDto
    {
        public Guid PatientId { get; set; }
        public string Narrative { get; set; } = string.Empty;
        public List<string> ActiveConditions { get; set; } = new();
        public string MostRecentConcern { get; set; } = string.Empty;
        public List<string> CareGaps { get; set; } = new();
        public string NextVisit { get; set; } = string.Empty;
        public string ReferralCaseSummary { get; set; } = string.Empty;
        public string ReferralReason { get; set; } = string.Empty;
        public string Limitations { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
