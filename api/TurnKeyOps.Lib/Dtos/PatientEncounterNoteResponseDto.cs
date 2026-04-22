namespace MedInsights.Lib.Dtos
{
    public class PatientEncounterNoteResponseDto
    {
        public Guid EncounterId { get; set; }
        public Guid? PatientId { get; set; }
        public string Template { get; set; } = string.Empty;
        public string NoteText { get; set; } = string.Empty;
        public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
