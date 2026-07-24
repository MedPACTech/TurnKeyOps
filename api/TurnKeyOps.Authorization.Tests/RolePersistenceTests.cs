using MedInsights.Authorization.Tests.Infrastructure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class RolePersistenceTests
{
    private const string EstimateReadPermission = "estimates.read";
    private const string EstimateSavePermission = "estimates.save";

    [Fact]
    public async Task TenantRoleDefinitionsAndMappingsPersistAcrossServiceInstances()
    {
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userContext = new TestUserContext { TenantId = tenantId, UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") };
        var roleStore = new Dictionary<string, TenantRoleDefinition>(StringComparer.OrdinalIgnoreCase);
        var mappingStore = new Dictionary<string, RolePermissionMapping>(StringComparer.OrdinalIgnoreCase);

        var roleRepository = CreateRoleRepository(roleStore);
        var mappingRepository = CreateMappingRepository(mappingStore);
        var catalog = new TestRolePermissionCatalog(
        [
            new PermissionDefinitionOption { Key = EstimateReadPermission, Name = "Read estimates" },
            new PermissionDefinitionOption { Key = EstimateSavePermission, Name = "Save estimates" }
        ]);

        var membershipAuthorization = new Mock<ITenantMembershipAuthorizationService>();
        membershipAuthorization.Setup(x => x.RequireMembershipManagementAccessAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var serviceA = new TenantRoleDefinitionService(
            roleRepository.Object,
            mappingRepository.Object,
            catalog,
            userContext,
            membershipAuthorization.Object);

        var created = await serviceA.CreateAsync(new UpsertTenantRoleRequestDto
        {
            Key = "estimate_reviewer",
            Name = "Estimate Reviewer",
            Description = "Reviews estimate changes",
            IsAssignable = true,
            PermissionKeys = [EstimateReadPermission, EstimateSavePermission]
        });

        var serviceB = new TenantRoleDefinitionService(
            roleRepository.Object,
            mappingRepository.Object,
            catalog,
            userContext,
            membershipAuthorization.Object);

        var roles = await serviceB.GetAllAsync();
        var reloaded = Assert.Single(roles, x => x.Id == created.Id);

        Assert.Equal("estimate_reviewer", reloaded.Key);
        Assert.Equal(2, reloaded.Permissions.Count);
        Assert.Contains(reloaded.Permissions, x => x.Key == EstimateReadPermission);
        Assert.Contains(reloaded.Permissions, x => x.Key == EstimateSavePermission);
    }

    private static Mock<ITenantRoleDefinitionRepository> CreateRoleRepository(IDictionary<string, TenantRoleDefinition> store)
    {
        var mock = new Mock<ITenantRoleDefinitionRepository>();

        mock.Setup(x => x.GetSystemByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, CancellationToken _) => null);

        mock.Setup(x => x.GetTenantByKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, string key, CancellationToken _) => store.Values
                .FirstOrDefault(x => x.TenantId == tenantId && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted));

        mock.Setup(x => x.GetSystemRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        mock.Setup(x => x.GetTenantRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => store.Values
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                .Select(Clone)
                .ToList());

        mock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((string partitionKey, string rowKey, CancellationToken _, bool includeDeleted) =>
            {
                var entity = store.Values.FirstOrDefault(x => x.PartitionKey == partitionKey && x.RowKey == rowKey);
                if (entity is null || (!includeDeleted && entity.IsDeleted))
                    return null;
                return Clone(entity);
            });

        mock.Setup(x => x.SaveAsync(It.IsAny<TenantRoleDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantRoleDefinition entity, CancellationToken _) =>
            {
                var clone = Clone(entity);
                store[clone.Id.ToString("D")] = clone;
                return Clone(clone);
            });

        return mock;
    }

    private static Mock<IRolePermissionMappingRepository> CreateMappingRepository(IDictionary<string, RolePermissionMapping> store)
    {
        var mock = new Mock<IRolePermissionMappingRepository>();

        mock.Setup(x => x.GetMappingsForRoleAsync(It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? tenantId, Guid roleId, CancellationToken _) => store.Values
                .Where(x => x.TenantId == tenantId && x.RoleId == roleId && !x.IsDeleted)
                .Select(Clone)
                .ToList());

        mock.Setup(x => x.GetSystemMappingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        mock.Setup(x => x.GetTenantMappingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => store.Values
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                .Select(Clone)
                .ToList());

        mock.Setup(x => x.SaveAsync(It.IsAny<RolePermissionMapping>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RolePermissionMapping entity, CancellationToken _) =>
            {
                var clone = Clone(entity);
                store[clone.Id.ToString("D")] = clone;
                return Clone(clone);
            });

        return mock;
    }

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
        DateUpdated = entity.DateUpdated
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
        DateUpdated = entity.DateUpdated
    };
}
