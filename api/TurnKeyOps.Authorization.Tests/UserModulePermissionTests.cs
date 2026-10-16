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
        Assert.Equal(UserModulePermissions.Modules.Length * 2 + UserModulePermissions.PayrollActions.Length + UserModulePermissions.EstimateActions.Length, resolved.Length);
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
        Assert.True(UserModulePermissions.Allows(["bob.read","bob.write"],"bob",false));
    }
    [Fact] public void TechnicianGetsExactlyItsRoleModulesAndCannotManageEstimates() {
        var grants = UserModulePermissions.Resolve(Member("staff"), new() { IsActive=true, EmployeeRoleId="technician" }, EmployeeAccessConfiguration.Default());
        foreach(var module in new[]{"bob","calendar","jobs","inventory","estimates","contacts"}) Assert.True(UserModulePermissions.Allows(grants,module,true));
        foreach(var module in new[]{"purchasing","leads","requests","invoices","finance","payroll","users","settings","billing","dashboard"}) Assert.False(UserModulePermissions.Allows(grants,module,false));
        Assert.Contains("estimates.submit",grants); Assert.DoesNotContain("estimates.manage",grants);
        Assert.True(UserModuleAccessFilter.AllowsEstimateSubmission("EstimateWorkspace","Submit",grants));
        foreach(var action in new[]{"Issue","Deliver","Approve","Void","Job","Revise","ApproveBob"}) Assert.False(UserModuleAccessFilter.AllowsEstimateSubmission("EstimateWorkspace",action,grants));
    }
    [Fact] public void UpdatedRoleIsInheritedAndUnknownRoleFailsClosed() {
        var profile = new UserProfile { IsActive=true,EmployeeRoleId="technician" };
        var config=EmployeeAccessConfiguration.Default();
        config.Roles[0].ModulePermissions=["jobs.read"];
        Assert.Equal(["jobs.read"],UserModulePermissions.Resolve(Member("staff"),profile,config));
        config.Roles=[]; Assert.Empty(UserModulePermissions.Resolve(Member("staff"),profile,config));
        Assert.NotEmpty(UserModulePermissions.Resolve(Member("owner"),profile,config));
    }
    [Fact] public void RoleDoesNotBypassMembershipCeilingOrExplicitUserRestrictions() {
        var config=EmployeeAccessConfiguration.Default();config.Roles[0].ModulePermissions=["users.read","users.write","jobs.read","jobs.write"];
        var profile=new UserProfile {IsActive=true,EmployeeRoleId="technician",ModulePermissions=["users.read","users.write","jobs.read"]};
        Assert.Equal(["jobs.read"],UserModulePermissions.Resolve(Member("staff"),profile,config));
    }
    [Fact] public void LegacyEstimateWriteOverridesRetainManagementWhileAssignedTechniciansCannotElevate() {
        var p=new UserProfile{IsActive=true,ModulePermissions=["estimates.read","estimates.write"]};
        Assert.Contains("estimates.manage",UserModulePermissions.Resolve(Member("staff"),p));
        p.EmployeeRoleId="technician";
        Assert.DoesNotContain("estimates.manage",UserModulePermissions.Resolve(Member("staff"),p));
    }
    [Fact] public void RoleCatalogRejectsDuplicateNamesAndUnknownGrants() {
        var config=EmployeeAccessConfiguration.Default();config.Roles=[config.Roles[0],new(){Id="other",Name="Technician"}];
        Assert.Throws<ArgumentException>(()=>config.Validate());
        config=EmployeeAccessConfiguration.Default();config.Roles[0].ModulePermissions=["superuser.write"];
        Assert.Throws<ArgumentException>(()=>config.Validate());
        config=EmployeeAccessConfiguration.Default();config.Teams=["Field","field"];
        Assert.Throws<ArgumentException>(()=>config.Validate());
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
    [InlineData("Leads","leads")] [InlineData("Contacts","contacts")] [InlineData("People","users")] [InlineData("Jobs","jobs")] [InlineData("Invoices","invoices")]
    [InlineData("Locksmith","jobs")] [InlineData("LocksmithFieldInvoices","invoices")]
    [InlineData("BobActions","bob")] [InlineData("TenantMembership","users")]
    public void SensitiveEndpointsHaveModuleEnforcement(string controller,string module) => Assert.Equal(module,UserModuleAccessFilter.ModuleFor(controller));
}
