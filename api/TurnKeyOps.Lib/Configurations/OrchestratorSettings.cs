using MedInsights.Models;

namespace MedInsights.Lib.Configurations
{
    public sealed class OrchestratorSettings
    {
        public int SummarizeAtPromptTokens { get; set; } = 3500;  // soft trigger
        public int HardCutoffPromptTokens { get; set; } = 7000;    // absolute must-summarize
        public int KeepRecentTurns { get; set; } = 8;              // always keep latest N turns verbatim
        public SummarizeStyle SummaryStyle { get; set; } = SummarizeStyle.Json;
    }

}
