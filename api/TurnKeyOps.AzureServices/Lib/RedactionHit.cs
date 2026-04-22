
namespace MedInsights.AzureServices.Lib
{
    public sealed class RedactionHit
    {
        public string SpeakerId { get; init; } = "";
        public string Placeholder { get; init; } = "";
        public long OffsetTicks { get; init; }      // SDK uses 100-ns ticks
        public long DurationTicks { get; init; }
        public string MaskedToken { get; init; } = ""; // what was seen in text (e.g., "*****" or "N***")
    }
}