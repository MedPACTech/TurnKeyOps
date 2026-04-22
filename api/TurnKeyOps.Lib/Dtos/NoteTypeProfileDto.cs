namespace MedInsights.Lib.Dtos
{
    public class NoteTypeProfileDto
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public Guid NoteTypeId { get; set; }
        public string RecordType { get; set; } = string.Empty;
        public string? PromptInstructions { get; set; }
        public string? SectionSchemaJson { get; set; }
        public bool RequireTelehealthAttestation { get; set; }
        public bool RequirePreventiveReview { get; set; }
        public bool IsSystem { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
