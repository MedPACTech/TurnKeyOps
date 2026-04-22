namespace MedInsights.Lib.Dtos
{
    public class UpdateNoteTypeProfileDto
    {
        public Guid Id { get; set; }
        public Guid NoteTypeId { get; set; }
        public string? RecordType { get; set; }
        public string? PromptInstructions { get; set; }
        public string? SectionSchemaJson { get; set; }
        public bool RequireTelehealthAttestation { get; set; }
        public bool RequirePreventiveReview { get; set; }
        public bool IsSystem { get; set; }
    }
}
