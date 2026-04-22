namespace MedInsights.Lib.Dtos
{
    public sealed class PatientReferralActivityDto
    {
        public Guid Id { get; set; }
        public Guid PatientReferralId { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public Dictionary<string, string?>? Metadata { get; set; }
    }
}
