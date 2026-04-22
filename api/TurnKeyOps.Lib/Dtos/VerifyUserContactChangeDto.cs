namespace MedInsights.Lib.Dtos
{
    public sealed class VerifyUserContactChangeDto
    {
        public Guid RequestId { get; set; }
        public string Code { get; set; } = string.Empty;
    }
}
