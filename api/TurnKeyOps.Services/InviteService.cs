using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.Lib.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MedInsights.Services
{
    public sealed class InviteService : IInviteService
    {
        private readonly IInviteRepository _inviteRepository;
        private readonly ITenantMembershipRepository _membershipRepository;
        private readonly IPlatformUserRepository _platformUserRepository;
        private readonly ITenantProfileRepository _tenantProfileRepository;
        private readonly ITenantSeatEntitlementService _seatEntitlementService;
        private readonly ITenantOnboardingPolicyService _tenantOnboardingPolicyService;
        private readonly IUserContext _userContext;
        private readonly IAuditService _auditService;
        private readonly IMemoryCache _memoryCache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IRoleDirectoryService _roleDirectoryService;
        private readonly IRoleAccessService _roleAccess;
        private const int RedeemAttemptLimit = 5;
        private static readonly TimeSpan RedeemAttemptWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan RedeemBlockDuration = TimeSpan.FromMinutes(30);

        public InviteService(
            IInviteRepository inviteRepository,
            ITenantMembershipRepository membershipRepository,
            IPlatformUserRepository platformUserRepository,
            ITenantProfileRepository tenantProfileRepository,
            ITenantSeatEntitlementService seatEntitlementService,
            ITenantOnboardingPolicyService tenantOnboardingPolicyService,
            IUserContext userContext,
            IAuditService auditService,
            IMemoryCache memoryCache,
            IHttpContextAccessor httpContextAccessor,
            IRoleDirectoryService roleDirectoryService,
            IRoleAccessService roleAccess)
        {
            _inviteRepository = inviteRepository;
            _membershipRepository = membershipRepository;
            _platformUserRepository = platformUserRepository;
            _tenantProfileRepository = tenantProfileRepository;
            _seatEntitlementService = seatEntitlementService;
            _tenantOnboardingPolicyService = tenantOnboardingPolicyService;
            _userContext = userContext;
            _auditService = auditService;
            _memoryCache = memoryCache;
            _httpContextAccessor = httpContextAccessor;
            _roleDirectoryService = roleDirectoryService;
            _roleAccess = roleAccess;
        }

        public async Task<IEnumerable<InviteDto>> GetAllAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
            var page = await _inviteRepository.GetByPartitionPagedAsync(PartitionKey(), 200, ct: ct);
            return page.Results.Select(InviteMapper.ToDto);
        }

        public async Task<InviteDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
            var entity = await _inviteRepository.GetAsync(PartitionKey(), EntityKeyPolicy.Row(id), ct);
            return entity is null ? null : InviteMapper.ToDto(entity);
        }

        public async Task<InviteAcceptanceContextDto> GetAcceptanceContextAsync(Guid id, string inviteToken, CancellationToken ct = default)
        {
            Invite? invite = null;

            try
            {
                if (string.IsNullOrWhiteSpace(inviteToken))
                    throw new ArgumentException("Invite token is required.", nameof(inviteToken));

                invite = await _inviteRepository.GetByIdAsync(id, ct)
                    ?? throw new KeyNotFoundException("Invite not found.");

                if (!VerifyInviteToken(invite.InviteTokenHash, inviteToken))
                    throw new UnauthorizedAccessException("Invite token is invalid.");
                if (!string.Equals(invite.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only pending invites can be accepted.");

                var onboardingPolicy = await _tenantOnboardingPolicyService.GetByTenantAsync(invite.TenantId, ct);
                await HandleExpiredInviteIfNeededAsync(invite, onboardingPolicy, ct);
                if (!string.Equals(invite.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only pending invites can be accepted.");

                var tenantProfile = await _tenantProfileRepository.GetAsync(
                    EntityKeyPolicy.TenantPartition(invite.TenantId),
                    EntityKeyPolicy.Row(invite.TenantId),
                    ct);

                var inviteTenantPartitionKey = EntityKeyPolicy.TenantPartition(invite.TenantId);
                var isAuthenticated = _userContext.IsAuthenticated;
                var matchedChannel = default(string);
                var existingMembership = default(TenantMembership);

                if (isAuthenticated)
                {
                    var platformUser = await GetPlatformUserOrThrowAsync(_userContext.UserId, ct);
                    matchedChannel = MatchVerifiedInviteContact(invite, platformUser);
                    existingMembership = await _membershipRepository.GetByUserIdAsync(inviteTenantPartitionKey, _userContext.UserId, ct);
                }

                var alreadyMember = IsCurrentTenantMember(existingMembership);
                var canRedeem = isAuthenticated && !alreadyMember && !string.IsNullOrWhiteSpace(matchedChannel);
                var nextStep = DetermineAcceptanceNextStep(isAuthenticated, alreadyMember, matchedChannel);

                await RecordInviteAuditAsync(
                    tenantId: invite.TenantId,
                    action: "invite_opened",
                    severity: "info",
                    targetId: invite.Id,
                    description: "Opened invite acceptance context.",
                    metadataJson: BuildInviteAuditMetadata("opened"),
                    ct: ct);

                return new InviteAcceptanceContextDto
                {
                    InviteId = invite.Id,
                    TenantId = invite.TenantId,
                    TenantName = string.IsNullOrWhiteSpace(tenantProfile?.TenantName) ? invite.TenantId.ToString("D") : tenantProfile.TenantName,
                    Role = invite.Role,
                    Status = invite.Status,
                    ExpiresAtUtc = invite.ExpiresAtUtc,
                    InvitedEmailMasked = MaskEmail(invite.InvitedEmail),
                    InvitedPhoneMasked = MaskPhone(invite.InvitedPhone),
                    IsAuthenticated = isAuthenticated,
                    RequiresAuthentication = !isAuthenticated,
                    CanRedeem = canRedeem,
                    NextStep = nextStep,
                    AuthenticatedUserMatchesInvite = !string.IsNullOrWhiteSpace(matchedChannel),
                    MatchedVerifiedContactChannel = matchedChannel,
                    AuthenticatedUserAlreadyMember = alreadyMember,
                    AuthenticatedUserMembershipStatus = existingMembership?.MembershipStatus
                };
            }
            catch (Exception ex) when (IsInviteAttemptFailure(ex))
            {
                await RecordInviteAuditAsync(
                    tenantId: invite?.TenantId,
                    action: "invite_open_failed",
                    severity: "warning",
                    targetId: id,
                    description: "Invite acceptance open failed.",
                    metadataJson: BuildInviteAuditMetadata("open_failed", reason: ClassifyInviteFailure(ex)),
                    ct: ct);
                throw;
            }
        }

        public async Task<InviteDto> CreateAsync(CreateInviteRequestDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
            ValidateInviteContact(dto.InvitedEmail, dto.InvitedPhone);
            var onboardingPolicy = await _tenantOnboardingPolicyService.GetCurrentAsync(ct);
            var role = await _roleDirectoryService.GetRequiredAssignableRoleAsync(_userContext.TenantId, dto.Role, ct);

            if (onboardingPolicy.ReserveSeatAtInviteTime)
                await _seatEntitlementService.ReserveSeatAsync(_userContext.TenantId, ct);

            var membershipId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var expiresAtUtc = NormalizeInviteExpiry(dto.ExpiresAtUtc, now, onboardingPolicy.DefaultInviteExpiryDays);
            var inviteToken = GenerateInviteToken();
            var inviterMembership = await _membershipRepository.GetByUserIdAsync(PartitionKey(), _userContext.UserId, ct);

            var membership = new TenantMembership
            {
                Id = membershipId,
                TenantId = _userContext.TenantId,
                UserId = Guid.Empty,
                PartitionKey = PartitionKey(),
                RowKey = EntityKeyPolicy.Row(membershipId),
                Role = role.Key,
                MembershipStatus = "Invited",
                SeatStatus = onboardingPolicy.ReserveSeatAtInviteTime ? "Reserved" : "Unassigned",
                InvitedEmail = Normalize(dto.InvitedEmail),
                InvitedPhone = Normalize(dto.InvitedPhone),
                IsOwner = false,
                IsBillingAdmin = role.GrantsBillingAdmin,
                DateCreated = now,
                DateInvited = now,
                DateUpdated = now,
                IsDeleted = false
            };

            var inviteId = Guid.NewGuid();
            var invite = new Invite
            {
                Id = inviteId,
                TenantId = _userContext.TenantId,
                ReservedSeatMembershipId = membershipId,
                SentByMembershipId = inviterMembership?.Id ?? Guid.Empty,
                PartitionKey = PartitionKey(),
                RowKey = EntityKeyPolicy.Row(inviteId),
                InvitedEmail = Normalize(dto.InvitedEmail),
                InvitedPhone = Normalize(dto.InvitedPhone),
                InviteTokenHash = HashInviteToken(inviteToken),
                Role = role.Key,
                Status = "Invited",
                ExpiresAtUtc = expiresAtUtc,
                DateCreated = now,
                DateUpdated = now,
                IsDeleted = false
            };

            await _membershipRepository.SaveAsync(membership, ct);
            await _inviteRepository.SaveAsync(invite, ct);
            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                Category = "admin",
                Action = "invite_created",
                Severity = "info",
                TargetType = "invite",
                TargetId = invite.Id.ToString("D"),
                Source = nameof(InviteService),
                Description = "Created tenant invite and reserved seat.",
                MetadataJson = BuildInviteAuditMetadata("created")
            }, ct);

            var result = InviteMapper.ToDto(invite);
            result.InviteToken = inviteToken;
            return result;
        }

        public async Task<InviteDto> ResendAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
            var invite = await GetInviteOrThrowAsync(id, ct);
            var onboardingPolicy = await _tenantOnboardingPolicyService.GetCurrentAsync(ct);

            if (!string.Equals(invite.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only pending invites can be resent.");

            var inviteToken = GenerateInviteToken();
            invite.DateUpdated = DateTime.UtcNow;
            invite.InviteTokenHash = HashInviteToken(inviteToken);
            if (invite.ExpiresAtUtc <= DateTime.UtcNow)
                invite.ExpiresAtUtc = DateTime.UtcNow.AddDays(Math.Clamp(onboardingPolicy.DefaultInviteExpiryDays, 1, 90));

            var saved = await _inviteRepository.SaveAsync(invite, ct);
            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                Category = "admin",
                Action = "invite_resent",
                Severity = "info",
                TargetType = "invite",
                TargetId = saved.Id.ToString("D"),
                Source = nameof(InviteService),
                Description = "Resent tenant invite."
            }, ct);
            var result = InviteMapper.ToDto(saved);
            result.InviteToken = inviteToken;
            return result;
        }

        public async Task<InviteDto> CancelAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _roleAccess.RequirePermissionAsync(TurnKeyPermissionKeys.MembershipManage, ct);
            var invite = await GetInviteOrThrowAsync(id, ct);

            if (string.Equals(invite.Status, "Redeemed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Redeemed invites cannot be cancelled.");

            invite.Status = "Cancelled";
            invite.InviteTokenHash = string.Empty;
            invite.DateUpdated = DateTime.UtcNow;
            var savedInvite = await _inviteRepository.SaveAsync(invite, ct);

            var membership = await _membershipRepository.GetAsync(PartitionKey(), EntityKeyPolicy.Row(invite.ReservedSeatMembershipId), ct);
            if (membership is not null)
            {
                var previousSeatStatus = membership.SeatStatus;
                membership.MembershipStatus = "Cancelled";
                membership.SeatStatus = "Released";
                membership.DateRemoved = DateTime.UtcNow;
                membership.DateUpdated = DateTime.UtcNow;
                await _membershipRepository.SaveAsync(membership, ct);

                if (string.Equals(previousSeatStatus, "Reserved", StringComparison.OrdinalIgnoreCase))
                    await _seatEntitlementService.ReleaseSeatAsync(_userContext.TenantId, previousSeatStatus, ct);
            }

            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                Category = "admin",
                Action = "invite_cancelled",
                Severity = "info",
                TargetType = "invite",
                TargetId = savedInvite.Id.ToString("D"),
                Source = nameof(InviteService),
                Description = "Cancelled tenant invite and released reserved seat."
            }, ct);

            return InviteMapper.ToDto(savedInvite);
        }

        public async Task<TenantMembershipDto> RedeemAsync(Guid id, RedeemInviteRequestDto dto, CancellationToken ct = default)
        {
            Invite? invite = null;

            try
            {
                EnsureAuthenticated();
                if (string.IsNullOrWhiteSpace(dto.InviteToken))
                    throw new ArgumentException("InviteToken is required.", nameof(dto));

                ThrowIfRateLimited(id);

                invite = await GetInviteByIdOrThrowAsync(id, ct);
                if (!string.Equals(invite.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only pending invites can be redeemed.");

                var onboardingPolicy = await _tenantOnboardingPolicyService.GetByTenantAsync(invite.TenantId, ct);
                await HandleExpiredInviteIfNeededAsync(invite, onboardingPolicy, ct);
                if (!string.Equals(invite.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only pending invites can be redeemed.");
                if (!VerifyInviteToken(invite.InviteTokenHash, dto.InviteToken))
                    throw new UnauthorizedAccessException("Invite token is invalid.");

                var platformUser = await GetPlatformUserOrThrowAsync(_userContext.UserId, ct);
                var matchedChannel = MatchVerifiedInviteContact(invite, platformUser);
                if (matchedChannel is null)
                    throw new UnauthorizedAccessException("Invite contact does not match the signed-in user's verified contact.");

                var inviteTenantPartitionKey = EntityKeyPolicy.TenantPartition(invite.TenantId);
                var existingMembership = await _membershipRepository.GetByUserIdAsync(inviteTenantPartitionKey, _userContext.UserId, ct);
                if (IsCurrentTenantMember(existingMembership))
                    throw new InvalidOperationException("Signed-in user is already a member of the invited tenant.");

                var membership = await _membershipRepository.GetAsync(inviteTenantPartitionKey, EntityKeyPolicy.Row(invite.ReservedSeatMembershipId), ct)
                    ?? throw new KeyNotFoundException("Reserved membership not found.");

                membership.UserId = _userContext.UserId;
                membership.MembershipStatus = "Active";
                var role = await _roleDirectoryService.GetRequiredAssignableRoleAsync(invite.TenantId, invite.Role, ct);
                membership.Role = role.Key;
                membership.IsOwner = false;
                membership.IsBillingAdmin = role.GrantsBillingAdmin;
                membership.VerifiedJoinChannel = matchedChannel;
                membership.DateJoined = DateTime.UtcNow;
                membership.DateUpdated = DateTime.UtcNow;

                if (onboardingPolicy.AutoAssignSeatOnActivation)
                {
                    await _seatEntitlementService.AssignSeatAsync(invite.TenantId, ct);
                    membership.SeatStatus = "Assigned";
                }
                else if (string.Equals(membership.SeatStatus, "Reserved", StringComparison.OrdinalIgnoreCase))
                {
                    membership.SeatStatus = "Reserved";
                }
                else
                {
                    membership.SeatStatus = "Unassigned";
                }

                invite.RedeemedByUserId = _userContext.UserId;
                invite.Status = "Redeemed";
                invite.InviteTokenHash = string.Empty;
                invite.DateRedeemed = DateTime.UtcNow;
                invite.DateUpdated = DateTime.UtcNow;

                await _membershipRepository.SaveAsync(membership, ct);
                await _inviteRepository.SaveAsync(invite, ct);
                ResetRedeemRateLimit(id);

                await RecordInviteAuditAsync(
                    tenantId: invite.TenantId,
                    userId: _userContext.UserId,
                    action: "invite_redeemed",
                    severity: "info",
                    targetId: invite.Id,
                    description: "Redeemed tenant invite using verified contact match and possession of the invite token.",
                    metadataJson: BuildInviteAuditMetadata("redeemed", matchedChannel: matchedChannel),
                    ct: ct);

                return TenantMembershipMapper.ToDto(membership);
            }
            catch (TooManyRequestsException)
            {
                await RecordInviteAuditAsync(
                    tenantId: invite?.TenantId,
                    userId: _userContext.IsAuthenticated ? _userContext.UserId : null,
                    action: "invite_redeem_failed",
                    severity: "warning",
                    targetId: id,
                    description: "Invite redeem attempt was rate-limited.",
                    metadataJson: BuildInviteAuditMetadata("redeem_failed", reason: "rate_limited"),
                    ct: ct);
                throw;
            }
            catch (Exception ex) when (IsInviteAttemptFailure(ex))
            {
                RegisterRedeemFailure(id);
                await RecordInviteAuditAsync(
                    tenantId: invite?.TenantId,
                    userId: _userContext.IsAuthenticated ? _userContext.UserId : null,
                    action: "invite_redeem_failed",
                    severity: "warning",
                    targetId: id,
                    description: "Invite redeem attempt failed.",
                    metadataJson: BuildInviteAuditMetadata("redeem_failed", reason: ClassifyInviteFailure(ex)),
                    ct: ct);
                throw;
            }
        }

        private async Task<Invite> GetInviteOrThrowAsync(Guid id, CancellationToken ct)
            => await _inviteRepository.GetAsync(PartitionKey(), EntityKeyPolicy.Row(id), ct)
               ?? throw new KeyNotFoundException("Invite not found.");

        private async Task<Invite> GetInviteByIdOrThrowAsync(Guid id, CancellationToken ct)
            => await _inviteRepository.GetByIdAsync(id, ct)
               ?? throw new KeyNotFoundException("Invite not found.");

        private async Task<PlatformUser> GetPlatformUserOrThrowAsync(Guid userId, CancellationToken ct)
            => await _platformUserRepository.GetAsync($"USER={userId:N}", "PROFILE", ct)
               ?? throw new KeyNotFoundException("Platform user not found.");

        private string PartitionKey() => EntityKeyPolicy.TenantPartition(_userContext.TenantId);

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static void ValidateInviteContact(string? email, string? phone)
        {
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException("Either invited email or invited phone is required.");
        }

        private static DateTime NormalizeInviteExpiry(DateTime? expiresAtUtc, DateTime now, int defaultInviteExpiryDays)
        {
            var normalized = expiresAtUtc ?? now.AddDays(Math.Clamp(defaultInviteExpiryDays, 1, 90));
            if (normalized <= now)
                throw new ArgumentException("ExpiresAtUtc must be in the future.", nameof(expiresAtUtc));

            return normalized;
        }

        private async Task HandleExpiredInviteIfNeededAsync(Invite invite, MedInsights.Lib.Dtos.TenantOnboardingPolicyDto onboardingPolicy, CancellationToken ct)
        {
            if (invite.ExpiresAtUtc >= DateTime.UtcNow)
                return;

            if (!string.Equals(invite.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invite has expired.");

            if (string.Equals(onboardingPolicy.ExpiredInviteHandling, TenantOnboardingPolicyDefaults.CancelAndReleaseSeat, StringComparison.Ordinal))
            {
                await InvalidateExpiredInviteAsync(invite, releaseSeat: true, ct);
                throw new InvalidOperationException("Invite has expired.");
            }

            if (string.Equals(onboardingPolicy.ExpiredInviteHandling, TenantOnboardingPolicyDefaults.MarkExpiredKeepSeat, StringComparison.Ordinal))
            {
                await InvalidateExpiredInviteAsync(invite, releaseSeat: false, ct);
                throw new InvalidOperationException("Invite has expired.");
            }
        }

        private async Task InvalidateExpiredInviteAsync(Invite invite, bool releaseSeat, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            invite.Status = "Expired";
            invite.InviteTokenHash = string.Empty;
            invite.DateUpdated = now;
            await _inviteRepository.SaveAsync(invite, ct);

            var membership = await _membershipRepository.GetAsync(EntityKeyPolicy.TenantPartition(invite.TenantId), EntityKeyPolicy.Row(invite.ReservedSeatMembershipId), ct);
            if (membership is null)
                return;

            if (releaseSeat)
            {
                var previousSeatStatus = membership.SeatStatus;
                membership.MembershipStatus = "Cancelled";
                membership.SeatStatus = "Released";
                membership.DateRemoved ??= now;
                membership.DateUpdated = now;
                await _membershipRepository.SaveAsync(membership, ct);

                if (string.Equals(previousSeatStatus, "Reserved", StringComparison.OrdinalIgnoreCase))
                    await _seatEntitlementService.ReleaseSeatAsync(invite.TenantId, previousSeatStatus, ct);
            }
            else
            {
                membership.MembershipStatus = "Expired";
                membership.DateUpdated = now;
                await _membershipRepository.SaveAsync(membership, ct);
            }
        }

        private static string GenerateInviteToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

        private static string? MatchVerifiedInviteContact(Invite invite, PlatformUser platformUser)
        {
            var verifiedEmail = platformUser.EmailVerified ? NormalizeEmail(platformUser.PrimaryEmail) : null;
            var verifiedPhone = platformUser.PhoneVerified ? NormalizePhone(platformUser.PrimaryPhone) : null;
            var invitedEmail = NormalizeEmail(invite.InvitedEmail);
            var invitedPhone = NormalizePhone(invite.InvitedPhone);

            if (!string.IsNullOrWhiteSpace(invitedEmail)
                && !string.IsNullOrWhiteSpace(verifiedEmail)
                && string.Equals(invitedEmail, verifiedEmail, StringComparison.Ordinal))
                return "email";

            if (!string.IsNullOrWhiteSpace(invitedPhone)
                && !string.IsNullOrWhiteSpace(verifiedPhone)
                && string.Equals(invitedPhone, verifiedPhone, StringComparison.Ordinal))
                return "phone";

            return null;
        }

        private static string HashInviteToken(string inviteToken)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inviteToken)));

        private static bool VerifyInviteToken(string? storedInviteTokenHash, string inviteToken)
        {
            if (string.IsNullOrWhiteSpace(storedInviteTokenHash) || string.IsNullOrWhiteSpace(inviteToken))
                return false;

            var expectedBytes = Encoding.UTF8.GetBytes(storedInviteTokenHash);
            var actualBytes = Encoding.UTF8.GetBytes(HashInviteToken(inviteToken));
            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        private static string? NormalizePhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private void ThrowIfRateLimited(Guid inviteId)
        {
            var cacheKey = BuildRedeemRateLimitKey(inviteId);
            if (!_memoryCache.TryGetValue(cacheKey, out RedeemRateLimitState? state) || state is null)
                return;

            var now = DateTime.UtcNow;
            if (state.BlockedUntilUtc.HasValue && state.BlockedUntilUtc.Value > now)
                throw new TooManyRequestsException("Too many invite redeem attempts. Try again later.");
        }

        private void RegisterRedeemFailure(Guid inviteId)
        {
            var now = DateTime.UtcNow;
            var cacheKey = BuildRedeemRateLimitKey(inviteId);
            var state = _memoryCache.Get<RedeemRateLimitState>(cacheKey);

            if (state is null || state.WindowStartedUtc.Add(RedeemAttemptWindow) <= now)
            {
                state = new RedeemRateLimitState
                {
                    Count = 1,
                    WindowStartedUtc = now
                };
            }
            else
            {
                state.Count++;
                if (state.Count >= RedeemAttemptLimit)
                    state.BlockedUntilUtc = now.Add(RedeemBlockDuration);
            }

            _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = RedeemBlockDuration > RedeemAttemptWindow ? RedeemBlockDuration : RedeemAttemptWindow
            });
        }

        private void ResetRedeemRateLimit(Guid inviteId)
            => _memoryCache.Remove(BuildRedeemRateLimitKey(inviteId));

        private string BuildRedeemRateLimitKey(Guid inviteId)
            => $"invite-redeem:{inviteId:D}:{GetClientFingerprint()}";

        private string BuildInviteAuditMetadata(string outcome, string? reason = null, string? matchedChannel = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var metadata = new Dictionary<string, object?>
            {
                ["outcome"] = outcome,
                ["reason"] = reason,
                ["matchedChannel"] = matchedChannel,
                ["authenticated"] = _userContext.IsAuthenticated,
                ["traceId"] = httpContext?.TraceIdentifier,
                ["requestPath"] = httpContext?.Request.Path.Value,
                ["clientFingerprint"] = GetClientFingerprint()
            };

            return JsonSerializer.Serialize(metadata);
        }

        private async Task RecordInviteAuditAsync(
            Guid? tenantId,
            string action,
            string severity,
            Guid targetId,
            string description,
            string? metadataJson,
            CancellationToken ct,
            Guid? userId = null)
        {
            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                TenantId = tenantId,
                UserId = userId,
                Category = "security",
                Action = action,
                Severity = severity,
                TargetType = "invite",
                TargetId = targetId.ToString("D"),
                Source = nameof(InviteService),
                Description = description,
                MetadataJson = metadataJson
            }, ct);
        }

        private string GetClientFingerprint()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ip = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString() ?? "unknown";
            var source = $"{ip}|{userAgent}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        }

        private static bool IsInviteAttemptFailure(Exception ex)
            => ex is ArgumentException
            || ex is KeyNotFoundException
            || ex is InvalidOperationException
            || ex is UnauthorizedAccessException;

        private static string ClassifyInviteFailure(Exception ex)
        {
            return ex switch
            {
                ArgumentException => "invalid_request",
                KeyNotFoundException keyNotFound when keyNotFound.Message.Contains("Reserved membership", StringComparison.OrdinalIgnoreCase) => "reserved_membership_missing",
                KeyNotFoundException => "invite_not_found",
                InvalidOperationException invalidOperation when invalidOperation.Message.Contains("already a member", StringComparison.OrdinalIgnoreCase) => "already_member",
                InvalidOperationException invalidOperation when invalidOperation.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) => "invite_expired",
                InvalidOperationException => "invite_not_pending",
                UnauthorizedAccessException unauthorized when unauthorized.Message.Contains("token is invalid", StringComparison.OrdinalIgnoreCase) => "invalid_token",
                UnauthorizedAccessException unauthorized when unauthorized.Message.Contains("verified contact", StringComparison.OrdinalIgnoreCase) => "verified_contact_mismatch",
                UnauthorizedAccessException => "unauthorized",
                _ => "unknown"
            };
        }

        private static string DetermineAcceptanceNextStep(bool isAuthenticated, bool alreadyMember, string? matchedChannel)
        {
            if (!isAuthenticated)
                return "authenticate";

            if (alreadyMember)
                return "already_joined";

            if (!string.IsNullOrWhiteSpace(matchedChannel))
                return "redeem";

            return "switch_account_or_verify_contact";
        }

        private static bool IsCurrentTenantMember(TenantMembership? membership)
        {
            return membership is not null
                && !string.Equals(membership.MembershipStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(membership.SeatStatus, "Released", StringComparison.OrdinalIgnoreCase);
        }

        private static string? MaskEmail(string? value)
        {
            var normalized = NormalizeEmail(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var atIndex = normalized.IndexOf('@');
            if (atIndex <= 0 || atIndex == normalized.Length - 1)
                return "***";

            var local = normalized[..atIndex];
            var domain = normalized[(atIndex + 1)..];
            var visible = local.Length <= 2 ? local[..1] : local[..2];
            return $"{visible}***@{domain}";
        }

        private static string? MaskPhone(string? value)
        {
            var normalized = NormalizePhone(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var last4 = normalized.Length <= 4 ? normalized : normalized[^4..];
            return $"***-***-{last4}";
        }

        private sealed class RedeemRateLimitState
        {
            public int Count { get; set; }
            public DateTime WindowStartedUtc { get; set; }
            public DateTime? BlockedUntilUtc { get; set; }
        }
    }
}
