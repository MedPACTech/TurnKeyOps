namespace MedInsights.Lib.Dtos
{
    public class PatientEncounterNarrativeCreateRequestDto
    {
        public Guid PatientId { get; set; }
        public string NarrativeText { get; set; } = string.Empty;
        public string? Template { get; set; }
    }
}
