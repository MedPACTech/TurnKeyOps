// MedInsights.Services.Interfaces/IChatTitleService.cs

namespace MedInsights.Services.Interfaces;

public interface IChatTitleService
{
    Task<string> GenerateTitleAsync(string? previousTitle, string lastUser, string lastAssistant, string? rollingSummary, CancellationToken ct = default);
}