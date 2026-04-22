namespace MedInsights.Lib.Dtos
{
    public sealed class AuditEventDto
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? Source { get; set; }
        public string? Description { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime OccurredUtc { get; set; }
    }
}
