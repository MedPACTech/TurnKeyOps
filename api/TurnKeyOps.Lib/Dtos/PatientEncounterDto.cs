namespace MedInsights.Lib.Dtos
{
    public class PatientEncounterDto
    {
        public Guid Id { get; set; }
        public string? PatientId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string EncounterBody { get; set; } = string.Empty;
        public Guid CaptureDraftNoteId { get; set; }
        public Guid ProviderId { get; set; }
        public string NoteType { get; set; } = string.Empty;
        public string NoteTitle { get; set; } = string.Empty;
        public string Data { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
