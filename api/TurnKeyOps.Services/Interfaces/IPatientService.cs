
using MedInsights.Lib.Dtos;
using System.IO;

namespace MedInsights.Services.Interfaces;

public interface IPatientService
{
    Task<PatientDto> AddAsync(PatientDto patientDto);
    Task<BulkPatientUploadResultDto> BulkUploadAsync(Stream csvStream, CancellationToken ct = default);
    Task<PatientDto?> GetAsync(Guid Id);
    Task<(IEnumerable<PatientDto> Patients, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken = null);
    Task DeleteAsync(Guid Id);
    Task<PatientDto> UpdateAsync(PatientDto patient);
    Task<List<PatientDto>> SearchAsync(string terms);
    Task<Dictionary<Guid, PatientDto>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<List<PatientDto>> GetHistoricalPatientsAsync(int scope);
    Task<PatientDto?> GetActiveAsync();
    Task<PatientDto> ActivateAsync(Guid patientId);
    Task<(byte[] Content, string FileName)> ExportAsync(PatientExportRequestDto request, CancellationToken ct = default);
}
