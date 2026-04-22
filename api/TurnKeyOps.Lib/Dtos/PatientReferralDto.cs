namespace MedInsights.Lib.Dtos
{
    public class PatientReferralDto
    {
        public Guid Id { get; set; }
        public Guid? EncounterId { get; set; }
        public Guid CaptureDraftNoteId { get; set; }
        public Guid PatientId { get; set; }
        public Guid ProviderId { get; set; }
        public string NoteType { get; set; } = string.Empty;
        public string NoteTitle { get; set; } = string.Empty;
        public string ReferralBody { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
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
        public string Contact { get; set; } = string.Empty;
        public Guid? CreatedByUserId { get; set; }
        public string CreatedByFirstName { get; set; } = string.Empty;
        public string CreatedByLastName { get; set; } = string.Empty;
        public DateTime DateSent { get; set; }
        public Guid SentBy { get; set; }
        public string SentTo { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    }
}
