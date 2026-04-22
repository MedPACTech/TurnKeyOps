namespace MedInsights.Lib.Dtos
{
    public class ActivityLogUpsertDto
    {
        public DateTime EntryDate { get; set; }
        public Guid TenantId { get; set; }
        public Guid? FacilityId { get; set; }
        public Guid UserId { get; set; }
        public List<ActivityLogItemDto> Items { get; set; } = new();
        public string? Narrative { get; set; }
        public Guid EnteredBy { get; set; }
    }
}
