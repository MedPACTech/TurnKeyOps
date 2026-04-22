using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class AuditService : IAuditService
    {
        private readonly IAuditEventRepository _repository;
        private readonly IUserContext _userContext;

        public AuditService(IAuditEventRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        public async Task<AuditEventDto> RecordAsync(RecordAuditEventRequestDto dto, CancellationToken ct = default)
        {
            var occurredUtc = DateTime.UtcNow;
            var tenantId = dto.TenantId ?? (_userContext.IsAuthenticated ? _userContext.TenantId : null);
            var userId = dto.UserId ?? (_userContext.IsAuthenticated ? _userContext.UserId : null);

            var entity = new AuditEvent
            {
                Id = Guid.NewGuid(),
                PartitionKey = tenantId.HasValue ? $"TENANT={tenantId.Value:N}" : "GLOBAL",
                RowKey = RepositoryKeyHelper.ToOrderedRowKey(Guid.NewGuid()),
                TenantId = tenantId,
                UserId = userId,
                Category = dto.Category.Trim(),
                Action = dto.Action.Trim(),
                Severity = string.IsNullOrWhiteSpace(dto.Severity) ? "info" : dto.Severity.Trim().ToLowerInvariant(),
                TargetType = Normalize(dto.TargetType),
                TargetId = Normalize(dto.TargetId),
                Source = Normalize(dto.Source),
                Description = Normalize(dto.Description),
                MetadataJson = Normalize(dto.MetadataJson),
                OccurredUtc = occurredUtc,
                IsDeleted = false
            };

            var saved = await _repository.SaveAsync(entity, ct);
            return AuditEventMapper.ToDto(saved);
        }

        public async Task<IReadOnlyList<AuditEventDto>> GetRecentAsync(int take = 100, CancellationToken ct = default)
        {
            var tenantId = _userContext.IsAuthenticated ? _userContext.TenantId : (Guid?)null;
            var entities = await _repository.GetByTenantAsync(tenantId, Math.Clamp(take, 1, 500), ct);
            return entities.Select(AuditEventMapper.ToDto).ToList();
        }

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
