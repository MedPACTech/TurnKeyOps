using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class NoteTypeService : INoteTypeService
    {
        private readonly INoteTypeRepository _repository;
        private readonly INoteTypeProfileRepository _profileRepository;
        private readonly IUserContext _userContext;

        public NoteTypeService(
            INoteTypeRepository repository,
            INoteTypeProfileRepository profileRepository,
            IUserContext userContext)
        {
            _repository = repository;
            _profileRepository = profileRepository;
            _userContext = userContext;
        }

        public async Task<IReadOnlyList<NoteTypeDto>> GetAllAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var systemDefinitions = await _repository.GetSystemDefinitionsAsync(ct);
            var tenantDefinitions = await _repository.GetTenantCustomDefinitionsAsync(_userContext.TenantId, ct);
            var tenantOverrides = await _repository.GetTenantSystemOverridesAsync(_userContext.TenantId, ct);

            var overrideBySystemId = tenantOverrides
                .Where(x => x.SystemNoteTypeId.HasValue)
                .ToDictionary(x => x.SystemNoteTypeId!.Value, x => x);

            var result = new List<NoteTypeDto>(systemDefinitions.Count + tenantDefinitions.Count);

            foreach (var systemDefinition in systemDefinitions)
            {
                overrideBySystemId.TryGetValue(systemDefinition.Id, out var tenantOverride);
                result.Add(NoteTypeMapper.ToDto(
                    BuildEffectiveSystemDefinition(systemDefinition, tenantOverride),
                    tenantOverride?.IsEnabled ?? systemDefinition.IsEnabled,
                    tenantOverride?.IsDefault ?? systemDefinition.IsDefault ?? false));
            }

            result.AddRange(tenantDefinitions.Select(x => NoteTypeMapper.ToDto(x)));

            return result
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<NoteTypeDto> CreateAsync(CreateNoteTypeDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var name = RequireName(dto.Name);
            var code = NormalizeCode(dto.Code, name);
            await EnsureCodeIsUniqueAsync(code, excludeId: null, ct);

            var now = DateTime.UtcNow;
            var entity = new NoteType
            {
                Id = Guid.NewGuid(),
                PartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                TenantId = _userContext.TenantId,
                RecordType = NoteTypeRepository.DefinitionRecordType,
                Name = name,
                Code = code,
                NormalizedCode = code,
                Description = NormalizeOptional(dto.Description),
                HasParentNote = dto.HasParentNote,
                IsSystem = false,
                IsEnabled = dto.IsEnabled,
                IsDefault = dto.IsDefault,
                SortOrder = dto.SortOrder,
                CreatedBy = _userContext.UserId.ToString(),
                DateCreated = now,
                UpdatedBy = _userContext.UserId.ToString(),
                DateUpdated = now
            };

            entity.RowKey = EntityKeyPolicy.Row(entity.Id);

            await _repository.SaveAsync(entity);
            await EnsureTenantProfileExistsAsync(entity, ct);
            await EnsureSingleDefaultAsync(dto.IsDefault ? entity.Id : null, ct);
            return await GetNoteTypeDtoByIdAsync(entity.Id, ct);
        }

        public async Task<NoteTypeDto> UpdateAsync(Guid id, UpdateNoteTypeDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var name = RequireName(dto.Name);
            var code = NormalizeCode(dto.Code, name);
            await EnsureCodeIsUniqueAsync(code, id, ct);

            var tenantCustom = await _repository.GetTenantCustomDefinitionAsync(_userContext.TenantId, id, ct);
            if (tenantCustom is not null)
            {
                tenantCustom.Name = name;
                tenantCustom.Code = code;
                tenantCustom.NormalizedCode = code;
                tenantCustom.Description = NormalizeOptional(dto.Description);
                tenantCustom.HasParentNote = dto.HasParentNote;
                tenantCustom.IsEnabled = dto.IsEnabled;
                if (dto.IsDefault.HasValue)
                    tenantCustom.IsDefault = dto.IsDefault.Value;
                tenantCustom.SortOrder = dto.SortOrder;
                tenantCustom.UpdatedBy = _userContext.UserId.ToString();
                tenantCustom.DateUpdated = DateTime.UtcNow;

                await _repository.SaveAsync(tenantCustom);
                await EnsureSingleDefaultAsync(dto.IsDefault == true ? tenantCustom.Id : null, ct);
                return await GetNoteTypeDtoByIdAsync(tenantCustom.Id, ct);
            }

            var systemDefinition = await _repository.GetSystemDefinitionAsync(id, ct)
                ?? throw new KeyNotFoundException("Note type not found.");

            var tenantOverride = await _repository.GetTenantSystemOverrideAsync(_userContext.TenantId, id, ct)
                ?? CreateSystemOverride(systemDefinition);

            tenantOverride.Name = name;
            tenantOverride.Code = code;
            tenantOverride.NormalizedCode = code;
            tenantOverride.Description = NormalizeOptional(dto.Description);
            tenantOverride.HasParentNote = dto.HasParentNote;
            tenantOverride.IsEnabled = dto.IsEnabled;
            if (dto.IsDefault.HasValue)
                tenantOverride.IsDefault = dto.IsDefault.Value;
            tenantOverride.SortOrder = dto.SortOrder;
            tenantOverride.UpdatedBy = _userContext.UserId.ToString();
            tenantOverride.DateUpdated = DateTime.UtcNow;

            await _repository.SaveAsync(tenantOverride);
            await EnsureSingleDefaultAsync(dto.IsDefault == true ? systemDefinition.Id : null, ct);
            return await GetNoteTypeDtoByIdAsync(systemDefinition.Id, ct);
        }

        public async Task<NoteTypeDto> UpdateStatusAsync(Guid id, UpdateNoteTypeStatusDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var customDefinition = await _repository.GetTenantCustomDefinitionAsync(_userContext.TenantId, id, ct);
            if (customDefinition is not null)
            {
                customDefinition.IsEnabled = dto.IsEnabled;
                if (dto.IsDefault.HasValue)
                    customDefinition.IsDefault = dto.IsDefault.Value;
                customDefinition.UpdatedBy = _userContext.UserId.ToString();
                customDefinition.DateUpdated = DateTime.UtcNow;

                await _repository.SaveAsync(customDefinition);
                await EnsureSingleDefaultAsync(dto.IsDefault == true ? customDefinition.Id : null, ct);
                return await GetNoteTypeDtoByIdAsync(customDefinition.Id, ct);
            }

            var systemDefinition = await _repository.GetSystemDefinitionAsync(id, ct)
                ?? throw new KeyNotFoundException("Note type not found.");

            var tenantOverride = await _repository.GetTenantSystemOverrideAsync(_userContext.TenantId, id, ct);
            if (tenantOverride is null)
            {
                tenantOverride = new NoteType
                {
                    Id = Guid.NewGuid(),
                    PartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                    RowKey = $"SYSTEMSTATUS|{id:D}",
                    TenantId = _userContext.TenantId,
                    SystemNoteTypeId = id,
                    RecordType = NoteTypeRepository.SystemOverrideRecordType,
                    Name = systemDefinition.Name,
                    Code = systemDefinition.Code,
                    NormalizedCode = systemDefinition.NormalizedCode,
                    Description = systemDefinition.Description,
                    HasParentNote = systemDefinition.HasParentNote,
                    IsSystem = true,
                    IsEnabled = systemDefinition.IsEnabled,
                    SortOrder = systemDefinition.SortOrder,
                    CreatedBy = _userContext.UserId.ToString(),
                    DateCreated = DateTime.UtcNow
                };
            }

            tenantOverride.IsEnabled = dto.IsEnabled;
            if (dto.IsDefault.HasValue)
                tenantOverride.IsDefault = dto.IsDefault.Value;
            tenantOverride.UpdatedBy = _userContext.UserId.ToString();
            tenantOverride.DateUpdated = DateTime.UtcNow;

            await _repository.SaveAsync(tenantOverride);
            await EnsureSingleDefaultAsync(dto.IsDefault == true ? systemDefinition.Id : null, ct);
            return await GetNoteTypeDtoByIdAsync(systemDefinition.Id, ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = await _repository.GetTenantCustomDefinitionAsync(_userContext.TenantId, id, ct)
                ?? throw new KeyNotFoundException("Note type not found.");

            entity.IsDeleted = true;
            entity.IsEnabled = false;
            entity.IsDefault = false;
            entity.UpdatedBy = _userContext.UserId.ToString();
            entity.DateUpdated = DateTime.UtcNow;

            await _repository.SaveAsync(entity);
            await DeleteTenantProfileAsync(entity.Id, ct);
            await EnsureSingleDefaultAsync(null, ct);
        }

        private async Task EnsureSingleDefaultAsync(Guid? preferredId, CancellationToken ct)
        {
            var systemDefinitions = await _repository.GetSystemDefinitionsAsync(ct);
            var tenantDefinitions = await _repository.GetTenantCustomDefinitionsAsync(_userContext.TenantId, ct);
            var tenantOverrides = await _repository.GetTenantSystemOverridesAsync(_userContext.TenantId, ct);

            var states = BuildDefaultStates(systemDefinitions, tenantDefinitions, tenantOverrides);
            var orderedEnabledStates = states
                .Where(x => x.EffectiveIsEnabled)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var targetId = preferredId.HasValue && orderedEnabledStates.Any(x => x.Id == preferredId.Value)
                ? preferredId
                : orderedEnabledStates.Where(x => x.EffectiveIsDefault).Select(x => (Guid?)x.Id).FirstOrDefault()
                    ?? orderedEnabledStates.Select(x => (Guid?)x.Id).FirstOrDefault();

            foreach (var tenantDefinition in tenantDefinitions)
            {
                var shouldBeDefault = targetId.HasValue && tenantDefinition.Id == targetId.Value;
                if ((tenantDefinition.IsDefault ?? false) == shouldBeDefault)
                    continue;

                tenantDefinition.IsDefault = shouldBeDefault;
                tenantDefinition.UpdatedBy = _userContext.UserId.ToString();
                tenantDefinition.DateUpdated = DateTime.UtcNow;
                await _repository.SaveAsync(tenantDefinition);
            }

            foreach (var systemDefinition in systemDefinitions)
            {
                var tenantOverride = tenantOverrides.FirstOrDefault(x => x.SystemNoteTypeId == systemDefinition.Id);
                var shouldBeDefault = targetId.HasValue && systemDefinition.Id == targetId.Value;
                var baselineDefault = systemDefinition.IsDefault ?? false;
                bool? desiredOverrideValue = shouldBeDefault == baselineDefault ? null : shouldBeDefault;

                if (tenantOverride is null)
                {
                    if (!desiredOverrideValue.HasValue)
                        continue;

                    tenantOverride = CreateSystemOverride(systemDefinition);
                    tenantOverride.IsDefault = desiredOverrideValue.Value;
                    tenantOverride.UpdatedBy = _userContext.UserId.ToString();
                    tenantOverride.DateUpdated = DateTime.UtcNow;
                    await _repository.SaveAsync(tenantOverride);
                    continue;
                }

                if (tenantOverride.IsDefault == desiredOverrideValue)
                    continue;

                tenantOverride.IsDefault = desiredOverrideValue;
                tenantOverride.UpdatedBy = _userContext.UserId.ToString();
                tenantOverride.DateUpdated = DateTime.UtcNow;
                await _repository.SaveAsync(tenantOverride);
            }
        }

        private async Task EnsureTenantProfileExistsAsync(NoteType entity, CancellationToken ct)
        {
            var existing = await _profileRepository.GetTenantProfileByNoteTypeIdAsync(_userContext.TenantId, entity.Id, ct);
            if (existing is not null)
                return;

            var now = DateTime.UtcNow;
            await _profileRepository.SaveAsync(new NoteTypeProfile
            {
                Id = entity.Id,
                PartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                RowKey = EntityKeyPolicy.Row(entity.Id),
                TenantId = _userContext.TenantId,
                NoteTypeId = entity.Id,
                RecordType = NoteTypeProfileRepository.ProfileRecordType,
                IsSystem = false,
                IsDeleted = false,
                CreatedBy = _userContext.UserId.ToString(),
                DateCreated = now,
                UpdatedBy = _userContext.UserId.ToString(),
                DateUpdated = now
            }, ct);
        }

        private async Task DeleteTenantProfileAsync(Guid noteTypeId, CancellationToken ct)
        {
            var profile = await _profileRepository.GetTenantProfileByNoteTypeIdAsync(_userContext.TenantId, noteTypeId, ct);
            if (profile is null)
                return;

            profile.IsDeleted = true;
            profile.UpdatedBy = _userContext.UserId.ToString();
            profile.DateUpdated = DateTime.UtcNow;
            await _profileRepository.SaveAsync(profile, ct);
        }


        private async Task<NoteTypeDto> GetNoteTypeDtoByIdAsync(Guid id, CancellationToken ct)
        {
            var noteType = (await GetAllAsync(ct)).FirstOrDefault(x => x.Id == id)
                ?? throw new KeyNotFoundException("Note type not found.");

            return noteType;
        }

        private List<NoteTypeState> BuildDefaultStates(
            IReadOnlyList<NoteType> systemDefinitions,
            IReadOnlyList<NoteType> tenantDefinitions,
            IReadOnlyList<NoteType> tenantOverrides)
        {
            var states = new List<NoteTypeState>(systemDefinitions.Count + tenantDefinitions.Count);

            foreach (var systemDefinition in systemDefinitions)
            {
                var tenantOverride = tenantOverrides.FirstOrDefault(x => x.SystemNoteTypeId == systemDefinition.Id);
                states.Add(new NoteTypeState(
                    systemDefinition.Id,
                    tenantOverride?.Name ?? systemDefinition.Name,
                    tenantOverride?.SortOrder ?? systemDefinition.SortOrder,
                    tenantOverride?.IsEnabled ?? systemDefinition.IsEnabled,
                    tenantOverride?.IsDefault ?? systemDefinition.IsDefault ?? false));
            }

            states.AddRange(tenantDefinitions.Select(x => new NoteTypeState(
                x.Id,
                x.Name,
                x.SortOrder,
                x.IsEnabled,
                x.IsDefault ?? false)));

            return states;
        }

        private static NoteType BuildEffectiveSystemDefinition(NoteType systemDefinition, NoteType? tenantOverride)
        {
            if (tenantOverride is null)
                return systemDefinition;

            return new NoteType
            {
                Id = systemDefinition.Id,
                PartitionKey = systemDefinition.PartitionKey,
                RowKey = systemDefinition.RowKey,
                TenantId = systemDefinition.TenantId,
                SystemNoteTypeId = systemDefinition.SystemNoteTypeId,
                RecordType = systemDefinition.RecordType,
                Name = tenantOverride.Name,
                Code = tenantOverride.Code,
                NormalizedCode = tenantOverride.NormalizedCode,
                Description = tenantOverride.Description,
                HasParentNote = tenantOverride.HasParentNote,
                IsSystem = systemDefinition.IsSystem,
                IsEnabled = tenantOverride.IsEnabled,
                IsDefault = tenantOverride.IsDefault,
                SortOrder = tenantOverride.SortOrder,
                CreatedBy = systemDefinition.CreatedBy,
                DateCreated = systemDefinition.DateCreated,
                UpdatedBy = tenantOverride.UpdatedBy ?? systemDefinition.UpdatedBy,
                DateUpdated = tenantOverride.DateUpdated ?? systemDefinition.DateUpdated,
                IsDeleted = systemDefinition.IsDeleted,
                ETag = systemDefinition.ETag,
                Timestamp = systemDefinition.Timestamp
            };
        }

        private NoteType CreateSystemOverride(NoteType systemDefinition)
        {
            return new NoteType
            {
                Id = Guid.NewGuid(),
                PartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                RowKey = $"SYSTEMSTATUS|{systemDefinition.Id:D}",
                TenantId = _userContext.TenantId,
                SystemNoteTypeId = systemDefinition.Id,
                RecordType = NoteTypeRepository.SystemOverrideRecordType,
                Name = systemDefinition.Name,
                Code = systemDefinition.Code,
                NormalizedCode = systemDefinition.NormalizedCode,
                Description = systemDefinition.Description,
                HasParentNote = systemDefinition.HasParentNote,
                IsSystem = true,
                IsEnabled = systemDefinition.IsEnabled,
                IsDefault = null,
                SortOrder = systemDefinition.SortOrder,
                CreatedBy = _userContext.UserId.ToString(),
                DateCreated = DateTime.UtcNow
            };
        }

        private async Task EnsureCodeIsUniqueAsync(string normalizedCode, Guid? excludeId, CancellationToken ct)
        {
            var allNoteTypes = await GetAllAsync(ct);
            var duplicate = allNoteTypes.Any(x =>
                x.Id != excludeId &&
                string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
                throw new InvalidOperationException($"Note type code '{normalizedCode}' already exists.");
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static string RequireName(string? value)
        {
            var normalized = NormalizeOptional(value);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Name is required.");

            return normalized;
        }

        private static string NormalizeCode(string? value, string fallbackName)
        {
            var raw = string.IsNullOrWhiteSpace(value) ? fallbackName : value;
            var chars = raw.Trim().ToUpperInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            var normalized = string.Join(
                "_",
                new string(chars)
                    .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Code is required.");

            return normalized;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private sealed record NoteTypeState(Guid Id, string Name, int SortOrder, bool EffectiveIsEnabled, bool EffectiveIsDefault);
    }
}
