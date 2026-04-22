namespace MedInsights.Lib.Dtos
{
    public sealed class RecordAuditEventRequestDto
    {
        public Guid? TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? Source { get; set; }
        public string? Description { get; set; }
        public string? MetadataJson { get; set; }
    }
}
