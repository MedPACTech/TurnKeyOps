using MedInsights.Lib.Entities;

namespace MedInsights.Services.BackgroundServices.Interfaces;

    public interface IDictationWorkerService
    {
        Task<Dictation?> GetAsync(string partitionKey, string rowKey);
        Task UpdateAsync(Dictation dictation);
        Task MarkFailedAsync(Dictation dictation, string errorMessage);
    }

