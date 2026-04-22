using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientEncounterService
    {
        /// <summary>
        /// Add a new encounter (saves blob + table entry + queues transcription).
        /// </summary>
        Task<PatientEncounterDto> AddEncounterAsync(Stream audioStream, Guid? patientId, CancellationToken ct);

        Task<PatientEncounterDto> AddEncounterFromNarrativeAsync(PatientEncounterNarrativeCreateRequestDto dto, CancellationToken ct);

        /// <summary>
        /// Get a single encounter by ID (scoped to current user).
        /// </summary>
        Task<PatientEncounterDto?> GetAsync(Guid id);

        /// <summary>
        /// Get all encounters for the current user (soft-delete aware).
        /// </summary>
        Task<List<PatientEncounterDto>> GetMyEncountersAsync();

        /// <summary>
        /// Update an existing encounter (scoped to current user).
        /// </summary>
        Task<PatientEncounterDto> UpdateAsync(PatientEncounterDto dto);

        /// <summary>
        /// Soft delete an encounter (sets IsDeleted = true).
        /// </summary>
        Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

        Task<PatientEncounterNoteResponseDto> GenerateNoteAsync(PatientEncounterNoteRequestDto dto, CancellationToken ct = default);

        Task<List<PatientEncounterListItemDto>> GetMyEncounterListAsync();

    }
}

