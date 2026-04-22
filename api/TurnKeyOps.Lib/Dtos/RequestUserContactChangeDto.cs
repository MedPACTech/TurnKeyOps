namespace MedInsights.Lib.Dtos
{
    public sealed class RequestUserContactChangeDto
    {
        public string Channel { get; set; } = string.Empty;
        public string NewContactValue { get; set; } = string.Empty;
    }
}
