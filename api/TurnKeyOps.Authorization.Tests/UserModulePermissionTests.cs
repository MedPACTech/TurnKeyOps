using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Dtos;
using MedInsights.Services;
using MedInsights.API.Infrastructure;

namespace MedInsights.Authorization.Tests;
public sealed class UserModulePermissionTests
{
    private static TenantMembership Member(string role = "admin") => new() { Role = role, MembershipStatus = "Active" };
    [Fact] public void LegacyOwnersKeepFullAccess() {
        var m = Member("owner"); m.IsOwner = true;
        var resolved = UserModulePermissions.Resolve(m, new() { ModulePermissions = [], IsActive = false });
        Assert.True(UserModulePermissions.Allows(resolved,"users",true));
        Assert.Equal(UserModulePermissions.Modules.Length * 2, resolved.Length);
    }
    [Fact] public void ExplicitEmptyOverrideIsNotRoleDefaults() {
        Assert.NotEmpty(UserModulePermissions.Resolve(Member(), null));
        Assert.Empty(UserModulePermissions.Resolve(Member(),new() { IsActive=true, ModulePermissions=[] }));
    }
    [Fact] public void ViewOnlyCannotWrite() {
        var permissions = UserModulePermissions.Resolve(Member(),new() { IsActive=true, ModulePermissions=["jobs.read"] });
        Assert.True(UserModulePermissions.Allows(permissions,"jobs",false));
        Assert.False(UserModulePermissions.Allows(permissions,"jobs",true));
        Assert.False(UserModulePermissions.Allows(permissions,"invoices",false));
    }
    [Theory] [InlineData("contact")] [InlineData("staff")] [InlineData("member")]
    public void OverridesCannotElevateRole(string role) {
        var permissions=UserModulePermissions.Resolve(Member(role),new() { IsActive=true, ModulePermissions=["users.read","users.write"] });
        Assert.False(UserModulePermissions.Allows(permissions,"users",true));
    }
    [Fact] public void RemovedMembershipFailsClosedEvenWithOwnerFlag() {
        var m=Member("owner");m.IsOwner=true;m.DateRemoved=DateTime.UtcNow;
        Assert.Empty(UserModulePermissions.Resolve(m,null));
    }
    [Fact] public void ArchivedProfileDeniesNonOwner() {
        Assert.Empty(UserModulePermissions.Resolve(Member(),new() {IsActive=false}));
    }
    [Fact] public void ReadOnlyAggregateCannotLeakOtherModules() {
        Assert.False(UserModulePermissions.Allows(["dashboard.read"],"dashboard",false));
        Assert.False(UserModulePermissions.Allows(["bob.read","bob.write"],"bob",false));
    }
    [Fact] public void InvalidPermissionOrWriteWithoutReadRejected() {
        Assert.Throws<ArgumentException>(()=>UserModulePermissions.Validate(["root.write"]));
        Assert.Throws<ArgumentException>(()=>UserModulePermissions.Validate(["jobs.write"]));
    }
    [Fact] public void MultipleProfilesAreIndependentOfAccessRoles() {
        ManagedPeopleService.Validate(new() {FirstName="Alex",ProfileTypes=["employee","vendor","customer"]});
        Assert.Throws<ArgumentException>(()=>ManagedPeopleService.Validate(new() {FirstName="Alex",ProfileTypes=["owner"]}));
    }
    [Fact] public void EveryTenantControllerHasAnExplicitModuleMapping() {
        var policies = new[] {TurnKeyAuthorizationPolicies.TenantStaff,TurnKeyAuthorizationPolicies.TenantAdmin,TurnKeyAuthorizationPolicies.BillingAdmin};
        var missing=typeof(MedInsights.Controllers.PeopleController).Assembly.GetTypes()
            .Where(t=>!t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .Where(t=>t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any(a=>policies.Contains(a.Policy)))
            .Select(t=>t.Name.Replace("Controller",""))
            .Where(name=>name!="UserProfile" && UserModuleAccessFilter.ModuleFor(name) is null);
        Assert.Empty(missing);
    }
    [Theory]
    [InlineData("People","users")] [InlineData("Jobs","jobs")] [InlineData("Invoices","invoices")]
    [InlineData("BobActions","bob")] [InlineData("TenantMembership","users")]
    public void SensitiveEndpointsHaveModuleEnforcement(string controller,string module) => Assert.Equal(module,UserModuleAccessFilter.ModuleFor(controller));
}
