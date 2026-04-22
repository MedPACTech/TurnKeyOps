using Azure;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.BackgroundServices.Interfaces;

namespace MedInsights.Services.BackgroundServices
{
    public class DictationWorkerService : IDictationWorkerService
    {
        private readonly IDictationRepository _dictationRepository;

        public DictationWorkerService(IDictationRepository dictationRepository)
        {
            _dictationRepository = dictationRepository;
        }

        public async Task<Dictation?> GetAsync(string partitionKey, string rowKey)
        {
            return await _dictationRepository.GetAsync(partitionKey, rowKey);
        }

        public async Task UpdateAsync(Dictation dictation)
        {
            dictation.DateUpdated = DateTime.UtcNow;

            // bypass ETag
            dictation.ETag = ETag.All;

            await _dictationRepository.SaveAsync(dictation);
        }

        public async Task MarkFailedAsync(Dictation dictation, string errorMessage)
        {
            dictation.Status = "Failed";
            dictation.TranscribedText = errorMessage; // or use a dedicated ErrorMessage field
            dictation.DateUpdated = DateTime.UtcNow;
            await _dictationRepository.SaveAsync(dictation);
        }
    }
}

