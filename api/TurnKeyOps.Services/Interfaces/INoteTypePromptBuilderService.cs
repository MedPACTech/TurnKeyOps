namespace MedInsights.Services.Interfaces
{
    public interface INoteTypePromptBuilderService
    {
        Task<NoteTypePromptProfile> ResolveAsync(string noteTypeSelector, CancellationToken ct = default);
        string BuildSystemPrompt(NoteTypePromptProfile profile);
        NoteTypePromptOutput SplitOutput(string generatedText, NoteTypePromptProfile profile);
    }

    public sealed class NoteTypePromptProfile
    {
        public Guid? NoteTypeId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public bool AlwaysCreateReferral { get; init; }
        public bool ExternalCommunicationEnabled { get; init; }
        public IReadOnlyList<NoteTypePromptSection> Sections { get; init; } = [];
    }

    public sealed class NoteTypePromptSection
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string PromptInstructions { get; init; } = string.Empty;
        public string OutputTarget { get; init; } = string.Empty;
        public string CommunicationMode { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public bool IsRequired { get; init; }
        public bool IsEnabled { get; init; } = true;
    }

    public sealed class NoteTypePromptOutput
    {
        public string ClinicalNote { get; init; } = string.Empty;
        public string BillingRecommendations { get; init; } = string.Empty;
        public string ExternalCommunication { get; init; } = string.Empty;
    }
}
