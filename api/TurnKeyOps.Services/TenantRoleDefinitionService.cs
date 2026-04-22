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
    public sealed class TenantRoleDefinitionService : ITenantRoleDefinitionService
    {
        private readonly ITenantRoleDefinitionRepository _roleRepository;
        private readonly IRolePermissionMappingRepository _mappingRepository;
        private readonly IRolePermissionCatalog _catalog;
        private readonly IUserContext _userContext;
        private readonly ITenantMembershipAuthorizationService _membershipAuthorizationService;

        public TenantRoleDefinitionService(
            ITenantRoleDefinitionRepository roleRepository,
            IRolePermissionMappingRepository mappingRepository,
            IRolePermissionCatalog catalog,
            IUserContext userContext,
            ITenantMembershipAuthorizationService membershipAuthorizationService)
        {
            _roleRepository = roleRepository;
            _mappingRepository = mappingRepository;
            _catalog = catalog;
            _userContext = userContext;
            _membershipAuthorizationService = membershipAuthorizationService;
        }

        public async Task<IReadOnlyList<TenantRoleDto>> GetAllAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var roles = (await _roleRepository.GetSystemRolesAsync(ct))
                .Concat(await _roleRepository.GetTenantRolesAsync(_userContext.TenantId, ct))
                .ToList();

            var mappings = (await _mappingRepository.GetSystemMappingsAsync(ct))
                .Concat(await _mappingRepository.GetTenantMappingsAsync(_userContext.TenantId, ct))
                .ToList();

            var permissionCatalog = BuildPermissionCatalog();

            return roles
                .OrderBy(x => x.IsSystem ? 0 : 1)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(role => TenantRoleDefinitionMapper.ToDto(
                    role,
                    mappings.Where(x => x.RoleId == role.Id)
                        .Select(mapping => ResolvePermission(permissionCatalog, mapping.PermissionKey, mapping.PermissionId))))
                .ToList();
        }

        public async Task<IReadOnlyList<TenantRoleDto>> GetAssignableAsync(CancellationToken ct = default)
            => (await GetAllAsync(ct)).Where(x => x.IsAssignable).ToList();

        public Task<IReadOnlyList<PermissionDefinitionDto>> GetPermissionCatalogAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return Task.FromResult<IReadOnlyList<PermissionDefinitionDto>>(BuildPermissionCatalog()
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        public async Task<TenantRoleDto> CreateAsync(UpsertTenantRoleRequestDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var key = NormalizeRoleKey(dto.Key);
            if (await _roleRepository.GetSystemByKeyAsync(key, ct) is not null
                || await _roleRepository.GetTenantByKeyAsync(_userContext.TenantId, key, ct) is not null)
            {
                throw new InvalidOperationException("Role key already exists.");
            }

            var now = DateTime.UtcNow;
            var entity = new TenantRoleDefinition
            {
                Id = dto.Id.GetValueOrDefault(Guid.NewGuid()),
                TenantId = _userContext.TenantId,
                PartitionKey = TenantPartition(_userContext.TenantId),
                RowKey = EntityKeyPolicy.Row(dto.Id.GetValueOrDefault(Guid.NewGuid())),
                Key = key,
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty,
                IsSystem = false,
                IsAssignable = dto.IsAssignable,
                GrantsOwnership = false,
                GrantsBillingAdmin = false,
                DateCreated = now,
                DateUpdated = now
            };
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);

            await _roleRepository.SaveAsync(entity, ct);
            await SaveMappingsAsync(entity, dto.PermissionKeys, dto.PermissionIds, ct);
            return await GetByIdAsync(entity.Id, ct);
        }

        public async Task<TenantRoleDto> UpdateAsync(Guid id, UpsertTenantRoleRequestDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var entity = await _roleRepository.GetAsync(TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(id), ct)
                ?? throw new KeyNotFoundException("Role not found.");

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description?.Trim() ?? string.Empty;
            entity.IsAssignable = dto.IsAssignable;
            entity.DateUpdated = DateTime.UtcNow;

            await _roleRepository.SaveAsync(entity, ct);
            await SaveMappingsAsync(entity, dto.PermissionKeys, dto.PermissionIds, ct);
            return await GetByIdAsync(entity.Id, ct);
        }

        public async Task<TenantRoleDto> UpdatePermissionsAsync(Guid id, UpdateRolePermissionsRequestDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var entity = await _roleRepository.GetAsync(TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(id), ct)
                ?? throw new KeyNotFoundException("Role not found.");

            await SaveMappingsAsync(entity, dto.PermissionKeys, dto.PermissionIds, ct);
            entity.DateUpdated = DateTime.UtcNow;
            await _roleRepository.SaveAsync(entity, ct);
            return await GetByIdAsync(entity.Id, ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var entity = await _roleRepository.GetAsync(TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(id), ct)
                ?? throw new KeyNotFoundException("Role not found.");

            entity.IsDeleted = true;
            entity.DateUpdated = DateTime.UtcNow;
            await _roleRepository.SaveAsync(entity, ct);

            var mappings = await _mappingRepository.GetMappingsForRoleAsync(_userContext.TenantId, id, ct);
            foreach (var mapping in mappings)
            {
                mapping.IsDeleted = true;
                mapping.DateUpdated = DateTime.UtcNow;
                await _mappingRepository.SaveAsync(mapping, ct);
            }
        }

        private async Task<TenantRoleDto> GetByIdAsync(Guid id, CancellationToken ct)
            => (await GetAllAsync(ct)).First(x => x.Id == id);

        private async Task SaveMappingsAsync(TenantRoleDefinition role, IEnumerable<string> permissionKeys, IEnumerable<Guid> permissionIds, CancellationToken ct)
        {
            var catalog = BuildPermissionCatalog();
            var requestedKeys = permissionKeys.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var permissionId in permissionIds)
            {
                var match = catalog.FirstOrDefault(x => x.Id == permissionId);
                if (match is not null)
                    requestedKeys.Add(match.Key);
            }

            if (requestedKeys.Count == 0)
                throw new ArgumentException("At least one permission is required.");

            var invalid = requestedKeys.Where(key => catalog.All(x => !string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))).ToList();
            if (invalid.Count > 0)
                throw new ArgumentException($"Unsupported permission keys: {string.Join(", ", invalid)}");

            var existing = await _mappingRepository.GetMappingsForRoleAsync(role.TenantId, role.Id, ct);
            var now = DateTime.UtcNow;

            foreach (var mapping in existing)
            {
                var shouldKeep = requestedKeys.Contains(mapping.PermissionKey);
                mapping.IsDeleted = !shouldKeep;
                mapping.DateUpdated = now;
                await _mappingRepository.SaveAsync(mapping, ct);
            }

            var existingKeys = existing.Where(x => !x.IsDeleted)
                .Select(x => x.PermissionKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var key in requestedKeys.Except(existingKeys, StringComparer.OrdinalIgnoreCase))
            {
                var permission = catalog.First(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
                await _mappingRepository.SaveAsync(new RolePermissionMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = role.TenantId,
                    PartitionKey = MappingPartition(role.TenantId),
                    RowKey = $"ROLE={role.Id:N}|PERM={permission.Id:N}",
                    RoleId = role.Id,
                    RoleKey = role.Key,
                    PermissionId = permission.Id,
                    PermissionKey = permission.Key,
                    DateCreated = now,
                    DateUpdated = now
                }, ct);
            }
        }

        private IReadOnlyList<PermissionDefinitionDto> BuildPermissionCatalog()
            => _catalog.GetPermissions()
                .Select(option => new PermissionDefinitionDto
                {
                    Id = option.Id ?? CreateDeterministicGuid(option.Key),
                    Key = option.Key,
                    Name = string.IsNullOrWhiteSpace(option.Name) ? option.Key : option.Name!,
                    Description = option.Description ?? string.Empty
                })
                .ToList();

        private static PermissionDefinitionDto ResolvePermission(
            IReadOnlyList<PermissionDefinitionDto> catalog,
            string key,
            Guid? id)
            => catalog.FirstOrDefault(x =>
                   (!string.IsNullOrWhiteSpace(key) && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
                   || (id.HasValue && x.Id == id.Value))
               ?? new PermissionDefinitionDto
               {
                   Id = id ?? CreateDeterministicGuid(key),
                   Key = key,
                   Name = key,
                   Description = string.Empty
               };

        private static string NormalizeRoleKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Role key is required.", nameof(key));

            return key.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        }

        private static string TenantPartition(Guid tenantId) => $"ROLEDEF|TENANT={tenantId:N}";
        private static string MappingPartition(Guid? tenantId) => tenantId.HasValue ? $"ROLEPERM|TENANT={tenantId.Value:N}" : RolePermissionMappingRepository.SystemPartitionKey;

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static Guid CreateDeterministicGuid(string value)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
            return new Guid(hash);
        }
    }
}
