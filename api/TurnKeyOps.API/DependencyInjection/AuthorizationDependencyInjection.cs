using MedInsights.Lib.Authorization;
using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using MedInsights.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace MedInsights.API.DependencyInjection
{
    public static class AuthorizationDependencyInjection
    {
        public static IServiceCollection AddRolePermissionAuthorization(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<PermissionRegistrationBuilder>? configure = null)
        {
            services.Configure<MedInsights.Lib.Configurations.AuthorizationOptions>(configuration.GetSection("Authorization"));
            var builder = new PermissionRegistrationBuilder();

            RegisterTurnKeyPermissions(builder);

            configure?.Invoke(builder);

            services.AddSingleton(builder);
            services.AddScoped<IRoleDirectoryService, RoleDirectoryService>();
            services.AddScoped<IRolePermissionCatalog, RolePermissionCatalog>();
            services.AddScoped<IRoleAccessService, RoleAccessService>();
            services.AddScoped<ITenantRoleDefinitionService, TenantRoleDefinitionService>();
            services.AddScoped<IStartupSeedContributor, AuthorizationSeedContributor>();
            services.AddTransient<IClaimsTransformation, TenantRoleClaimsTransformation>();

            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                options.AddPolicy(TurnKeyAuthorizationPolicies.TenantAccess, policy =>
                    policy.RequireAuthenticatedUser().RequireRole(
                        TurnKeyAuthorizationRoles.Owner,
                        TurnKeyAuthorizationRoles.Admin,
                        TurnKeyAuthorizationRoles.BillingAdmin,
                        TurnKeyAuthorizationRoles.Member,
                        TurnKeyAuthorizationRoles.Staff,
                        TurnKeyAuthorizationRoles.Contact));

                options.AddPolicy(TurnKeyAuthorizationPolicies.TenantStaff, policy =>
                    policy.RequireAuthenticatedUser().RequireRole(
                        TurnKeyAuthorizationRoles.Owner,
                        TurnKeyAuthorizationRoles.Admin,
                        TurnKeyAuthorizationRoles.Member,
                        TurnKeyAuthorizationRoles.Staff));

                options.AddPolicy(TurnKeyAuthorizationPolicies.TenantAdmin, policy =>
                    policy.RequireAuthenticatedUser().RequireRole(
                        TurnKeyAuthorizationRoles.Owner,
                        TurnKeyAuthorizationRoles.Admin));

                options.AddPolicy(TurnKeyAuthorizationPolicies.BillingAdmin, policy =>
                    policy.RequireAuthenticatedUser().RequireRole(
                        TurnKeyAuthorizationRoles.Owner,
                        TurnKeyAuthorizationRoles.Admin,
                        TurnKeyAuthorizationRoles.BillingAdmin));

                options.AddPolicy(TurnKeyAuthorizationPolicies.InternalAdmin, policy =>
                    policy.RequireAuthenticatedUser().RequireRole(TurnKeyAuthorizationRoles.InternalAdmin));

                // Compatibility for controllers/packages that still reference the legacy name.
                options.AddPolicy("RequireAdmin", policy =>
                    policy.RequireAuthenticatedUser().RequireRole(
                        TurnKeyAuthorizationRoles.Owner,
                        TurnKeyAuthorizationRoles.Admin));
            });

            return services;
        }

        private static void RegisterTurnKeyPermissions(PermissionRegistrationBuilder builder)
        {
            builder
                .AddPermission(TurnKeyPermissionKeys.TenantRead, name: "Read tenant data")
                .AddPermission(TurnKeyPermissionKeys.TenantManage, name: "Manage tenant settings")
                .AddPermission(TurnKeyPermissionKeys.OperationsRead, name: "Read operations")
                .AddPermission(TurnKeyPermissionKeys.OperationsManage, name: "Manage operations")
                .AddPermission(TurnKeyPermissionKeys.EstimateDefaultsRead, name: "Read estimate defaults")
                .AddPermission(TurnKeyPermissionKeys.EstimateDefaultsManage, name: "Manage estimate defaults")
                .AddPermission(TurnKeyPermissionKeys.TenantSettingsRead, name: "Read tenant settings")
                .AddPermission(TurnKeyPermissionKeys.TenantSettingsManage, name: "Manage tenant settings")
                .AddPermission(TurnKeyPermissionKeys.BillingRead, name: "Read billing")
                .AddPermission(TurnKeyPermissionKeys.BillingManage, name: "Manage billing")
                .AddPermission(TurnKeyPermissionKeys.MembershipManage, name: "Manage membership")
                .AddPermission(TurnKeyPermissionKeys.MembershipOwnerGrant, name: "Grant tenant ownership");

            builder.MapRole(TenantRoleCatalog.Owner,
                TurnKeyPermissionKeys.TenantRead,
                TurnKeyPermissionKeys.TenantManage,
                TurnKeyPermissionKeys.OperationsRead,
                TurnKeyPermissionKeys.OperationsManage,
                TurnKeyPermissionKeys.EstimateDefaultsRead,
                TurnKeyPermissionKeys.EstimateDefaultsManage,
                TurnKeyPermissionKeys.TenantSettingsRead,
                TurnKeyPermissionKeys.TenantSettingsManage,
                TurnKeyPermissionKeys.BillingRead,
                TurnKeyPermissionKeys.BillingManage,
                TurnKeyPermissionKeys.MembershipManage,
                TurnKeyPermissionKeys.MembershipOwnerGrant);
            builder.MapRole(TenantRoleCatalog.Admin,
                TurnKeyPermissionKeys.TenantRead,
                TurnKeyPermissionKeys.TenantManage,
                TurnKeyPermissionKeys.OperationsRead,
                TurnKeyPermissionKeys.OperationsManage,
                TurnKeyPermissionKeys.EstimateDefaultsRead,
                TurnKeyPermissionKeys.EstimateDefaultsManage,
                TurnKeyPermissionKeys.TenantSettingsRead,
                TurnKeyPermissionKeys.TenantSettingsManage,
                TurnKeyPermissionKeys.BillingRead,
                TurnKeyPermissionKeys.BillingManage,
                TurnKeyPermissionKeys.MembershipManage);
            builder.MapRole(TenantRoleCatalog.BillingAdmin,
                TurnKeyPermissionKeys.TenantRead,
                TurnKeyPermissionKeys.BillingRead,
                TurnKeyPermissionKeys.BillingManage);
            builder.MapRole(TenantRoleCatalog.Member,
                TurnKeyPermissionKeys.TenantRead,
                TurnKeyPermissionKeys.OperationsRead,
                TurnKeyPermissionKeys.OperationsManage,
                TurnKeyPermissionKeys.EstimateDefaultsRead,
                TurnKeyPermissionKeys.TenantSettingsRead);
            builder.MapRole(TenantRoleCatalog.Staff,
                TurnKeyPermissionKeys.TenantRead,
                TurnKeyPermissionKeys.OperationsRead,
                TurnKeyPermissionKeys.OperationsManage,
                TurnKeyPermissionKeys.EstimateDefaultsRead,
                TurnKeyPermissionKeys.TenantSettingsRead);
            builder.MapRole(TenantRoleCatalog.Contact, TurnKeyPermissionKeys.TenantRead);
        }
    }
}
