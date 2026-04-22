using System.Text.Json;
using MedInsights.Models;

namespace MedInsights.Services.Interfaces
{
    public interface IChatSummaryService
    {
        Task<JsonDocument> SummarizeAsync(SummaryRequest request, CancellationToken ct);
    }

}