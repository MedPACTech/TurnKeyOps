namespace MedInsights.Lib.Dtos
{
    public class CreateReferralWorkItemDto
    {
        public Guid? PatientId { get; set; }
        public Guid? EncounterId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Mrn { get; set; } = string.Empty;
        public string ReferralSource { get; set; } = string.Empty;
        public string ReferralChannel { get; set; } = string.Empty;
        public string SourceReceivedAt { get; set; } = string.Empty;
        public string CaseTitle { get; set; } = string.Empty;
        public string CaseSummary { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Assignee { get; set; } = string.Empty;
        public string OwnerRole { get; set; } = string.Empty;
        public string NextAction { get; set; } = string.Empty;
        public string NextActionAt { get; set; } = string.Empty;
        public string LastUpdate { get; set; } = string.Empty;
        public string LastUpdateNote { get; set; } = string.Empty;
        public string Signal { get; set; } = string.Empty;
        public string ReasonInQueue { get; set; } = string.Empty;
        public string QueueLane { get; set; } = string.Empty;
        public string BlockerLabel { get; set; } = string.Empty;
        public string DueLabel { get; set; } = string.Empty;
        public string DueClock { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public List<ReferralPatientDetailDto> PatientDetails { get; set; } = new();
        public string LatestNoteAuthor { get; set; } = string.Empty;
        public List<ReferralTimelineItemDto> Timeline { get; set; } = new();
    }
}
