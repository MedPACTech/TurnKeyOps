namespace MedInsights.Lib.Dtos
{
    public class CreatePatientReferralDto
    {
        public Guid? EncounterId { get; set; }
        public Guid CaptureDraftNoteId { get; set; }
        public Guid PatientId { get; set; }
        public Guid ProviderId { get; set; }
        public string NoteType { get; set; } = string.Empty;
        public string NoteTitle { get; set; } = string.Empty;
        public string ReferralBody { get; set; } = string.Empty;
        public DateTime DateSent { get; set; }
        public Guid SentBy { get; set; }
        public string SentTo { get; set; } = string.Empty;
    }
}
