using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IDictation
    {

        Task<AudioCaptureDto> StartCaptureAsync(string? patientId, CancellationToken ct);    

        /// <summary>
        /// Add a new capture (saves blob + table entry + queues transcription).
        /// </summary>
        Task<AudioCaptureDto> AddOrAttachCaptureAsync(Stream audioStream, Guid? existingCaptureId, CancellationToken ct);

        /// <summary>
        /// Get a single capture by ID (scoped to current user).
        /// </summary>
        Task<AudioCaptureDto?> GetAsync(Guid id);

        /// <summary>
        /// Get all captures for the current user (soft-delete aware).
        /// </summary>
        Task<List<AudioCaptureDto>> GetMyCapturesAsync();

        /// <summary>
        /// Update an existing capture (scoped to current user).
        /// </summary>
        Task<AudioCaptureDto> UpdateAsync(AudioCaptureDto dto);

        /// <summary>
        /// Soft delete a capture (sets IsDeleted = true).
        /// </summary>
        Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
