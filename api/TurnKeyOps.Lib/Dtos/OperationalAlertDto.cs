namespace MedInsights.Lib.Dtos
{
    public sealed class OperationalAlertDto
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DedupeKey { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ContextJson { get; set; }
        public int RepeatCount { get; set; }
        public DateTime FirstOccurredUtc { get; set; }
        public DateTime LastOccurredUtc { get; set; }
        public DateTime? AcknowledgedUtc { get; set; }
        public Guid? AcknowledgedByUserId { get; set; }
        public DateTime? ResolvedUtc { get; set; }
    }
}
