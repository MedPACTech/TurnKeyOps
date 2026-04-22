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
    public sealed class NoteTypeProfileService : INoteTypeProfileService
    {
        private readonly INoteTypeProfileRepository _repository;
        private readonly INoteTypeRepository _noteTypeRepository;
        private readonly IUserContext _userContext;

        public NoteTypeProfileService(
            INoteTypeProfileRepository repository,
            INoteTypeRepository noteTypeRepository,
            IUserContext userContext)
        {
            _repository = repository;
            _noteTypeRepository = noteTypeRepository;
            _userContext = userContext;
        }

        public async Task<IReadOnlyList<NoteTypeProfileDto>> GetAllAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var systemProfiles = await _repository.GetSystemProfilesAsync(ct);
            var tenantProfiles = await _repository.GetTenantProfilesAsync(_userContext.TenantId, ct);
            var tenantByNoteTypeId = tenantProfiles.ToDictionary(x => x.NoteTypeId, x => x);

            var result = new List<NoteTypeProfileDto>(tenantProfiles.Count + systemProfiles.Count);
            result.AddRange(tenantProfiles.Select(NoteTypeProfileMapper.ToDto));
            result.AddRange(systemProfiles
                .Where(x => !tenantByNoteTypeId.ContainsKey(x.NoteTypeId))
                .Select(NoteTypeProfileMapper.ToDto));

            return result
                .OrderBy(x => x.IsSystem)
                .ThenBy(x => x.NoteTypeId)
                .ToList();
        }

        public async Task<NoteTypeProfileDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var tenantProfile = await _repository.GetTenantProfileAsync(_userContext.TenantId, id, ct);
            if (tenantProfile is not null)
                return NoteTypeProfileMapper.ToDto(tenantProfile);

            var systemProfile = await _repository.GetSystemProfileAsync(id, ct);
            return systemProfile is null ? null : NoteTypeProfileMapper.ToDto(systemProfile);
        }

        public async Task<NoteTypeProfileDto?> GetByNoteTypeIdAsync(Guid noteTypeId, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var tenantProfile = await _repository.GetTenantProfileByNoteTypeIdAsync(_userContext.TenantId, noteTypeId, ct);
            if (tenantProfile is not null)
                return NoteTypeProfileMapper.ToDto(tenantProfile);

            var systemProfile = await _repository.GetSystemProfileByNoteTypeIdAsync(noteTypeId, ct);
            return systemProfile is null ? null : NoteTypeProfileMapper.ToDto(systemProfile);
        }

        public async Task<NoteTypeProfileDto> CreateAsync(CreateNoteTypeProfileDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            Validate(dto.NoteTypeId, dto.SectionSchemaJson);
            await EnsureNoteTypeExistsAsync(dto.NoteTypeId, dto.IsSystem, ct);

            var existing = dto.IsSystem
                ? await _repository.GetSystemProfileByNoteTypeIdAsync(dto.NoteTypeId, ct)
                : await _repository.GetTenantProfileByNoteTypeIdAsync(_userContext.TenantId, dto.NoteTypeId, ct);

            if (existing is not null)
            {
                existing.Id = dto.NoteTypeId;
                existing.NoteTypeId = dto.NoteTypeId;
                existing.PartitionKey = dto.IsSystem
                    ? NoteTypeProfileRepository.SystemPartitionKey
                    : EntityKeyPolicy.TenantPartition(_userContext.TenantId);
                existing.RowKey = EntityKeyPolicy.Row(existing.Id);
                existing.TenantId = dto.IsSystem ? null : _userContext.TenantId;
                existing.RecordType = NormalizeRecordType(dto.RecordType);
                existing.PromptInstructions = NormalizeOptional(dto.PromptInstructions);
                existing.SectionSchemaJson = NormalizeJson(dto.SectionSchemaJson);
                existing.RequireTelehealthAttestation = dto.RequireTelehealthAttestation;
                existing.RequirePreventiveReview = dto.RequirePreventiveReview;
                existing.IsSystem = dto.IsSystem;
                existing.IsDeleted = false;
                existing.UpdatedBy = _userContext.UserId.ToString();
                existing.DateUpdated = DateTime.UtcNow;

                await _repository.SaveAsync(existing, ct);
                return NoteTypeProfileMapper.ToDto(existing);
            }

            var now = DateTime.UtcNow;
            var entity = new NoteTypeProfile
            {
                Id = dto.NoteTypeId,
                PartitionKey = dto.IsSystem
                    ? NoteTypeProfileRepository.SystemPartitionKey
                    : EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                RowKey = EntityKeyPolicy.Row(dto.NoteTypeId),
                TenantId = dto.IsSystem ? null : _userContext.TenantId,
                NoteTypeId = dto.NoteTypeId,
                RecordType = NormalizeRecordType(dto.RecordType),
                PromptInstructions = NormalizeOptional(dto.PromptInstructions),
                SectionSchemaJson = NormalizeJson(dto.SectionSchemaJson),
                RequireTelehealthAttestation = dto.RequireTelehealthAttestation,
                RequirePreventiveReview = dto.RequirePreventiveReview,
                IsSystem = dto.IsSystem,
                IsDeleted = false,
                CreatedBy = _userContext.UserId.ToString(),
                DateCreated = now,
                UpdatedBy = _userContext.UserId.ToString(),
                DateUpdated = now
            };

            await _repository.SaveAsync(entity, ct);
            return NoteTypeProfileMapper.ToDto(entity);
        }

        public async Task<NoteTypeProfileDto> UpdateAsync(Guid id, UpdateNoteTypeProfileDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            Validate(dto.NoteTypeId, dto.SectionSchemaJson);

            var entity = await GetAccessibleEntityAsync(id, ct)
                ?? throw new KeyNotFoundException("Note type profile not found.");

            // Some clients address the profile by note type id rather than a durable profile id.
            // Keep the stored identity aligned with the note type id so create-then-update flows
            // do not trip uniqueness checks or overwrite the wrong row.
            var previousId = entity.Id;
            var effectiveIsSystem = entity.IsSystem;

            await EnsureProfileIsUniqueAsync(dto.NoteTypeId, effectiveIsSystem, previousId, ct);

            entity.NoteTypeId = dto.NoteTypeId;
            entity.Id = dto.NoteTypeId;
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.RecordType = NormalizeRecordType(dto.RecordType);
            entity.PromptInstructions = NormalizeOptional(dto.PromptInstructions);
            entity.SectionSchemaJson = NormalizeJson(dto.SectionSchemaJson);
            entity.RequireTelehealthAttestation = dto.RequireTelehealthAttestation;
            entity.RequirePreventiveReview = dto.RequirePreventiveReview;
            entity.IsSystem = effectiveIsSystem;
            entity.UpdatedBy = _userContext.UserId.ToString();
            entity.DateUpdated = DateTime.UtcNow;

            await _repository.SaveAsync(entity, ct);
            return NoteTypeProfileMapper.ToDto(entity);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = await GetAccessibleEntityAsync(id, ct)
                ?? throw new KeyNotFoundException("Note type profile not found.");

            var tenantNoteType = await _noteTypeRepository.GetTenantCustomDefinitionAsync(_userContext.TenantId, entity.NoteTypeId, ct);
            if (tenantNoteType is not null)
            {
                tenantNoteType.IsDeleted = true;
                tenantNoteType.IsEnabled = false;
                tenantNoteType.IsDefault = false;
                tenantNoteType.UpdatedBy = _userContext.UserId.ToString();
                tenantNoteType.DateUpdated = DateTime.UtcNow;
                await _noteTypeRepository.SaveAsync(tenantNoteType, ct);
            }

            entity.IsDeleted = true;
            entity.UpdatedBy = _userContext.UserId.ToString();
            entity.DateUpdated = DateTime.UtcNow;

            await _repository.SaveAsync(entity, ct);
        }

        private async Task<NoteTypeProfile?> GetAccessibleEntityAsync(Guid id, CancellationToken ct)
        {
            var tenantProfile = await _repository.GetTenantProfileAsync(_userContext.TenantId, id, ct);
            if (tenantProfile is not null)
                return tenantProfile;

            var systemProfile = await _repository.GetSystemProfileAsync(id, ct);
            if (systemProfile is not null)
                return systemProfile;

            // Some callers send the note type id in the route instead of the profile id.
            var tenantProfileByNoteTypeId = await _repository.GetTenantProfileByNoteTypeIdAsync(_userContext.TenantId, id, ct);
            if (tenantProfileByNoteTypeId is not null)
                return tenantProfileByNoteTypeId;

            return await _repository.GetSystemProfileByNoteTypeIdAsync(id, ct);
        }

        private async Task EnsureProfileIsUniqueAsync(Guid noteTypeId, bool isSystem, Guid? excludeId, CancellationToken ct)
        {
            var existing = isSystem
                ? await _repository.GetSystemProfileByNoteTypeIdAsync(noteTypeId, ct)
                : await _repository.GetTenantProfileByNoteTypeIdAsync(_userContext.TenantId, noteTypeId, ct);

            if (existing is not null && existing.Id != excludeId)
                throw new InvalidOperationException($"A note type profile already exists for note type '{noteTypeId}'.");
        }

        private async Task EnsureNoteTypeExistsAsync(Guid noteTypeId, bool isSystemProfile, CancellationToken ct)
        {
            if (isSystemProfile)
            {
                var systemType = await _noteTypeRepository.GetSystemDefinitionAsync(noteTypeId, ct);
                if (systemType is null)
                    throw new KeyNotFoundException("Note type not found for profile.");

                return;
            }

            var tenantType = await _noteTypeRepository.GetTenantCustomDefinitionAsync(_userContext.TenantId, noteTypeId, ct);
            if (tenantType is not null)
                return;

            var systemDefinition = await _noteTypeRepository.GetSystemDefinitionAsync(noteTypeId, ct);
            if (systemDefinition is not null)
                return;

            throw new KeyNotFoundException("Note type not found for profile.");
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static void Validate(Guid noteTypeId, string? sectionSchemaJson)
        {
            if (noteTypeId == Guid.Empty)
                throw new ArgumentException("Note type id is required.");

            if (sectionSchemaJson is null)
                return;

            try
            {
                _ = System.Text.Json.JsonDocument.Parse(sectionSchemaJson);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new ArgumentException("SectionSchemaJson must be valid JSON.");
            }
        }

        private static string NormalizeRecordType(string? value)
        {
            // RecordType is an internal storage discriminator for NoteTypeProfile rows.
            // Reads are intentionally scoped to "Profile", so always canonicalize writes
            // to prevent client payload drift from creating unreadable rows.
            return NoteTypeProfileRepository.ProfileRecordType;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeJson(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
