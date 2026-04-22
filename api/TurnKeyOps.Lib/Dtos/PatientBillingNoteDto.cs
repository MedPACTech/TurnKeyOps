namespace MedInsights.Lib.Dtos
{
    public class PatientBillingNoteDto
    {
        public Guid Id { get; set; }
        public Guid? EncounterId { get; set; }
        public Guid CaptureDraftNoteId { get; set; }
        public Guid PatientId { get; set; }
        public Guid ProviderId { get; set; }
        public string NoteType { get; set; } = string.Empty;
        public string NoteTitle { get; set; } = string.Empty;
        public string BillingBody { get; set; } = string.Empty;
        public DateTime DateSigned { get; set; }
        public Guid SignedBy { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    }
}
