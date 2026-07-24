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
            var builder = new PermissionRegistrationBuilder();

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
