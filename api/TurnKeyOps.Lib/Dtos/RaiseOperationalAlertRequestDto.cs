namespace MedInsights.Lib.Dtos
{
    public sealed class RaiseOperationalAlertRequestDto
    {
        public Guid? TenantId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = "error";
        public string DedupeKey { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ContextJson { get; set; }
    }
}
