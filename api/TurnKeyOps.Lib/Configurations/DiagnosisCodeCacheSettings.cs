namespace MedInsights.Lib.Configurations
{
    public sealed class DiagnosisCodeCacheSettings
    {
        public bool WarmOnStartup { get; set; } = true;
        public bool SkipWarmupInDevelopment { get; set; } = true;
    }
}
