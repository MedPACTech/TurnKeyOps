using System.Reflection;
using IBeam.Identity.Interfaces;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class UserVerifiedContactService : IUserVerifiedContactService
    {
        private readonly IUserContext _userContext;
        private readonly IIdentityOtpAuthService _otpAuthService;
        private readonly IUserContactChangeRequestRepository _requestRepository;
        private readonly IPlatformUserRepository _platformUserRepository;
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly ITenantMembershipRepository _tenantMembershipRepository;
        private readonly IAuditService _auditService;

        public UserVerifiedContactService(
            IUserContext userContext,
            IIdentityOtpAuthService otpAuthService,
            IUserContactChangeRequestRepository requestRepository,
            IPlatformUserRepository platformUserRepository,
            IUserProfileRepository userProfileRepository,
            ITenantMembershipRepository tenantMembershipRepository,
            IAuditService auditService)
        {
            _userContext = userContext;
            _otpAuthService = otpAuthService;
            _requestRepository = requestRepository;
            _platformUserRepository = platformUserRepository;
            _userProfileRepository = userProfileRepository;
            _tenantMembershipRepository = tenantMembershipRepository;
            _auditService = auditService;
        }

        public async Task<UserContactChangeRequestDto> RequestChangeAsync(RequestUserContactChangeDto dto, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var channel = NormalizeChannel(dto.Channel);
            var newContact = NormalizeContact(channel, dto.NewContactValue);
            if (string.IsNullOrWhiteSpace(newContact))
                throw new ArgumentException("New contact value is required.", nameof(dto));

            var platformUser = await GetPlatformUserAsync(_userContext.UserId, ct);
            var currentContact = GetCurrentContact(platformUser, channel);
            if (string.Equals(NormalizeContact(channel, currentContact), newContact, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The new contact matches the current verified contact.");

            var pending = await _requestRepository.GetLatestPendingAsync(_userContext.UserId, channel, ct);
            if (pending is not null && pending.ExpiresUtc.HasValue && pending.ExpiresUtc.Value > DateTime.UtcNow)
                throw new InvalidOperationException("A contact change request is already pending for this channel.");

            var challenge = await _otpAuthService.StartOtpAsync(newContact, _userContext.TenantId, ct);
            var request = new UserContactChangeRequest
            {
                Id = Guid.NewGuid(),
                UserId = _userContext.UserId,
                TenantId = _userContext.TenantId,
                Channel = channel,
                NewContactValue = dto.NewContactValue.Trim(),
                NormalizedNewContactValue = newContact,
                PreviousContactValue = currentContact,
                ChallengeId = ReadStringProperty(challenge, "ChallengeId", "Id"),
                Status = "pending",
                RequestedUtc = DateTime.UtcNow,
                ExpiresUtc = ReadDateTimeProperty(challenge, "ExpiresAtUtc", "ExpiresAt", "ExpiresUtc"),
                PartitionKey = $"USER={_userContext.UserId:N}",
                RowKey = EntityKeyPolicy.Row(Guid.NewGuid()),
                IsDeleted = false
            };
            request.RowKey = EntityKeyPolicy.Row(request.Id);

            var saved = await _requestRepository.SaveAsync(request, ct);

            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                TenantId = _userContext.TenantId,
                UserId = _userContext.UserId,
                Category = "security",
                Action = "contact_change_requested",
                Severity = "info",
                TargetType = "user_contact_change",
                TargetId = saved.Id.ToString("D"),
                Source = nameof(UserVerifiedContactService),
                Description = $"Requested verified {channel} change."
            }, ct);

            return UserContactChangeRequestMapper.ToDto(saved);
        }

        public async Task<UserProfileDto> VerifyChangeAsync(VerifyUserContactChangeDto dto, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
            if (dto.RequestId == Guid.Empty)
                throw new ArgumentException("RequestId is required.", nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.Code))
                throw new ArgumentException("Code is required.", nameof(dto));

            var request = await _requestRepository.GetAsync($"USER={_userContext.UserId:N}", EntityKeyPolicy.Row(dto.RequestId), ct)
                ?? throw new KeyNotFoundException("Contact change request not found.");

            if (request.UserId != _userContext.UserId)
                throw new UnauthorizedAccessException();
            if (!string.Equals(request.Status, "pending", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Contact change request is not pending.");
            if (request.ExpiresUtc.HasValue && request.ExpiresUtc.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Contact change request has expired.");
            if (string.IsNullOrWhiteSpace(request.ChallengeId))
                throw new InvalidOperationException("Contact change request is missing OTP challenge data.");

            try
            {
                await _otpAuthService.CompleteOtpAsync(
                    request.ChallengeId,
                    request.NormalizedNewContactValue,
                    dto.Code.Trim(),
                    request.Id.ToString("D"),
                    ct);
            }
            catch (Exception ex)
            {
                request.LastError = ex.Message;
                await _requestRepository.SaveAsync(request, ct);
                throw;
            }

            var platformUser = await GetPlatformUserAsync(_userContext.UserId, ct);
            ApplyVerifiedContact(platformUser, request.Channel, request.NewContactValue, request.NormalizedNewContactValue);
            platformUser.DateUpdated = DateTime.UtcNow;
            await _platformUserRepository.SaveAsync(platformUser, ct);

            var memberships = await _tenantMembershipRepository.GetByUserIdAsync(_userContext.UserId, ct);
            UserProfile? currentProfile = null;
            foreach (var membership in memberships)
            {
                var partitionKey = membership.PartitionKey;
                var rowKey = EntityKeyPolicy.Row(_userContext.UserId);
                var profile = await _userProfileRepository.GetAsync(partitionKey, rowKey, ct);
                if (profile is null)
                    continue;

                if (string.Equals(request.Channel, "email", StringComparison.OrdinalIgnoreCase))
                    profile.PrimaryEmail = request.NewContactValue;
                else
                    profile.PrimaryPhone = request.NewContactValue;

                await _userProfileRepository.SaveAsync(profile, ct);

                if (membership.TenantId == _userContext.TenantId)
                    currentProfile = profile;
            }

            request.Status = "verified";
            request.VerifiedUtc = DateTime.UtcNow;
            request.LastError = null;
            await _requestRepository.SaveAsync(request, ct);

            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                TenantId = _userContext.TenantId,
                UserId = _userContext.UserId,
                Category = "security",
                Action = "contact_change_verified",
                Severity = "info",
                TargetType = "user_contact_change",
                TargetId = request.Id.ToString("D"),
                Source = nameof(UserVerifiedContactService),
                Description = $"Verified and applied {request.Channel} contact change."
            }, ct);

            if (currentProfile is not null)
                return UserProfileMapper.ToDto(currentProfile);

            var fallbackProfile = await _userProfileRepository.GetAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(_userContext.UserId), ct)
                ?? throw new KeyNotFoundException("User profile not found.");
            return UserProfileMapper.ToDto(fallbackProfile);
        }

        private async Task<PlatformUser> GetPlatformUserAsync(Guid userId, CancellationToken ct)
        {
            return await _platformUserRepository.GetAsync($"USER={userId:N}", "PROFILE", ct)
                ?? throw new KeyNotFoundException("Platform user not found.");
        }

        private static string NormalizeChannel(string channel)
        {
            var normalized = channel?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "email" => "email",
                "phone" => "phone",
                "sms" => "phone",
                _ => throw new ArgumentException("Channel must be 'email' or 'phone'.", nameof(channel))
            };
        }

        private static string? NormalizeContact(string channel, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase)
                ? value.Trim().ToLowerInvariant()
                : value.Trim();
        }

        private static string? GetCurrentContact(PlatformUser platformUser, string channel)
            => string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase)
                ? platformUser.PrimaryEmail
                : platformUser.PrimaryPhone;

        private static void ApplyVerifiedContact(PlatformUser platformUser, string channel, string contactValue, string normalizedContactValue)
        {
            if (string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase))
            {
                platformUser.PrimaryEmail = contactValue;
                platformUser.NormalizedPrimaryEmail = normalizedContactValue;
                platformUser.EmailVerified = true;
                return;
            }

            platformUser.PrimaryPhone = contactValue;
            platformUser.NormalizedPrimaryPhone = normalizedContactValue;
            platformUser.PhoneVerified = true;
        }

        private static string? ReadStringProperty(object instance, params string[] names)
        {
            foreach (var name in names)
            {
                var value = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(instance);
                if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                    return stringValue;
            }

            return null;
        }

        private static DateTime? ReadDateTimeProperty(object instance, params string[] names)
        {
            foreach (var name in names)
            {
                var value = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(instance);
                if (value is DateTime dateTimeValue)
                    return DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc);
                if (value is DateTimeOffset dateTimeOffsetValue)
                    return dateTimeOffsetValue.UtcDateTime;
            }

            return null;
        }
    }
}
