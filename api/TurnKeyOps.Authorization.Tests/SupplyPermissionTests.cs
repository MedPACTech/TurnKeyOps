using MedInsights.API.DependencyInjection;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace TurnKeyOps.Authorization.Tests;
public sealed class SupplyPermissionTests
{
    [Fact]public void SupplyModulesAreIndependentAndStaffDefaultsDoNotGainPurchasing()
    {
        var membership=new TenantMembership{Role="staff",MembershipStatus="Active"};
        Assert.DoesNotContain("purchasing.read",UserModulePermissions.Resolve(membership,null));
        var field=new UserProfile{IsActive=true,ModulePermissions=["inventory.read","jobs.read"]};var grants=UserModulePermissions.Resolve(membership,field);
        Assert.True(UserModulePermissions.Allows(grants,"inventory",false));Assert.False(UserModulePermissions.Allows(grants,"inventory",true));Assert.False(UserModulePermissions.Allows(grants,"purchasing",false));
        var warehouse=new UserProfile{IsActive=true,ModulePermissions=["inventory.read","inventory.write","purchasing.read"]};grants=UserModulePermissions.Resolve(membership,warehouse);
        Assert.True(UserModulePermissions.Allows(grants,"inventory",true));Assert.False(UserModulePermissions.Allows(grants,"purchasing",true));
    }
    [Fact]public void StartupRegistersAllSupplyPermissions()
    {
        var services=new ServiceCollection();services.AddLogging();services.AddRolePermissionAuthorization(new ConfigurationBuilder().Build());using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();var catalog=scope.ServiceProvider.GetRequiredService<IRolePermissionCatalog>();
        foreach(var permission in new[]{"inventory.read","inventory.write","purchasing.read","purchasing.write"})Assert.Contains(catalog.GetPermissions(),p=>p.Key==permission);
        foreach(var role in new[]{"staff","member","contact"})Assert.DoesNotContain(catalog.GetRoleMappings().Where(r=>r.RoleKey==role).SelectMany(r=>r.PermissionKeys),p=>p.StartsWith("purchasing."));
    }
}
