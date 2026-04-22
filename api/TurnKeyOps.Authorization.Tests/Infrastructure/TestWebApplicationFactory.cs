using System.Collections.Concurrent;
using System.Net.Http.Headers;
using IBeam.Identity.Api.Authorization;
using MedInsights.Controllers;
using MedInsights.Lib;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace MedInsights.Authorization.Tests.Infrastructure;

public sealed class TestWebApplicationFactory : IDisposable
{
    private readonly Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ConcurrentDictionary<string, TenantMembership> _memberships = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PatientAllergy> _allergies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TenantRoleDefinition> _roles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RolePermissionMapping> _mappings = new(StringComparer.OrdinalIgnoreCase);
    private IHost? _host;

    public Guid TenantId => _tenantId;

    public TestWebApplicationFactory()
    {
        SeedRolesAndMappings();
    }

    public HttpClient CreateClientForRole(string roleKey, Guid? userId = null)
    {
        EnsureHost();

        var actualUserId = userId ?? Guid.NewGuid();
        _memberships[actualUserId.ToString("D")] = new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = actualUserId,
            Role = roleKey,
            IsDeleted = false,
            PartitionKey = EntityKeyPolicy.TenantPartition(_tenantId),
            RowKey = actualUserId.ToString("D")
        };

        var client = _host!.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        client.DefaultRequestHeaders.Add("X-Test-UserId", actualUserId.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Test-TenantId", _tenantId.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Test-Role", roleKey);
        client.DefaultRequestHeaders.Add("X-Test-RoleId", GetRoleId(roleKey));
        return client;
    }

    public void SeedAllergy(PatientAllergy entity)
    {
        _allergies[$"{entity.PartitionKey}|{entity.RowKey}"] = Clone(entity);
    }

    public void Dispose()
    {
        _host?.Dispose();
    }

    private void EnsureHost()
    {
        if (_host is not null)
            return;

        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddHttpContextAccessor();
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                    services.AddAuthorization();
                    services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, RoleIdsAuthorizationPolicyProvider>();
                    services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RequireRoleIdsAuthorizationHandler>();

                    services.AddScoped<IUserContext>(sp =>
                    {
                        var http = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                        return http.HttpContext is null ? UserContext.Anonymous() : UserContext.FromHttp(http);
                    });

                    services.AddScoped<IRoleDirectoryService, RoleDirectoryService>();
                    services.AddScoped<IRoleAccessService, IntegrationTestRoleAccessService>();
                    services.AddScoped<IPatientAllergyService, PatientAllergyService>();

                    services.AddSingleton(CreatePatientAllergyRepository().Object);
                    services.AddSingleton(CreateTenantMembershipRepository().Object);
                    services.AddSingleton(CreateTenantRoleDefinitionRepository().Object);
                    services.AddSingleton(CreateRolePermissionMappingRepository().Object);
                    services.AddSingleton(CreateSystemErrorRepository().Object);

                    services.AddControllers()
                        .AddApplicationPart(typeof(ApiControllerBase).Assembly);
                });

                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.Use(async (context, next) =>
                    {
                        try
                        {
                            await next();
                        }
                        catch (ForbiddenAccessException ex)
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
                        }
                        catch (Exception ex)
                        {
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            await context.Response.WriteAsJsonAsync(new { error = ex.ToString() });
                        }
                    });
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Start();
    }

    private Mock<IPatientAllergyRepository> CreatePatientAllergyRepository()
    {
        var mock = new Mock<IPatientAllergyRepository>();

        mock.Setup(x => x.GetByPatientAsync(It.IsAny<string>()))
            .ReturnsAsync((string partitionKey) => _allergies.Values
                .Where(x => x.PartitionKey == partitionKey && !x.IsDeleted)
                .Select(Clone)
                .ToList());

        mock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((string partitionKey, string rowKey, CancellationToken _, bool includeDeleted) =>
            {
                if (!_allergies.TryGetValue($"{partitionKey}|{rowKey}", out var entity))
                    return null;

                if (!includeDeleted && entity.IsDeleted)
                    return null;

                return Clone(entity);
            });

        mock.Setup(x => x.SaveAsync(It.IsAny<PatientAllergy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientAllergy entity, CancellationToken _) =>
            {
                var clone = Clone(entity);
                _allergies[$"{clone.PartitionKey}|{clone.RowKey}"] = clone;
                return Clone(clone);
            });

        return mock;
    }

    private Mock<ITenantMembershipRepository> CreateTenantMembershipRepository()
    {
        var mock = new Mock<ITenantMembershipRepository>();

        mock.Setup(x => x.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Guid userId, CancellationToken _) =>
            {
                _memberships.TryGetValue(userId.ToString("D"), out var membership);
                return membership;
            });

        return mock;
    }

    private Mock<ITenantRoleDefinitionRepository> CreateTenantRoleDefinitionRepository()
    {
        var mock = new Mock<ITenantRoleDefinitionRepository>();

        mock.Setup(x => x.GetSystemByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
                _roles.Values.FirstOrDefault(x => x.TenantId is null && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted));

        mock.Setup(x => x.GetTenantByKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, string key, CancellationToken _) =>
                _roles.Values.FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted));

        mock.Setup(x => x.GetSystemRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => _roles.Values.Where(x => x.TenantId is null && !x.IsDeleted).Select(Clone).ToList());

        mock.Setup(x => x.GetTenantRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => _roles.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).Select(Clone).ToList());

        mock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((string partitionKey, string rowKey, CancellationToken _, bool includeDeleted) =>
            {
                var role = _roles.Values.FirstOrDefault(x => x.PartitionKey == partitionKey && x.RowKey == rowKey);
                if (role is null || (!includeDeleted && role.IsDeleted))
                    return null;
                return Clone(role);
            });

        mock.Setup(x => x.SaveAsync(It.IsAny<TenantRoleDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantRoleDefinition entity, CancellationToken _) =>
            {
                var clone = Clone(entity);
                _roles[clone.Id.ToString("D")] = clone;
                return Clone(clone);
            });

        return mock;
    }

    private Mock<IRolePermissionMappingRepository> CreateRolePermissionMappingRepository()
    {
        var mock = new Mock<IRolePermissionMappingRepository>();

        mock.Setup(x => x.GetMappingsForRoleAsync(It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? tenantId, Guid roleId, CancellationToken _) => _mappings.Values
                .Where(x => x.TenantId == tenantId && x.RoleId == roleId && !x.IsDeleted)
                .Select(Clone)
                .ToList());

        mock.Setup(x => x.GetSystemMappingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => _mappings.Values.Where(x => x.TenantId is null && !x.IsDeleted).Select(Clone).ToList());

        mock.Setup(x => x.GetTenantMappingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => _mappings.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).Select(Clone).ToList());

        mock.Setup(x => x.SaveAsync(It.IsAny<RolePermissionMapping>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RolePermissionMapping entity, CancellationToken _) =>
            {
                var clone = Clone(entity);
                _mappings[clone.Id.ToString("D")] = clone;
                return Clone(clone);
            });

        return mock;
    }

    private static Mock<ISystemErrorRepository> CreateSystemErrorRepository()
    {
        var mock = new Mock<ISystemErrorRepository>();
        mock.Setup(x => x.SaveAsync(It.IsAny<SystemError>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private void SeedRolesAndMappings()
    {
        AddRole(SystemRoleIds.Owner, TenantRoleCatalog.Owner);
        AddRole(SystemRoleIds.Admin, TenantRoleCatalog.Admin);
        AddRole(SystemRoleIds.BillingAdmin, TenantRoleCatalog.BillingAdmin);
        AddRole(SystemRoleIds.Member, TenantRoleCatalog.Member);

        MapSystemPermission(SystemRoleIds.Owner, TenantRoleCatalog.Owner,
            PatientAllergyAuthorizationKeys.Read,
            PatientAllergyAuthorizationKeys.Save,
            PatientAllergyAuthorizationKeys.Delete,
            PatientAllergyAuthorizationKeys.CascadeSevere);
        MapSystemPermission(SystemRoleIds.Admin, TenantRoleCatalog.Admin,
            PatientAllergyAuthorizationKeys.Read,
            PatientAllergyAuthorizationKeys.Save,
            PatientAllergyAuthorizationKeys.Delete,
            PatientAllergyAuthorizationKeys.CascadeSevere);
        MapSystemPermission(SystemRoleIds.BillingAdmin, TenantRoleCatalog.BillingAdmin,
            PatientAllergyAuthorizationKeys.Read,
            PatientAllergyAuthorizationKeys.Save);
        MapSystemPermission(SystemRoleIds.Member, TenantRoleCatalog.Member,
            PatientAllergyAuthorizationKeys.Read);
    }

    private void AddRole(string roleId, string roleKey)
    {
        var id = Guid.Parse(roleId);
        _roles[id.ToString("D")] = new TenantRoleDefinition
        {
            Id = id,
            TenantId = null,
            PartitionKey = "ROLEDEF|SYSTEM",
            RowKey = EntityKeyPolicy.Row(id),
            Key = roleKey,
            Name = roleKey,
            IsSystem = true,
            IsAssignable = true
        };
    }

    private void MapSystemPermission(string roleId, string roleKey, params string[] permissionKeys)
    {
        foreach (var permissionKey in permissionKeys)
        {
            var mappingId = Guid.NewGuid();
            _mappings[mappingId.ToString("D")] = new RolePermissionMapping
            {
                Id = mappingId,
                TenantId = null,
                PartitionKey = "ROLEPERM|SYSTEM",
                RowKey = $"ROLE={Guid.Parse(roleId):N}|PERM={permissionKey}",
                RoleId = Guid.Parse(roleId),
                RoleKey = roleKey,
                PermissionKey = permissionKey,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow
            };
        }
    }

    private static string GetRoleId(string roleKey) => roleKey switch
    {
        var key when string.Equals(key, TenantRoleCatalog.Owner, StringComparison.OrdinalIgnoreCase) => SystemRoleIds.Owner,
        var key when string.Equals(key, TenantRoleCatalog.Admin, StringComparison.OrdinalIgnoreCase) => SystemRoleIds.Admin,
        var key when string.Equals(key, TenantRoleCatalog.BillingAdmin, StringComparison.OrdinalIgnoreCase) => SystemRoleIds.BillingAdmin,
        _ => SystemRoleIds.Member
    };

    private static PatientAllergy Clone(PatientAllergy entity) => new()
    {
        Id = entity.Id,
        PartitionKey = entity.PartitionKey,
        RowKey = entity.RowKey,
        PatientId = entity.PatientId,
        AllergyType = entity.AllergyType,
        Severity = entity.Severity,
        Description = entity.Description,
        Reaction = entity.Reaction,
        DateNoted = entity.DateNoted,
        IsDeleted = entity.IsDeleted,
        Timestamp = entity.Timestamp,
        ETag = entity.ETag
    };

    private static TenantRoleDefinition Clone(TenantRoleDefinition entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        PartitionKey = entity.PartitionKey,
        RowKey = entity.RowKey,
        Key = entity.Key,
        Name = entity.Name,
        Description = entity.Description,
        IsSystem = entity.IsSystem,
        IsAssignable = entity.IsAssignable,
        GrantsOwnership = entity.GrantsOwnership,
        GrantsBillingAdmin = entity.GrantsBillingAdmin,
        IsDeleted = entity.IsDeleted,
        DateCreated = entity.DateCreated,
        DateUpdated = entity.DateUpdated,
        Timestamp = entity.Timestamp,
        ETag = entity.ETag
    };

    private static RolePermissionMapping Clone(RolePermissionMapping entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        PartitionKey = entity.PartitionKey,
        RowKey = entity.RowKey,
        RoleId = entity.RoleId,
        RoleKey = entity.RoleKey,
        PermissionId = entity.PermissionId,
        PermissionKey = entity.PermissionKey,
        IsDeleted = entity.IsDeleted,
        DateCreated = entity.DateCreated,
        DateUpdated = entity.DateUpdated,
        Timestamp = entity.Timestamp,
        ETag = entity.ETag
    };
}
