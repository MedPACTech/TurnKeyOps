namespace MedInsights.Lib.Dtos
{
    public sealed class CreatePatientReferralActivityDto
    {
        public Guid PatientReferralId { get; set; }
        public Guid PatientId { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public Dictionary<string, string?>? Metadata { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
    }
}
