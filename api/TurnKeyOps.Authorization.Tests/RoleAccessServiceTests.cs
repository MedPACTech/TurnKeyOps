using System.Security.Claims;
using MedInsights.Lib;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class RoleAccessServiceTests
{
    [Fact]
    public async Task RemovedMembershipCannotUsePersistedPermissionMapping()
    {
        var context = new TestUserContext();
        var memberships = new Mock<ITenantMembershipRepository>();
        memberships
            .Setup(x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(context.TenantId), context.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership
            {
                TenantId = context.TenantId,
                UserId = context.UserId,
                Role = TenantRoleCatalog.Owner,
                MembershipStatus = "Removed",
                DateRemoved = DateTime.UtcNow,
                IsOwner = true
            });
        var mappings = new Mock<IRolePermissionMappingRepository>();
        var roles = new Mock<IRoleDirectoryService>();
        var service = new RoleAccessService(context, HttpAccessor(context), memberships.Object, mappings.Object, roles.Object);

        var allowed = await service.HasPermissionAsync(TurnKeyPermissionKeys.MembershipManage);

        Assert.False(allowed);
        roles.VerifyNoOtherCalls();
        mappings.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ActiveMembershipUsesOnlyItsResolvedRoleMapping()
    {
        var context = new TestUserContext();
        var roleId = Guid.NewGuid();
        var memberships = new Mock<ITenantMembershipRepository>();
        memberships
            .Setup(x => x.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(context.TenantId), context.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership
            {
                TenantId = context.TenantId,
                UserId = context.UserId,
                Role = TenantRoleCatalog.Staff,
                MembershipStatus = "Active"
            });
        var roles = new Mock<IRoleDirectoryService>();
        roles
            .Setup(x => x.GetRoleAsync(context.TenantId, TenantRoleCatalog.Staff, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantRoleDefinition { Id = roleId, Key = TenantRoleCatalog.Staff, TenantId = null });
        var mappings = new Mock<IRolePermissionMappingRepository>();
        mappings
            .Setup(x => x.GetMappingsForRoleAsync(null, roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RolePermissionMapping { RoleId = roleId, PermissionKey = TurnKeyPermissionKeys.OperationsManage }]);
        var service = new RoleAccessService(context, HttpAccessor(context), memberships.Object, mappings.Object, roles.Object);

        Assert.True(await service.HasPermissionAsync(TurnKeyPermissionKeys.OperationsManage));
        Assert.False(await service.HasPermissionAsync(TurnKeyPermissionKeys.BillingManage));
    }

    private static IHttpContextAccessor HttpAccessor(TestUserContext context)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, context.UserId.ToString()),
                new Claim("tenant_id", context.TenantId.ToString()),
                new Claim(ClaimTypes.Role, TenantRoleCatalog.Staff)
            ],
            "test");
        return new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private sealed class TestUserContext : MedInsights.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public Guid UserId { get; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public MedInsights.Lib.Utils.AppTimeZone Timezone => MedInsights.Lib.Utils.AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
