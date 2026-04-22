namespace MedInsights.Lib.Dtos
{
    public sealed class PatientReferralQueueItemDto
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientMrn { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Assignee { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;
        public string OwnerRole { get; set; } = string.Empty;
        public string NextAction { get; set; } = string.Empty;
        public DateTime? NextActionAt { get; set; }
        public DateTime? DueAt { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string ReferralSource { get; set; } = string.Empty;
        public string SourceApp { get; set; } = string.Empty;
        public string ReferralChannel { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string CaseTitle { get; set; } = string.Empty;
        public string CaseSummary { get; set; } = string.Empty;
        public string ReferralReason { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
