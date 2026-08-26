namespace MedInsights.Lib.Authorization;

public static class TurnKeyAuthorizationPolicies
{
    public const string TenantAccess = "TurnKey.TenantAccess";
    public const string TenantStaff = "TurnKey.TenantStaff";
    public const string TenantAdmin = "TurnKey.TenantAdmin";
    public const string BillingAdmin = "TurnKey.BillingAdmin";
    public const string InternalAdmin = "TurnKey.InternalAdmin";

    public const string TenantRoleClaimType = "turnkey_tenant_role";
    public const string ClaimsTransformedMarker = "turnkey_roles_resolved";
}

public static class TurnKeyAuthorizationRoles
{
    public const string InternalAdmin = "internal_admin";
    public const string Owner = TenantRoleCatalog.Owner;
    public const string Admin = TenantRoleCatalog.Admin;
    public const string BillingAdmin = TenantRoleCatalog.BillingAdmin;
    public const string Member = TenantRoleCatalog.Member;
    public const string Staff = TenantRoleCatalog.Staff;
    public const string Contact = TenantRoleCatalog.Contact;

    public static readonly IReadOnlySet<string> TenantRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Owner,
        Admin,
        BillingAdmin,
        Member,
        Staff,
        Contact
    };
}

public static class TurnKeyPermissionKeys
{
    public const string TenantRead = "tenant.read";
    public const string TenantManage = "tenant.manage";
    public const string OperationsRead = "operations.read";
    public const string OperationsManage = "operations.manage";
    public const string EstimateDefaultsRead = "estimate_defaults.read";
    public const string EstimateDefaultsManage = "estimate_defaults.manage";
    public const string TenantSettingsRead = "tenant_settings.read";
    public const string TenantSettingsManage = "tenant_settings.manage";
    public const string BillingRead = "billing.read";
    public const string BillingManage = "billing.manage";
    public const string MembershipManage = "membership.manage";
    public const string MembershipOwnerGrant = "membership.owner_grant";
}
