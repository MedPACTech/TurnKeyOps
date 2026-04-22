using MedInsights.Lib.Entities;

namespace MedInsights.Services.BackgroundServices.Interfaces;

    public interface IEncounterWorkerService
    {
        Task<PatientEncounter?> GetAsync(string partitionKey, string rowKey);
        Task UpdateAsync(PatientEncounter encounter);
        Task MarkFailedAsync(PatientEncounter encounter, string errorMessage);
    }

