namespace MedInsights.Lib.Dtos
{
    public sealed class PatientExportRequestDto
    {
        public List<string> Fields { get; set; } = new();
        public string? Search { get; set; }
    }
}
