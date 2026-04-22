using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IUserContext _userContext;

        public UserProfileService(IUserContext userContext, IUserProfileRepository userProfileRepository)
        {
            _userContext = userContext;
            _userProfileRepository = userProfileRepository;
        }

        // Get a single user
        public async Task<UserProfileDto?> GetCurrentAsync(CancellationToken ct = default)
        {
            return await GetAsync(_userContext.UserId, ct);
        }

        // Get a single user
        public async Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken ct = default)
        {
            
            var pk = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var userProfile = await _userProfileRepository.GetAsync(pk, EntityKeyPolicy.Row(userId), ct);
            if (userProfile == null) return null;

            return UserProfileMapper.ToDto(userProfile);
        }

        public async Task<UserProfileDto> EnsureProfileExistsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            var pk = EntityKeyPolicy.TenantPartition(tenantId);
            var rk = EntityKeyPolicy.Row(userId);

            var existing = await _userProfileRepository.GetAsync(pk, rk, ct);
            if (existing is not null)
            {
                return UserProfileMapper.ToDto(existing);
            }

            var profile = new UserProfile
            {
                Id = userId,
                PartitionKey = pk,
                RowKey = rk,
                ApplicationUserId = userId,
                FirstName = string.Empty,
                LastName = string.Empty,
                IsActive = true,
                IsDeleted = false
            };

            var saved = await _userProfileRepository.SaveAsync(profile, ct);
            return UserProfileMapper.ToDto(saved);
        }

        // Get all users
        public async Task<IEnumerable<UserProfileDto>> GetAllAsync(CancellationToken ct = default)
        {
            var pk = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var page = await _userProfileRepository.GetByPartitionPagedAsync(pk, 100, ct: CancellationToken.None);

            var results = page.Results.Select(UserProfileMapper.ToDto);

            return results;
        }

        // Update an existing user
        public async Task<UserProfileDto> UpdateAsync(UserProfileDto dto, CancellationToken ct = default)
        {
            var pk = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var existingEntity = await _userProfileRepository.GetAsync(pk, EntityKeyPolicy.Row(dto.Id))
                                ?? throw new KeyNotFoundException("User not found.");

            if (!string.Equals(Normalize(dto.PrimaryEmail), Normalize(existingEntity.PrimaryEmail), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Primary email must be changed through the verified contact change flow.");
            if (!string.Equals(Normalize(dto.PrimaryPhone), Normalize(existingEntity.PrimaryPhone), StringComparison.Ordinal))
                throw new InvalidOperationException("Primary phone must be changed through the verified contact change flow.");

            var entity = UserProfileMapper.ToEntity(dto, pk, existingEntity.RowKey);

            // 💥 PRESERVE server-managed fields
            entity.ApplicationUserId = existingEntity.ApplicationUserId;

            // 💥 Preserve concurrency + timestamp fields
            entity.ETag = existingEntity.ETag;
            entity.Timestamp = existingEntity.Timestamp;

            var saved = await _userProfileRepository.SaveAsync(entity, ct);

            return UserProfileMapper.ToDto(saved);
        }

        public async Task CreateUserProfileAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            var pk = EntityKeyPolicy.TenantPartition(tenantId);
            var rk = EntityKeyPolicy.Row(userId);

            var existing = await _userProfileRepository.GetAsync(pk, rk, ct);
            if (existing is not null)
            {
                return;
            }

            var profile = new UserProfile
            {
                Id = userId,
                PartitionKey = pk,
                RowKey = rk,
                ApplicationUserId = userId,
                FirstName = string.Empty,
                LastName = string.Empty,
                IsActive = true,
                IsDeleted = false
            };

            await _userProfileRepository.SaveAsync(profile, ct);
        }


        // Delete a user
        // TODO: Remove this function
        public async Task DeleteAsync(UserProfileDto dto, CancellationToken ct = default)
        {

            var pk = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var user = await _userProfileRepository.GetAsync(pk, EntityKeyPolicy.Row(dto.Id), ct)
                        ?? throw new KeyNotFoundException("User not found.");

            user.IsDeleted = true;
            user.IsActive = false;
            await _userProfileRepository.SaveAsync(user, ct);
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

