using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class AppointmentTypeService : IAppointmentTypeService
    {
        private const int MaxNameLength = 120;
        private const int MaxDataLength = 32000;

        private readonly IAppointmentTypeRepository _repository;
        private readonly IUserContext _userContext;
        private readonly ITenantMembershipAuthorizationService _membershipAuthorizationService;

        public AppointmentTypeService(
            IAppointmentTypeRepository repository,
            IUserContext userContext,
            ITenantMembershipAuthorizationService membershipAuthorizationService)
        {
            _repository = repository;
            _userContext = userContext;
            _membershipAuthorizationService = membershipAuthorizationService;
        }

        public async Task<IReadOnlyList<AppointmentTypeDto>> GetAllAsync(bool includeInactive = true, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entities = await _repository.GetByTenantAsync(_userContext.TenantId, ct);
            var filtered = includeInactive
                ? entities
                : entities.Where(x => x.IsActive).ToList();

            return filtered
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(AppointmentTypeMapper.ToDto)
                .ToList();
        }

        public async Task<AppointmentTypeDto> CreateAsync(CreateAppointmentTypeDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var name = NormalizeRequiredName(dto.Name);
            ValidateLocation(dto.Location);
            ValidateAverageTime(dto.AverageTimeInMinutes);
            var data = NormalizeData(dto.Data);

            var existing = await _repository.GetByTenantAsync(_userContext.TenantId, ct);
            if (!dto.IsActive && !existing.Any(x => x.IsActive))
                throw new InvalidOperationException("At least one active appointment type is required.");

            await EnsureUniqueNameAsync(name, excludeId: null, ct);

            var now = DateTime.UtcNow;
            var entity = new AppointmentTypeDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = _userContext.TenantId,
                PartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                Name = name,
                Location = dto.Location,
                IsActive = dto.IsActive,
                AverageTimeInMinutes = dto.AverageTimeInMinutes,
                Data = data,
                CreatedBy = _userContext.UserId.ToString(),
                DateCreated = now,
                UpdatedBy = _userContext.UserId.ToString(),
                DateUpdated = now
            };

            entity.RowKey = EntityKeyPolicy.Row(entity.Id);

            var saved = await _repository.SaveAsync(entity, ct);
            return AppointmentTypeMapper.ToDto(saved);
        }

        public async Task<AppointmentTypeDto> UpdateAsync(Guid id, UpdateAppointmentTypeDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var entity = await _repository.GetByIdAsync(_userContext.TenantId, id, ct)
                ?? throw new KeyNotFoundException("Appointment type not found.");
            var activeCount = await GetActiveCountAsync(ct);

            var name = NormalizeRequiredName(dto.Name);
            ValidateLocation(dto.Location);
            ValidateAverageTime(dto.AverageTimeInMinutes);
            var data = NormalizeData(dto.Data);

            await EnsureUniqueNameAsync(name, excludeId: entity.Id, ct);

            if (!dto.IsActive && entity.IsActive && activeCount <= 1)
                throw new InvalidOperationException("At least one active appointment type is required.");

            entity.Name = name;
            entity.Location = dto.Location;
            entity.IsActive = dto.IsActive;
            entity.AverageTimeInMinutes = dto.AverageTimeInMinutes;
            entity.Data = data;
            entity.UpdatedBy = _userContext.UserId.ToString();
            entity.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity, ct);
            return AppointmentTypeMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var entity = await _repository.GetByIdAsync(_userContext.TenantId, id, ct)
                ?? throw new KeyNotFoundException("Appointment type not found.");
            var activeCount = await GetActiveCountAsync(ct);

            if (entity.IsActive && activeCount <= 1)
                throw new InvalidOperationException("At least one active appointment type is required.");

            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.UpdatedBy = _userContext.UserId.ToString();
            entity.DateUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(entity, ct);
        }

        private async Task<int> GetActiveCountAsync(CancellationToken ct)
            => (await _repository.GetByTenantAsync(_userContext.TenantId, ct)).Count(x => x.IsActive);

        private async Task EnsureUniqueNameAsync(string name, Guid? excludeId, CancellationToken ct)
        {
            var duplicate = (await _repository.GetByTenantAsync(_userContext.TenantId, ct))
                .Any(x => x.Id != excludeId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
                throw new InvalidOperationException($"Appointment type '{name}' already exists.");
        }

        private static string NormalizeRequiredName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Name is required.");

            var normalized = value.Trim();
            if (normalized.Length > MaxNameLength)
                throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.");

            return normalized;
        }

        private static string? NormalizeData(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            if (normalized.Length > MaxDataLength)
                throw new ArgumentException($"Data must be {MaxDataLength} characters or fewer.");

            return normalized;
        }

        private static void ValidateLocation(AppointmentTypeLocation location)
        {
            if (!Enum.IsDefined(typeof(AppointmentTypeLocation), location))
                throw new ArgumentException("Location must be one of Remote, Home, or Facility.");
        }

        private static void ValidateAverageTime(int averageTimeInMinutes)
        {
            if (averageTimeInMinutes <= 0 || averageTimeInMinutes > 1440)
                throw new ArgumentException("AverageTimeInMinutes must be between 1 and 1440.");
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }
    }
}
