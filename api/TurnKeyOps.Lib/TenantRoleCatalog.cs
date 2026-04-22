using MedInsights.Lib.Dtos;

namespace MedInsights.Lib
{
    public static class TenantRoleCatalog
    {
        public const string Owner = "owner";
        public const string Admin = "admin";
        public const string BillingAdmin = "billing_admin";
        public const string Member = "member";

        private static readonly IReadOnlyList<TenantRoleDefinitionDto> Roles = new[]
        {
            new TenantRoleDefinitionDto
            {
                Key = Owner,
                Name = "Owner",
                Description = "Full tenant control. Reserved for the tenant owner.",
                IsAssignable = false,
                GrantsOwnership = true,
                GrantsBillingAdmin = true
            },
            new TenantRoleDefinitionDto
            {
                Key = Admin,
                Name = "Admin",
                Description = "Can manage membership, invites, and tenant role assignments.",
                IsAssignable = true,
                GrantsOwnership = false,
                GrantsBillingAdmin = false
            },
            new TenantRoleDefinitionDto
            {
                Key = BillingAdmin,
                Name = "Billing Admin",
                Description = "Can manage billing settings and billing operations without full tenant administration.",
                IsAssignable = true,
                GrantsOwnership = false,
                GrantsBillingAdmin = true
            },
            new TenantRoleDefinitionDto
            {
                Key = Member,
                Name = "Member",
                Description = "Standard tenant member without billing administration privileges.",
                IsAssignable = true,
                GrantsOwnership = false,
                GrantsBillingAdmin = false
            }
        };

        public static IReadOnlyList<TenantRoleDefinitionDto> GetAll() => Roles;

        public static IReadOnlyList<TenantRoleDefinitionDto> GetAssignable()
            => Roles.Where(x => x.IsAssignable).ToArray();

        public static string NormalizeRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentException("Role is required.", nameof(role));

            var normalized = NormalizeRoleKey(role);
            if (!Roles.Any(x => string.Equals(x.Key, normalized, StringComparison.Ordinal)))
                throw new ArgumentException("Role is invalid.", nameof(role));

            return normalized;
        }

        public static string NormalizeAssignableRole(string? role)
        {
            var normalized = NormalizeRole(role);
            if (!Roles.Any(x => x.IsAssignable && string.Equals(x.Key, normalized, StringComparison.Ordinal)))
                throw new ArgumentException("Role is invalid or not assignable.", nameof(role));

            return normalized;
        }

        public static bool GrantsBillingAdmin(string role)
            => Roles.First(x => string.Equals(x.Key, role, StringComparison.Ordinal)).GrantsBillingAdmin;

        public static bool CanManageRoles(string role)
            => string.Equals(role, Owner, StringComparison.Ordinal)
               || string.Equals(role, Admin, StringComparison.Ordinal);

        private static string NormalizeRoleKey(string role)
        {
            var trimmed = role.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            var normalized = trimmed.ToLowerInvariant();
            return normalized switch
            {
                "billing admin" => BillingAdmin,
                "billing-admin" => BillingAdmin,
                "billingadmin" => BillingAdmin,
                "billingAdmin" => BillingAdmin,
                _ => normalized.Replace(' ', '_').Replace('-', '_')
            };
        }
    }
}
