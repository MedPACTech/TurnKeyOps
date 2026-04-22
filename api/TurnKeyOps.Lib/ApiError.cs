namespace MedInsights.Lib
{
    public class ApiError
    {
        public string Code { get; set; } = string.Empty;   // e.g. "RequiredField", "UnexpectedError"
        public string? Field { get; set; }                 // Optional field name
        public string Message { get; set; } = string.Empty;
    }
}