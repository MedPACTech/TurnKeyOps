using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class JobService : IJobService
{
    private readonly IJobRepository _repo;
    private readonly IEstimateWorkflowPayloadStore _payloadStore;
    private readonly IInvoiceService _invoiceService;
    private readonly IUserContext _userContext;

    public JobService(
        IJobRepository repo,
        IEstimateWorkflowPayloadStore payloadStore,
        IInvoiceService invoiceService,
        IUserContext userContext)
    {
        _repo = repo;
        _payloadStore = payloadStore;
        _invoiceService = invoiceService;
        _userContext = userContext;
    }

    private string PartitionKeyForTenant() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<JobDto?> GetAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        var dto = JobMapper.ToDto(entity);
        await HydrateJobArtifactsAsync(entity, dto);
        return dto;
    }

    public async Task<(IEnumerable<JobDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken)
    {
        var pk = PartitionKeyForTenant();
        var offset = int.TryParse(continuationToken, out var parsed) ? parsed : 0;
        var all = (await _repo.GetAllAsync(false, false))
            .Where(x => x.PartitionKey == pk && !x.IsDeleted)
            .OrderByDescending(x => x.DateUpdated)
            .ToList();
        var items = all.Skip(offset).Take(pageSize).ToList();
        var token = offset + items.Count < all.Count ? (offset + items.Count).ToString() : null;
        return (items.Where(x => !x.IsDeleted).Select(JobMapper.ToDto), token);
    }

    public async Task<IEnumerable<JobDto>> GetActiveAsync()
    {
        var pk = PartitionKeyForTenant();
        var all = await _repo.GetAllAsync(false, false);
        return all
            .Where(j => j.PartitionKey == pk && !j.IsDeleted && j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled && j.Status != JobStatus.Closed && j.Status != JobStatus.Paid)
            .OrderByDescending(j => j.DateUpdated)
            .Select(JobMapper.ToDto);
    }

    public async Task<JobDto> AddAsync(JobDto dto)
    {
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        await EnsureReleaseEligibleAsync(dto);
        var entity = JobMapper.ToEntity(dto, PartitionKeyForTenant());
        await PersistJobArtifactsAsync(entity, dto);
        await _repo.SaveAsync(entity);
        return await GetAsync(dto.Id) ?? JobMapper.ToDto(entity);
    }

    public async Task<JobDto> UpdateAsync(JobDto dto)
    {
        var existing = await _repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException("Job not found", nameof(dto.Id));
        if (!IsReleased(existing.Status) && IsReleased(dto.Status))
        {
            await EnsureReleaseEligibleAsync(dto);
        }
        var entity = JobMapper.ToEntity(dto, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await PersistJobArtifactsAsync(entity, dto);
        await _repo.SaveAsync(entity);
        return await GetAsync(dto.Id) ?? JobMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null) return;
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }

    private async Task PersistJobArtifactsAsync(Job entity, JobDto dto)
    {
        entity.EstimateSnapshotBlobName = await _payloadStore.SaveJobEstimateSnapshotAsync(_userContext.TenantId, dto.Id, dto.EstimateSnapshot);
        entity.EstimateSnapshotJson = null;
    }

    private async Task HydrateJobArtifactsAsync(Job entity, JobDto dto)
    {
        dto.EstimateSnapshot = await _payloadStore.LoadJobEstimateSnapshotAsync(entity.EstimateSnapshotBlobName, entity.EstimateSnapshotJson);
    }

    private async Task EnsureReleaseEligibleAsync(JobDto dto)
    {
        if (!IsReleased(dto.Status)) return;
        if (!dto.InvoiceId.HasValue || dto.InvoiceId.Value == Guid.Empty)
            throw new ArgumentException("A qualifying invoice is required before a job can be released.", nameof(dto.InvoiceId));
        var release = await _invoiceService.GetJobReleaseAsync(dto.InvoiceId.Value);
        if (!release.IsEligible)
            throw new ArgumentException(release.Reason, nameof(dto.InvoiceId));
    }

    private static bool IsReleased(JobStatus status) => status is JobStatus.Scheduled or JobStatus.InProgress;
}
