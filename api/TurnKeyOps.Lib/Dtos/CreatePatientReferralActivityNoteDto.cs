namespace MedInsights.Lib.Dtos
{
    public sealed class CreatePatientReferralActivityNoteDto
    {
        public string Note { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
    }
}
