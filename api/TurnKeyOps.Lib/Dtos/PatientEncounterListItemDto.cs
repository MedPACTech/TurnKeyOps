namespace MedInsights.Lib.Dtos
{
    public class PatientEncounterListItemDto
    {
        public Guid Id { get; set; }
        public string? PatientId { get; set; }
        public string? PatientFirstName { get; set; }
        public string? PatientLastName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? Status { get; set; }
    }
}
