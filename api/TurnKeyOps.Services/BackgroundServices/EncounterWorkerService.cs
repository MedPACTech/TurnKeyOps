using Azure;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.BackgroundServices.Interfaces;

namespace MedInsights.Services.BackgroundServices
{
    public class EncounterWorkerService : IEncounterWorkerService
    {
        private readonly IPatientEncounterRepository _encounterRepository;

        public EncounterWorkerService(IPatientEncounterRepository patientEncounterRepository)
        {
            _encounterRepository = patientEncounterRepository;
        }

        public async Task<PatientEncounter?> GetAsync(string partitionKey, string rowKey)
        {
            return await _encounterRepository.GetAsync(partitionKey, rowKey);
        }

        public async Task UpdateAsync(PatientEncounter encounter)
        {
            encounter.DateUpdated = DateTime.UtcNow;

            // bypass ETag
            encounter.ETag = ETag.All;

            await _encounterRepository.SaveAsync(encounter);
        }

        public async Task MarkFailedAsync(PatientEncounter encounter, string errorMessage)
        {
            encounter.Status = "Failed";
            encounter.EncounterBody = errorMessage;
            encounter.DateUpdated = DateTime.UtcNow;
            await _encounterRepository.SaveAsync(encounter);
        }
    }
}


