using MedInsights.Lib.Dtos;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class BillingAdminService : IBillingAdminService
    {
        private readonly IUserContext _userContext;
        private readonly ITenantBillingAccountService _billingAccountService;
        private readonly ITenantSubscriptionService _subscriptionService;
        private readonly ITenantSeatEntitlementService _seatEntitlementService;
        private readonly ITenantCreditBalanceService _creditBalanceService;
        private readonly ITenantMembershipService _membershipService;
        private readonly IInviteService _inviteService;
        private readonly ITenantMembershipRepository _tenantMembershipRepository;
        private readonly IInviteRepository _inviteRepository;
        private readonly ITenantSeatEntitlementRepository _seatEntitlementRepository;
        private readonly IBillingLedgerRepository _billingLedgerRepository;
        private readonly ICreditLedgerRepository _creditLedgerRepository;
        private readonly IUserCreditPeriodRepository _userCreditPeriodRepository;
        private readonly IAuditService _auditService;
        private readonly IOperationalAlertService _alertService;
        private readonly ITenantMembershipAuthorizationService _membershipAuthorizationService;

        public BillingAdminService(
            IUserContext userContext,
            ITenantBillingAccountService billingAccountService,
            ITenantSubscriptionService subscriptionService,
            ITenantSeatEntitlementService seatEntitlementService,
            ITenantCreditBalanceService creditBalanceService,
            ITenantMembershipService membershipService,
            IInviteService inviteService,
            ITenantMembershipRepository tenantMembershipRepository,
            IInviteRepository inviteRepository,
            ITenantSeatEntitlementRepository seatEntitlementRepository,
            IBillingLedgerRepository billingLedgerRepository,
            ICreditLedgerRepository creditLedgerRepository,
            IUserCreditPeriodRepository userCreditPeriodRepository,
            IAuditService auditService,
            IOperationalAlertService alertService,
            ITenantMembershipAuthorizationService membershipAuthorizationService)
        {
            _userContext = userContext;
            _billingAccountService = billingAccountService;
            _subscriptionService = subscriptionService;
            _seatEntitlementService = seatEntitlementService;
            _creditBalanceService = creditBalanceService;
            _membershipService = membershipService;
            _inviteService = inviteService;
            _tenantMembershipRepository = tenantMembershipRepository;
            _inviteRepository = inviteRepository;
            _seatEntitlementRepository = seatEntitlementRepository;
            _billingLedgerRepository = billingLedgerRepository;
            _creditLedgerRepository = creditLedgerRepository;
            _userCreditPeriodRepository = userCreditPeriodRepository;
            _auditService = auditService;
            _alertService = alertService;
            _membershipAuthorizationService = membershipAuthorizationService;
        }

        public async Task<BillingSummaryDto> GetSummaryAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var billingAccount = await _billingAccountService.GetCurrentAsync(ct);
            var subscriptions = await _subscriptionService.GetAllAsync(ct);
            var seatEntitlement = await _seatEntitlementService.GetCurrentAsync(ct);
            var creditBalance = await _creditBalanceService.GetCurrentAsync(ct);
            var memberships = await _membershipService.GetAllAsync(ct);
            var invites = await _inviteService.GetAllAsync(ct);

            return new BillingSummaryDto
            {
                BillingAccount = billingAccount,
                Subscription = subscriptions.OrderByDescending(x => x.DateUpdated ?? x.DateCreated).FirstOrDefault(),
                SeatEntitlement = seatEntitlement,
                CreditBalance = creditBalance,
                ActiveAssignedUsers = memberships.Count(x =>
                    string.Equals(x.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.SeatStatus, "Assigned", StringComparison.OrdinalIgnoreCase)),
                PendingInvites = invites.Count(x => string.Equals(x.Status, "Invited", StringComparison.OrdinalIgnoreCase))
            };
        }

        public async Task<IReadOnlyList<BillingLedgerDto>> GetBillingLedgerAsync(int take = 100, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var entities = await _billingLedgerRepository.GetByTenantAsync(_userContext.TenantId, NormalizeTake(take), ct);
            return entities.Select(BillingLedgerMapper.ToDto).ToList();
        }

        public async Task<IReadOnlyList<CreditLedgerDto>> GetCreditLedgerAsync(int take = 100, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var entities = await _creditLedgerRepository.GetByTenantAsync(_userContext.TenantId, NormalizeTake(take), ct);
            return entities.Select(CreditLedgerMapper.ToDto).ToList();
        }

        public async Task<IReadOnlyList<UserCreditPeriodDto>> GetCreditPeriodsAsync(int take = 100, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var entities = await _userCreditPeriodRepository.GetByTenantAsync(_userContext.TenantId, NormalizeTake(take), ct);
            return entities.Select(UserCreditPeriodMapper.ToDto).ToList();
        }

        public async Task<IReadOnlyList<TenantMembershipDto>> GetTenantUsersAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return (await _membershipService.GetAllAsync(ct)).ToList();
        }

        public async Task<IReadOnlyList<InviteDto>> GetInvitesAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return (await _inviteService.GetAllAsync(ct)).ToList();
        }

        public async Task<IReadOnlyList<AuditEventDto>> GetAuditEventsAsync(int take = 100, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return await _auditService.GetRecentAsync(take, ct);
        }

        public async Task<IReadOnlyList<OperationalAlertDto>> GetOperationalAlertsAsync(string? status = null, int take = 100, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return await _alertService.GetRecentAsync(status, take, ct);
        }

        public async Task<OperationalAlertDto> AcknowledgeOperationalAlertAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return await _alertService.AcknowledgeAsync(id, ct);
        }

        public async Task<TenantSeatEntitlementDto?> GetSeatViewAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return await _seatEntitlementService.GetCurrentAsync(ct);
        }

        public async Task<InviteRepairReportDto> ReconcileInviteStateAsync(bool apply = false, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var tenantId = _userContext.TenantId;
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var now = DateTime.UtcNow;
            var findings = new List<string>();

            var invites = (await _inviteRepository.GetByPartitionPagedAsync(partitionKey, 500, ct: ct)).Results.ToList();
            var memberships = (await _tenantMembershipRepository.GetByPartitionPagedAsync(partitionKey, 500, ct: ct)).Results.ToList();
            var entitlement = await _seatEntitlementRepository.GetCurrentAsync(partitionKey, ct);
            var entitlementBefore = entitlement is null ? null : TenantSeatEntitlementMapper.ToDto(entitlement);

            var invitesByMembershipId = invites
                .Where(x => x.ReservedSeatMembershipId != Guid.Empty)
                .GroupBy(x => x.ReservedSeatMembershipId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var activePendingInviteMembershipIds = memberships
                .Where(IsPendingInviteMembership)
                .Select(x => x.Id)
                .ToHashSet();

            var orphanedInvites = invites
                .Where(x => string.Equals(x.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                .Where(x => !activePendingInviteMembershipIds.Contains(x.ReservedSeatMembershipId))
                .ToList();

            var strandedMemberships = memberships
                .Where(IsReservedInviteMembership)
                .Where(m =>
                {
                    if (!invitesByMembershipId.TryGetValue(m.Id, out var linkedInvites))
                        return true;

                    return !linkedInvites.Any(i =>
                        string.Equals(i.Status, "Invited", StringComparison.OrdinalIgnoreCase)
                        && i.ExpiresAtUtc > now
                        && !string.IsNullOrWhiteSpace(i.InviteTokenHash));
                })
                .ToList();

            foreach (var invite in orphanedInvites)
                findings.Add($"Orphaned pending invite {invite.Id:D} references missing or non-reserved membership {invite.ReservedSeatMembershipId:D}.");

            foreach (var membership in strandedMemberships)
                findings.Add($"Reserved membership {membership.Id:D} has no active pending invite and should be released.");

            var computedAssignedSeats = memberships.Count(IsAssignedMembership);
            var computedReservedSeats = memberships.Count(IsReservedInviteMembership);
            var computedAvailableSeats = entitlement is null
                ? 0
                : Math.Max(0, entitlement.PurchasedSeats - computedAssignedSeats - computedReservedSeats);

            var entitlementAdjusted = entitlement is not null
                && (entitlement.AssignedSeats != computedAssignedSeats
                    || entitlement.ReservedSeats != computedReservedSeats
                    || entitlement.AvailableSeats != computedAvailableSeats);

            if (entitlementAdjusted)
            {
                findings.Add(
                    $"Seat entitlement counts differ from memberships. Stored assigned/reserved/available = {entitlement!.AssignedSeats}/{entitlement.ReservedSeats}/{entitlement.AvailableSeats}, computed = {computedAssignedSeats}/{computedReservedSeats}/{computedAvailableSeats}.");
            }

            if (apply)
            {
                foreach (var invite in orphanedInvites)
                {
                    invite.Status = "Cancelled";
                    invite.InviteTokenHash = string.Empty;
                    invite.DateUpdated = now;
                    await _inviteRepository.SaveAsync(invite, ct);
                }

                foreach (var membership in strandedMemberships)
                {
                    membership.MembershipStatus = "Cancelled";
                    membership.SeatStatus = "Released";
                    membership.DateRemoved ??= now;
                    membership.DateUpdated = now;
                    await _tenantMembershipRepository.SaveAsync(membership, ct);
                }

                if (entitlementAdjusted)
                {
                    entitlement!.AssignedSeats = computedAssignedSeats;
                    entitlement.ReservedSeats = computedReservedSeats;
                    entitlement.AvailableSeats = computedAvailableSeats;
                    entitlement.DateUpdated = now;
                    entitlement = await _seatEntitlementRepository.SaveAsync(entitlement, ct);
                }

                await _auditService.RecordAsync(new RecordAuditEventRequestDto
                {
                    TenantId = tenantId,
                    UserId = _userContext.UserId,
                    Category = "admin",
                    Action = "invite_state_reconciled",
                    Severity = "warning",
                    TargetType = "tenant",
                    TargetId = tenantId.ToString("D"),
                    Source = nameof(BillingAdminService),
                    Description = $"Reconciled invite state. Cancelled {orphanedInvites.Count} invites, released {strandedMemberships.Count} memberships, seat entitlement adjusted: {entitlementAdjusted}."
                }, ct);
            }

            return new InviteRepairReportDto
            {
                Applied = apply,
                TenantId = tenantId,
                PendingInviteCount = invites.Count(x => string.Equals(x.Status, "Invited", StringComparison.OrdinalIgnoreCase)),
                ReservedMembershipCount = memberships.Count(IsReservedInviteMembership),
                OrphanedInviteCount = orphanedInvites.Count,
                StrandedReservedMembershipCount = strandedMemberships.Count,
                CancelledInviteCount = apply ? orphanedInvites.Count : 0,
                ReleasedMembershipCount = apply ? strandedMemberships.Count : 0,
                SeatEntitlementAdjusted = apply && entitlementAdjusted,
                SeatEntitlementBefore = entitlementBefore,
                SeatEntitlementAfter = entitlement is null ? null : TenantSeatEntitlementMapper.ToDto(entitlement),
                Findings = findings
            };
        }

        public async Task<TenantCreditBalanceDto?> GetCreditViewAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return await _creditBalanceService.GetCurrentAsync(ct);
        }

        public async Task<TenantBillingAccountDto?> GetTopUpSettingsAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return await _billingAccountService.GetCurrentAsync(ct);
        }

        public async Task<TenantBillingAccountDto> UpdateTopUpSettingsAsync(TenantBillingAccountDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            var saved = await _billingAccountService.UpsertAsync(dto, ct);
            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                Category = "admin",
                Action = "topup_settings_updated",
                Severity = "info",
                TargetType = "tenant_billing_account",
                TargetId = saved.Id.ToString("D"),
                Source = nameof(BillingAdminService),
                Description = "Updated tenant top-up settings."
            }, ct);
            return saved;
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static int NormalizeTake(int take) => Math.Clamp(take, 1, 500);

        private static bool IsAssignedMembership(TenantMembership membership)
            => string.Equals(membership.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase)
               && string.Equals(membership.SeatStatus, "Assigned", StringComparison.OrdinalIgnoreCase)
               && membership.UserId != Guid.Empty;

        private static bool IsReservedInviteMembership(TenantMembership membership)
            => string.Equals(membership.SeatStatus, "Reserved", StringComparison.OrdinalIgnoreCase)
               && (string.Equals(membership.MembershipStatus, "Invited", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(membership.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase));

        private static bool IsPendingInviteMembership(TenantMembership membership)
            => string.Equals(membership.MembershipStatus, "Invited", StringComparison.OrdinalIgnoreCase)
               && (string.Equals(membership.SeatStatus, "Reserved", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(membership.SeatStatus, "Unassigned", StringComparison.OrdinalIgnoreCase));
    }
}
