

using MedInsights.Models;

namespace MedInsights.Lib.Configurations
{
    public sealed class SummarizerSettings
    {
        public string Model { get; set; } = "gpt-5-nano";
        public int TargetTokens { get; set; } = 600;
        public int KeepRecentTurns { get; set; } = 8;
        public bool RedactSensitive { get; set; } = true;
        public float Temperature { get; set; } = 0.2f;
        public SummarizeStyle Style { get; set; } = SummarizeStyle.Json;
        public string[]? AdditionalMaskPatterns { get; set; } = Array.Empty<string>();
    }

}
