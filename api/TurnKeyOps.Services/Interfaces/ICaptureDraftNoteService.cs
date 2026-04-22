using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ICaptureDraftNoteService
    {
        Task<CaptureDraftNoteDto> AddAsync(CaptureDraftNoteDto dto);

        Task<CaptureDraftNoteDto?> GetAsync(Guid id);

        Task<List<CaptureDraftNoteDto>> GetMineAsync();
        Task<List<CaptureDraftNoteDto>> GetRecentAsync(int take = 10, CancellationToken ct = default);

        /// <summary>
        /// Optional convenience: filter drafts for a patient (current user scope).
        /// </summary>
        Task<List<CaptureDraftNoteDto>> GetMineByPatientAsync(Guid patientId);

        Task<CaptureDraftNoteDto> UpdateAsync(CaptureDraftNoteDto dto);

        Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

        Task<CaptureDraftNoteDto> GenerateNoteAsync(CaptureDraftNoteDto dto, CancellationToken ct = default);

        Task<PatientReferralDto> CreateReferralAsync(Guid captureDraftNoteId, CancellationToken ct = default);

        /// <summary>
        /// Creates a signed encounter and marks the capture draft note as Completed.
        /// </summary>
        Task<CaptureDraftNoteDto> SignAsync(Guid captureDraftNoteId, CancellationToken ct = default);
    }
}
