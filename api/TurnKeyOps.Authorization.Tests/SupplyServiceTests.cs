using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace MedInsights.Authorization.Tests;
public sealed class SupplyServiceTests
{
    [Theory][InlineData("inventory","adjust")][InlineData("purchasing","draft-order")]
    public async Task ReadPermissionCannotMutate(string module,string action)
    {var f=new Fixture();f.Authority.Setup(a=>a.RequireAsync(module,true,It.IsAny<CancellationToken>())).ThrowsAsync(new ForbiddenAccessException("Read only"));await Assert.ThrowsAsync<ForbiddenAccessException>(()=>f.Service.CommandAsync(module,new(){Action=action}));Assert.Equal(0,f.Store.Writes);}
    [Fact]public async Task BobCannotBypassInventoryAuthority()
    {var f=new Fixture();f.Authority.Setup(a=>a.RequireAsync("inventory",true,It.IsAny<CancellationToken>())).ThrowsAsync(new ForbiddenAccessException("Read only"));var bob=new BobSupplyActionProvider(f.Service,"supply.reserve");await Assert.ThrowsAsync<ForbiddenAccessException>(()=>bob.ExecuteAsync(null!,JsonSerializer.SerializeToElement(new SupplyCommand(),JobConfigurationService.Json)));}
    [Fact]public async Task ForeignJobAndVendorReferencesAreRejected()
    {var f=new Fixture();await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync("inventory",new(){Action="demand",Demand=new(){JobId=Guid.NewGuid()}}));await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync("purchasing",new(){Action="vendor",Vendor=new(){ContactId=Guid.NewGuid()}}));Assert.Equal(0,f.Store.Writes);}
    [Fact]public async Task CostProjectionRequiresPurchasingRead()
    {var f=new Fixture();f.Store.Data.Catalog.Add(new(){Name="Test",DefaultCost=8123});var json=JsonSerializer.Serialize(await f.Service.WorkspaceAsync("inventory"),JobConfigurationService.Json);Assert.DoesNotContain("8123",json);Assert.Contains("\"defaultCost\":null",json);}
    [Fact]public async Task StaleVersionCannotCommitAndRetryDoesNotDoubleApply()
    {var f=new Fixture();var command=new SupplyCommand{Action="location",Location=new(){Name="Warehouse"}};await f.Service.CommandAsync("inventory",command);await f.Service.CommandAsync("inventory",command);Assert.Single(f.Store.Data.Locations);Assert.Equal(1,f.Store.Writes);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.CommandAsync("inventory",new(){Action="location",Location=new(){Name="Truck"}}));}
    [Fact]public async Task ItemMetadataAndImportedIdentityAreValidated()
    {var f=new Fixture();await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync("inventory",new(){Action="catalog",Item=new(){Name="Lock",Trades=["concrete"],Metadata=new(){["keying"]="A"}}}));await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync("inventory",new(){Action="catalog",Item=new(){Name="Imported",SourceSystem="legacy"}}));}
    private sealed class MemoryStore:ISupplyStore
    {public SupplyState Data=new();public int Writes;public Task<SupplyState> ReadAsync(Guid tenant,CancellationToken ct=default)=>Task.FromResult(JobConfigurationService.Clone(Data));public Task SaveAsync(Guid tenant,SupplyState s,string expected,CancellationToken ct=default){Assert.Equal(Data.Version,expected);Data=JobConfigurationService.Clone(s);Data.Version=(++Writes).ToString();return Task.CompletedTask;}}
    private sealed class Fixture
    {
        public MemoryStore Store=new();public Mock<ISupplyAuthority> Authority=new();public SupplyService Service;
        public Fixture(){var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(Guid.NewGuid());user.SetupGet(u=>u.UserId).Returns(Guid.NewGuid());
            Service=new(Store,Authority.Object,user.Object,new Mock<IJobRepository>().Object,new Mock<IJobWorkflowPayloadStore>().Object,new Mock<IManagedProfileStore>().Object,new Mock<ICalendarEventRepository>().Object);}
    }
}
