namespace MedInsights.Lib.Dtos
{
    public class PatientEncounterNoteRequestDto
    {
        public Guid EncounterId { get; set; }
        public Guid PatientId { get; set; }
        public string Template { get; set; } = string.Empty;
        public string Transcript { get; set; } = string.Empty;
    }
}
