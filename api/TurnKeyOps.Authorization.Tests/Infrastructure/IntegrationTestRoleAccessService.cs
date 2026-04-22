using System.Security.Claims;
using MedInsights.Lib;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Authorization.Tests.Infrastructure;

internal sealed class IntegrationTestRoleAccessService : IRoleAccessService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IntegrationTestRoleAccessService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task RequirePermissionAsync(string permissionKey, CancellationToken ct = default)
    {
        if (!HasPermission(permissionKey))
            throw new ForbiddenAccessException($"Current user does not have permission '{permissionKey}'.");

        return Task.CompletedTask;
    }

    public Task RequireAnyRoleAsync(IEnumerable<string> roleKeys, CancellationToken ct = default)
    {
        if (!roleKeys.Contains(GetRole(), StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("Current user does not have the required role.");

        return Task.CompletedTask;
    }

    public Task RequireAnyRoleIdAsync(IEnumerable<string> roleIds, CancellationToken ct = default)
    {
        if (!roleIds.Contains(GetRoleId(), StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("Current user does not have the required role id.");

        return Task.CompletedTask;
    }

    public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken ct = default)
        => Task.FromResult(HasPermission(permissionKey));

    public Task<bool> HasAnyRoleAsync(IEnumerable<string> roleKeys, CancellationToken ct = default)
        => Task.FromResult(roleKeys.Contains(GetRole(), StringComparer.OrdinalIgnoreCase));

    public Task<bool> HasAnyRoleIdAsync(IEnumerable<string> roleIds, CancellationToken ct = default)
        => Task.FromResult(roleIds.Contains(GetRoleId(), StringComparer.OrdinalIgnoreCase));

    public Task EnforceDeclaredAccessAsync(Type targetType, string methodName, CancellationToken ct = default)
    {
        var role = GetRole();

        var allowed = methodName switch
        {
            nameof(MedInsights.Services.PatientAllergyService.GetAsync) or nameof(MedInsights.Services.PatientAllergyService.GetByPatientAsync)
                => new[] { TenantRoleCatalog.Member, TenantRoleCatalog.BillingAdmin, TenantRoleCatalog.Admin, TenantRoleCatalog.Owner },
            nameof(MedInsights.Services.PatientAllergyService.AddAsync) or nameof(MedInsights.Services.PatientAllergyService.UpdateAsync)
                => new[] { TenantRoleCatalog.BillingAdmin, TenantRoleCatalog.Admin, TenantRoleCatalog.Owner },
            nameof(MedInsights.Services.PatientAllergyService.DeleteAsync)
                => new[] { TenantRoleCatalog.Admin, TenantRoleCatalog.Owner },
            _ => Array.Empty<string>()
        };

        if (allowed.Length > 0 && !allowed.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("Current user does not have the required role.");

        return Task.CompletedTask;
    }

    private bool HasPermission(string permissionKey)
    {
        var role = GetRole();
        return role switch
        {
            var r when string.Equals(r, TenantRoleCatalog.Owner, StringComparison.OrdinalIgnoreCase) => true,
            var r when string.Equals(r, TenantRoleCatalog.Admin, StringComparison.OrdinalIgnoreCase) => true,
            var r when string.Equals(r, TenantRoleCatalog.BillingAdmin, StringComparison.OrdinalIgnoreCase)
                => permissionKey is PatientAllergyAuthorizationKeys.Read or PatientAllergyAuthorizationKeys.Save,
            var r when string.Equals(r, TenantRoleCatalog.Member, StringComparison.OrdinalIgnoreCase)
                => permissionKey == PatientAllergyAuthorizationKeys.Read,
            _ => false
        };
    }

    private string GetRole()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role)
            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("role")
            ?? string.Empty;

    private string GetRoleId()
        => _httpContextAccessor.HttpContext?.User.FindFirstValue("rid")
            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("role_id")
            ?? string.Empty;
}
