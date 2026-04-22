namespace MedInsights.Lib.Configurations;

public sealed class RepositoryKeySettings
{
    // Global GUID key format for repository keys. Supported values: "N" or "D".
    public string GuidFormat { get; set; } = "N";

    // Enables migration-time fallback reads across GUID key formats.
    public bool EnableLegacyGuidKeyFallbackReads { get; set; } = false;
}
