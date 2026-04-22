namespace MedInsights.Lib.Dtos
{
    public sealed class UpdateReferralWorkflowDto
    {
        public string Status { get; set; } = string.Empty;
        public string Assignee { get; set; } = string.Empty;
        public string OwnerRole { get; set; } = string.Empty;
        public string NextAction { get; set; } = string.Empty;
        public string NextActionAt { get; set; } = string.Empty;
    }
}
