using System.Text.Json;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Repositories.Interfaces;
namespace MedInsights.Authorization.Tests;

public sealed class BobActionGuardTests
{
    static readonly Guid Tenant=Guid.NewGuid(),Actor=Guid.NewGuid();
    [Theory][InlineData("bob.read")][InlineData("bob.write")][InlineData("leads.read")][InlineData("leads.write")]
    public async Task BobAndTargetReadWriteGrantsAreAllRequired(string absent)
    {var f=new Fixture(new[]{"bob.read","bob.write","leads.read","leads.write"}.Except([absent]).ToArray());await Assert.ThrowsAsync<ForbiddenAccessException>(()=>f.Guard.RequireAsync(f.Provider,default));}
    [Theory][InlineData(BobAutonomy.DISABLED)][InlineData(BobAutonomy.RECOMMEND_ONLY)][InlineData(BobAutonomy.DRAFT)]
    public async Task PolicyCannotGrantExecutionItDoesNotAuthorize(BobAutonomy mode)
    {var f=new Fixture();f.State.Policy.Actions["lead.assign"]=mode;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Guard.RequireAsync(f.Provider,default));}
    [Theory][InlineData("read")][InlineData("disabled")][InlineData("recommend")][InlineData("draft")][InlineData("unknown")]
    public async Task CentralAutoCannotOverrideModulePolicyCeiling(string legacy)
    {var f=new Fixture(legacy:legacy);f.State.Policy.Actions["lead.assign"]=BobAutonomy.AUTO_ACT_BY_POLICY;await Assert.ThrowsAnyAsync<Exception>(()=>f.Guard.RequireAsync(f.Provider,default));}
    [Fact]public async Task MediumRiskExplicitAutoCanExecuteButLegacyApprovalRemainsACheckpoint()
    {var f=new Fixture();f.State.Policy.Actions["lead.assign"]=BobAutonomy.AUTO_ACT_BY_POLICY;Assert.False(await f.Guard.RequireAsync(f.Provider,default));var approval=new Fixture(legacy:"approval");approval.State.Policy.Actions["lead.assign"]=BobAutonomy.AUTO_ACT_BY_POLICY;Assert.True(await approval.Guard.RequireAsync(approval.Provider,default));}
    [Fact]public async Task DisabledTenantModuleFailsEvenForFullyAuthorizedUser()
    {var f=new Fixture();f.State.Policy.EnabledModules=["bob"];await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Guard.RequireAsync(f.Provider,default));}
    [Fact]public async Task ExternalCommunicationRequiresPolicyAndApproval()
    {var f=new Fixture(tool:"lead.send");await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Guard.RequireAsync(f.Provider,default));f.State.Policy.ExternalNotificationsEnabled=true;f.State.Policy.Actions["lead.send"]=BobAutonomy.AUTO_ACT_BY_POLICY;Assert.True(await f.Guard.RequireAsync(f.Provider,default));}
    [Fact]public async Task RoleDenialStillAppliesWithExplicitModuleGrants()
    {var f=new Fixture();f.Roles.Setup(r=>r.RequirePermissionAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new ForbiddenAccessException("role denied"));await Assert.ThrowsAsync<ForbiddenAccessException>(()=>f.Guard.RequireAsync(f.Provider,default));}
    [Fact] public async Task DurableExecutionClaimRejectsSameKeyEvenWithAnotherActionId()
    {
        var f=new Fixture();await f.Guard.ClaimExecutionAsync("same-key",Guid.NewGuid(),default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Guard.ClaimExecutionAsync("same-key",Guid.NewGuid(),default));
    }
    sealed class Provider(string tool):IBobActionProvider
    {public string ToolKey=>tool;public string PermissionKey=>"leads.write";public BobActionRisk Risk=>BobActionRisk.Destructive;public Task<object?> ExecuteAsync(BobActionExecutionContext c,JsonElement i,CancellationToken ct=default)=>throw new NotImplementedException();}
    sealed class Fixture
    {
        public BobIntelligenceState State=new();public BobActionGuard Guard;public Provider Provider;public Mock<IRoleAccessService> Roles=new();
        public Fixture(string[]? permissions=null,string? legacy=null,string tool="lead.assign")
        {
            Provider=new(tool);var membership=new Mock<ITenantMembershipRepository>();membership.Setup(m=>m.GetByUserIdAsync(It.IsAny<string>(),Actor,It.IsAny<CancellationToken>())).ReturnsAsync(new TenantMembership{Role="admin",TenantId=Tenant,UserId=Actor,MembershipStatus="Active"});
            var profiles=new Mock<IAzureTablesRepositoryStore<UserProfile>>();profiles.Setup(p=>p.GetByKeysAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new UserProfile{IsActive=true,ModulePermissions=permissions??["bob.read","bob.write","leads.read","leads.write"]});
            var user=new Mock<MedInsights.Lib.Utils.IUserContext>();user.SetupGet(u=>u.IsAuthenticated).Returns(true);user.SetupGet(u=>u.UserId).Returns(Actor);user.SetupGet(u=>u.TenantId).Returns(Tenant);
            var access=new UserModuleAccessService(membership.Object,profiles.Object,user.Object);
            var store=new Mock<IBobIntelligenceStore>();store.Setup(s=>s.ReadAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(State));store.Setup(s=>s.SaveAsync(Tenant,It.IsAny<BobIntelligenceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Callback<Guid,BobIntelligenceState,string,CancellationToken>((_,value,_,_)=>State=JobConfigurationService.Clone(value)).Returns(Task.CompletedTask);
            var settings=new Mock<ITenantSettingsRepository>();settings.Setup(s=>s.GetAsync(It.IsAny<string>(),"SETTINGS|OPERATIONAL",It.IsAny<CancellationToken>(),false)).ReturnsAsync(new TenantSettingsDocument{ValuesJson=legacy is null?"{}":JsonSerializer.Serialize(new{leads=new{aiActions=new Dictionary<string,string>{{tool,legacy}}}})});
            Roles.Setup(r=>r.RequirePermissionAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Guard=new(new BobCapabilityRegistry([Provider]),access,Roles.Object,new TurnKeyOps.Lib.Utils.UserContextAdapter(user.Object),store.Object,settings.Object,Mock.Of<IPayrollStore>(),Mock.Of<ISupplyStore>());
        }
    }
}
