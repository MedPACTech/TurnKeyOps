using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class OperationalAlertService : IOperationalAlertService
    {
        private readonly IOperationalAlertRepository _repository;
        private readonly IUserContext _userContext;
        private readonly ISystemErrorRepository _systemErrorRepository;

        public OperationalAlertService(
            IOperationalAlertRepository repository,
            IUserContext userContext,
            ISystemErrorRepository systemErrorRepository)
        {
            _repository = repository;
            _userContext = userContext;
            _systemErrorRepository = systemErrorRepository;
        }

        public async Task<OperationalAlertDto> RaiseAsync(RaiseOperationalAlertRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.DedupeKey))
                throw new ArgumentException("DedupeKey is required.", nameof(dto));

            var tenantId = dto.TenantId ?? (_userContext.IsAuthenticated ? _userContext.TenantId : null);
            var partitionKey = tenantId.HasValue ? $"TENANT={tenantId.Value:N}" : "GLOBAL";
            var existing = await _repository.GetByDedupeKeyAsync(partitionKey, dto.DedupeKey.Trim(), ct);
            var now = DateTime.UtcNow;

            if (existing is null)
            {
                existing = new OperationalAlert
                {
                    Id = Guid.NewGuid(),
                    PartitionKey = partitionKey,
                    RowKey = RepositoryKeyHelper.ToOrderedRowKey(Guid.NewGuid()),
                    TenantId = tenantId,
                    AlertType = dto.AlertType.Trim(),
                    Severity = string.IsNullOrWhiteSpace(dto.Severity) ? "error" : dto.Severity.Trim().ToLowerInvariant(),
                    Status = "open",
                    DedupeKey = dto.DedupeKey.Trim(),
                    Source = dto.Source.Trim(),
                    Message = dto.Message.Trim(),
                    ContextJson = Normalize(dto.ContextJson),
                    RepeatCount = 1,
                    FirstOccurredUtc = now,
                    LastOccurredUtc = now,
                    IsDeleted = false
                };
            }
            else
            {
                existing.Severity = string.IsNullOrWhiteSpace(dto.Severity) ? existing.Severity : dto.Severity.Trim().ToLowerInvariant();
                existing.Source = dto.Source.Trim();
                existing.Message = dto.Message.Trim();
                existing.ContextJson = Normalize(dto.ContextJson);
                existing.Status = "open";
                existing.RepeatCount += 1;
                existing.LastOccurredUtc = now;
                existing.ResolvedUtc = null;
            }

            var saved = await _repository.SaveAsync(existing, ct);

            await _systemErrorRepository.SaveAsync(new SystemError
            {
                PartitionKey = now.ToString("yyyyMMdd"),
                RowKey = Guid.NewGuid().ToString("N"),
                Path = saved.Source,
                Method = saved.AlertType,
                Message = saved.Message,
                StackTrace = saved.ContextJson ?? string.Empty,
                TraceId = saved.DedupeKey,
                Timestamp = DateTimeOffset.UtcNow
            });

            return OperationalAlertMapper.ToDto(saved);
        }

        public async Task<IReadOnlyList<OperationalAlertDto>> GetRecentAsync(string? status = null, int take = 100, CancellationToken ct = default)
        {
            var tenantId = _userContext.IsAuthenticated ? _userContext.TenantId : (Guid?)null;
            var entities = await _repository.GetByTenantAsync(tenantId, status, Math.Clamp(take, 1, 500), ct);
            return entities.Select(OperationalAlertMapper.ToDto).ToList();
        }

        public async Task<OperationalAlertDto> AcknowledgeAsync(Guid id, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var entity = await _repository.GetByTenantAsync(_userContext.TenantId, null, 500, ct);
            var alert = entity.FirstOrDefault(x => x.Id == id)
                ?? throw new KeyNotFoundException("Operational alert not found.");

            alert.Status = "acknowledged";
            alert.AcknowledgedUtc = DateTime.UtcNow;
            alert.AcknowledgedByUserId = _userContext.UserId;

            var saved = await _repository.SaveAsync(alert, ct);
            return OperationalAlertMapper.ToDto(saved);
        }

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
