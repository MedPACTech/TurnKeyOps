using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services;

public sealed class PlatformUserAdministrationService : IPlatformUserAdministrationService
{
    private readonly UserAdministrationOptions _options;
    private readonly ITenantMembershipRepository _membershipRepository;
    private readonly IInviteRepository _inviteRepository;
    private readonly ITrustedTenantInviteService _inviteService;
    private readonly IAuditService _auditService;

    public PlatformUserAdministrationService(
        IOptions<UserAdministrationOptions> options,
        ITenantMembershipRepository membershipRepository,
        IInviteRepository inviteRepository,
        ITrustedTenantInviteService inviteService,
        IAuditService auditService)
    {
        _options = options.Value;
        _membershipRepository = membershipRepository;
        _inviteRepository = inviteRepository;
        _inviteService = inviteService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<ManagedTenantUsersDto>> GetTenantsAsync(CancellationToken ct = default)
    {
        var results = new List<ManagedTenantUsersDto>();
        foreach (var pair in ValidTenants().OrderBy(x => x.Value.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(pair.Value.TenantId);
            var membershipPage = await _membershipRepository.GetByPartitionPagedAsync(partitionKey, 200, ct: ct);
            var invitePage = await _inviteRepository.GetByPartitionPagedAsync(partitionKey, 200, ct: ct);
            var inviteByMembership = invitePage.Results
                .GroupBy(x => x.ReservedSeatMembershipId)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(i => i.DateCreated).First());

            results.Add(new ManagedTenantUsersDto
            {
                TenantKey = pair.Key,
                DisplayName = pair.Value.DisplayName,
                TenantId = pair.Value.TenantId,
                Users = membershipPage.Results
                    .Where(x => !x.IsDeleted)
                    .OrderByDescending(x => x.DateUpdated ?? x.DateCreated)
                    .Select(x => new ManagedTenantUserDto
                    {
                        MembershipId = x.Id,
                        InviteId = inviteByMembership.TryGetValue(x.Id, out var invite) ? invite.Id : null,
                        UserId = x.UserId == Guid.Empty ? null : x.UserId,
                        Email = x.InvitedEmail,
                        Phone = x.InvitedPhone,
                        Role = x.Role,
                        Status = x.MembershipStatus,
                        InvitedAtUtc = x.DateInvited,
                        JoinedAtUtc = x.DateJoined
                    })
                    .ToArray()
            });
        }

        return results;
    }

    public async Task<ManagedUserInviteResultDto> CreateCustomerAdminInviteAsync(
        string tenantKey,
        CreateManagedUserInviteRequestDto request,
        CancellationToken ct = default)
    {
        var (normalizedKey, tenant) = GetTenant(tenantKey);
        var invite = await _inviteService.CreateForTenantAsync(tenant.TenantId, new CreateInviteRequestDto
        {
            InvitedEmail = request.Email,
            InvitedPhone = request.Phone,
            Role = TenantRoleCatalog.Admin,
            ExpiresAtUtc = request.ExpiresAtUtc
        }, ct);

        await _auditService.RecordAsync(new RecordAuditEventRequestDto
        {
            TenantId = tenant.TenantId,
            Category = "admin",
            Action = "platform_customer_admin_invited",
            Severity = "info",
            TargetType = "invite",
            TargetId = invite.Id.ToString("D"),
            Source = nameof(PlatformUserAdministrationService),
            Description = $"Internal Admin invited a Customer Admin to {tenant.DisplayName}."
        }, ct);

        return new ManagedUserInviteResultDto
        {
            TenantKey = normalizedKey,
            TenantDisplayName = tenant.DisplayName,
            Invite = invite
        };
    }

    private IEnumerable<KeyValuePair<string, ManagedTenantDefinition>> ValidTenants() =>
        _options.Tenants.Where(x => x.Value.TenantId != Guid.Empty && !string.IsNullOrWhiteSpace(x.Value.DisplayName));

    private (string Key, ManagedTenantDefinition Tenant) GetTenant(string tenantKey)
    {
        var normalized = tenantKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || !_options.Tenants.TryGetValue(normalized, out var tenant)
            || tenant.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(tenant.DisplayName))
        {
            throw new KeyNotFoundException("Managed tenant not found.");
        }

        return (normalized.ToLowerInvariant(), tenant);
    }
}
