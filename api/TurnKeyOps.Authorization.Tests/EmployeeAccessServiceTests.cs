using System.Text.Json;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib;
using MedInsights.Lib.Utils;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using Moq;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace MedInsights.Authorization.Tests;
public sealed class EmployeeAccessServiceTests
{
    [Fact] public async Task AccessReadsOnlyCurrentTenantCatalogAndNewRequestsInheritRoleChanges()
    {
        var tenant=Guid.NewGuid();var id=Guid.NewGuid();var partition=EntityKeyPolicy.TenantPartition(tenant);
        var user=new Mock<IUserContext>();user.SetupGet(x=>x.IsAuthenticated).Returns(true);user.SetupGet(x=>x.TenantId).Returns(tenant);user.SetupGet(x=>x.UserId).Returns(id);
        var members=new Mock<ITenantMembershipRepository>();members.Setup(x=>x.GetByUserIdAsync(partition,id,It.IsAny<CancellationToken>())).ReturnsAsync(new TenantMembership{MembershipStatus="Active",Role="staff"});
        var profiles=new Mock<IAzureTablesRepositoryStore<UserProfile>>();profiles.Setup(x=>x.GetByKeysAsync(partition,EntityKeyPolicy.Row(id),It.IsAny<CancellationToken>())).ReturnsAsync(new UserProfile{IsActive=true,EmployeeRoleId="technician"});
        var settings=new Mock<ITenantSettingsRepository>(MockBehavior.Strict);var configuration=EmployeeAccessConfiguration.Default();
        settings.Setup(x=>x.GetAsync(partition,"SETTINGS|EMPLOYEE-ACCESS",It.IsAny<CancellationToken>(),false)).ReturnsAsync(()=>new TenantSettingsDocument{ValuesJson=JsonSerializer.Serialize(configuration)});
        var request=new UserModuleAccessService(members.Object,profiles.Object,user.Object,settings.Object);
        Assert.Contains("inventory.read",await request.GetAsync());Assert.DoesNotContain("invoices.read",await request.GetAsync());
        var roleAccess=new RoleAccessService(user.Object,new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object,members.Object,new Mock<IRolePermissionMappingRepository>(MockBehavior.Strict).Object,new Mock<MedInsights.Services.Interfaces.IRoleDirectoryService>(MockBehavior.Strict).Object,request);
        Assert.True(await roleAccess.HasPermissionAsync("inventory.read"));
        Assert.True(await roleAccess.HasPermissionAsync("jobs.write"));
        Assert.False(await roleAccess.HasPermissionAsync("invoices.read"));
        Assert.False(await roleAccess.HasPermissionAsync("users.write"));
        configuration.Roles[0].ModulePermissions=["jobs.read"];
        var nextRequest=new UserModuleAccessService(members.Object,profiles.Object,user.Object,settings.Object);
        Assert.Equal(["jobs.read"],await nextRequest.GetAsync());
        settings.Verify(x=>x.GetAsync(partition,"SETTINGS|EMPLOYEE-ACCESS",It.IsAny<CancellationToken>(),false),Times.Exactly(2));
        settings.VerifyNoOtherCalls();
    }
}
