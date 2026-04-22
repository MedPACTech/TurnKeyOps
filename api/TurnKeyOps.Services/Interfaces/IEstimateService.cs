using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IEstimateService
{
    Task<EstimateDto?> GetAsync(Guid id);
    Task<(IEnumerable<EstimateDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken);
    Task<EstimateDto> AddAsync(EstimateDto dto);
    Task<EstimateDto> UpdateAsync(EstimateDto dto);
    Task<EstimateDto> CreateFromAppointmentAsync(CreateEstimateFromAppointmentRequestDto dto);
    Task<EstimateDto> UpdateStructuredAsync(Guid id, UpdateEstimateStructuredRequestDto dto);
    Task<EstimateCalculationSnapshotDto> CalculateAsync(StructuredEstimateInputDto dto);
    Task<EstimateDto> SubmitAsync(Guid id);
    Task<EstimateDto> StartReviewAsync(Guid id);
    Task<EstimateDto> AwardAsync(Guid id);
    Task<EstimateDto> RejectAsync(Guid id);
    Task<EstimateDto> ReviseAsync(Guid id);
    Task<JobDto> ConvertToJobAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<EstimateDto> CreateFromTemplateAsync(Guid templateId, Guid customerId, Guid? jobId);
    Task<ConcreteCalculatorResult> CalculateConcreteAsync(ConcreteCalculatorRequest request);
    Task<EstimateDto> SignAsync(Guid estimateId, string signatureDataUrl, string signedByName);
}
