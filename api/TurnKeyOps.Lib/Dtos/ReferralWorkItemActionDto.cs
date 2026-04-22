namespace MedInsights.Lib.Dtos
{
    public sealed class ReferralWorkItemActionDto
    {
        public string Action { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string? PerformedBy { get; set; }
    }
}
