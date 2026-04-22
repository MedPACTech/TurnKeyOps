namespace MedInsights.Lib.Dtos
{
    public class ActivityLogDto
    {
        public DateTime EntryDate { get; set; }
        public Guid TenantId { get; set; }
        public Guid? FacilityId { get; set; }
        public Guid UserId { get; set; }
        public Guid LogId { get; set; }
        public List<ActivityLogItemDto> Items { get; set; } = new();
        public string? Narrative { get; set; }
        public DateTime EnteredAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid EnteredBy { get; set; }
    }
}
