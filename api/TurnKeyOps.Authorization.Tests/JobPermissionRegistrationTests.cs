using MedInsights.API.DependencyInjection;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace TurnKeyOps.Authorization.Tests;
public sealed class JobPermissionRegistrationTests
{
    [Fact] public void StartupCatalogProvidesReadPermissionsForExistingJobManagers()
    {
        var services=new ServiceCollection();services.AddLogging();
        services.AddRolePermissionAuthorization(new ConfigurationBuilder().Build());
        using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
        var catalog=scope.ServiceProvider.GetRequiredService<IRolePermissionCatalog>();
        foreach(var permission in new[]{"jobs.read","jobs.write","calendar.read","calendar.write"})Assert.Contains(catalog.GetPermissions(),p=>p.Key==permission);
        foreach(var role in new[]{"owner","admin"}){
            var mapping=Assert.Single(catalog.GetRoleMappings().Where(r=>r.RoleKey==role));
            Assert.Contains("jobs.read",mapping.PermissionKeys);Assert.Contains("calendar.read",mapping.PermissionKeys);
        }
        foreach(var role in new[]{"contact","billing_admin"})Assert.DoesNotContain(catalog.GetRoleMappings().Where(r=>r.RoleKey==role).SelectMany(r=>r.PermissionKeys),p=>p is "jobs.read" or "jobs.write");
    }
}
