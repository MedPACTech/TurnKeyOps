using MedInsights.Lib.Authorization;
using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using MedInsights.API.Infrastructure;

namespace MedInsights.API.DependencyInjection
{
    public static class AuthorizationDependencyInjection
    {
        public static IServiceCollection AddRolePermissionAuthorization(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<PermissionRegistrationBuilder>? configure = null)
        {
            services.Configure<AuthorizationOptions>(configuration.GetSection("Authorization"));
            var builder = new PermissionRegistrationBuilder()
                .AddPermission(PatientAllergyAuthorizationKeys.Read, name: "Read allergies")
                .AddPermission(PatientAllergyAuthorizationKeys.Save, name: "Save allergies")
                .AddPermission(PatientAllergyAuthorizationKeys.Delete, name: "Delete allergies")
                .AddPermission(PatientAllergyAuthorizationKeys.CascadeSevere, name: "Cascade severe allergy changes")
                .MapRole(TenantRoleCatalog.Owner,
                    PatientAllergyAuthorizationKeys.Read,
                    PatientAllergyAuthorizationKeys.Save,
                    PatientAllergyAuthorizationKeys.Delete,
                    PatientAllergyAuthorizationKeys.CascadeSevere)
                .MapRole(TenantRoleCatalog.Admin,
                    PatientAllergyAuthorizationKeys.Read,
                    PatientAllergyAuthorizationKeys.Save,
                    PatientAllergyAuthorizationKeys.Delete,
                    PatientAllergyAuthorizationKeys.CascadeSevere)
                .MapRole(TenantRoleCatalog.BillingAdmin,
                    PatientAllergyAuthorizationKeys.Read,
                    PatientAllergyAuthorizationKeys.Save)
                .MapRole(TenantRoleCatalog.Member,
                    PatientAllergyAuthorizationKeys.Read);

            configure?.Invoke(builder);

            services.AddSingleton(builder);
            services.AddScoped<IRoleDirectoryService, RoleDirectoryService>();
            services.AddScoped<IRolePermissionCatalog, RolePermissionCatalog>();
            services.AddScoped<IRoleAccessService, RoleAccessService>();
            services.AddScoped<ITenantRoleDefinitionService, TenantRoleDefinitionService>();
            services.AddScoped<IStartupSeedContributor, AuthorizationSeedContributor>();
            services.AddTransient<IClaimsTransformation, TenantRoleClaimsTransformation>();

            return services;
        }
    }
}
