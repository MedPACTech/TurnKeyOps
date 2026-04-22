namespace MedInsights.Lib.Configurations
{
    public sealed class PatientClinicalSummarySettings
    {
        public string Model { get; set; } = "gpt-4o-mini";
        public int MaxOutputTokens { get; set; } = 900;
        public float Temperature { get; set; } = 0.1f;
        public int MaxItemsPerSection { get; set; } = 12;
        public int MaxNarrativeChars { get; set; } = 2000;
        public int MaxLongFieldChars { get; set; } = 1000;
        public string PromptVersion { get; set; } = "v1";
    }
}
