namespace MedInsights.Lib.Dtos;

public sealed class ManagedTenantUsersDto
{
    public string TenantKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public IReadOnlyList<ManagedTenantUserDto> Users { get; set; } = [];
}

public sealed class ManagedTenantUserDto
{
    public Guid MembershipId { get; set; }
    public Guid? InviteId { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? InvitedAtUtc { get; set; }
    public DateTime? JoinedAtUtc { get; set; }
}

public sealed class CreateManagedUserInviteRequestDto
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = TenantRoleCatalog.Staff;
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class ManagedUserInviteResultDto
{
    public string TenantKey { get; set; } = string.Empty;
    public string TenantDisplayName { get; set; } = string.Empty;
    public InviteDto Invite { get; set; } = new();
}
