namespace MedInsights.Lib.Dtos
{
    public class DiagnosisCodeDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string LongDescription { get; set; } = string.Empty;
    }
}
