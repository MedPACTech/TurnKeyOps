using IBeam.Communications.Abstractions;
using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services;
namespace MedInsights.Authorization.Tests;
public sealed class SupplyDeliveryTests
{
    [Fact]public async Task ProviderUncertaintyNeverTriggersAnAutomaticResend()
    {
        var f=new Fixture();f.Email.Setup(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        await f.Service.SendAsync(f.Order.Id,"");Assert.Equal("unconfirmed",f.Store.Data.Orders[0].Dispatch!.Status);
        await f.Service.SendAsync(f.Order.Id,"");f.Email.Verify(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact]public async Task ConcurrentApprovedSendsClaimOneProviderAttempt()
    {
        var f=new Fixture();f.Email.Setup(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>())).Returns(async()=>await Task.Delay(20));
        await Task.WhenAll(f.Service.SendAsync(f.Order.Id,""),f.Service.SendAsync(f.Order.Id,""));Assert.Equal("SENT",f.Store.Data.Orders[0].Status);Assert.Equal("provider-accepted",f.Store.Data.Orders[0].Dispatch!.Status);
        f.Email.Verify(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact]public async Task ChangedApprovalRulesPreventVendorSend()
    {var f=new Fixture();f.Store.Data.Policy.ApprovalAbove=1;await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SendAsync(f.Order.Id,""));f.Email.Verify(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>()),Times.Never);}
    private sealed class Store:ISupplyStore
    {
        public SupplyState Data=new();private int revision;
        public Task<SupplyState> ReadAsync(Guid tenant,CancellationToken ct=default){lock(this)return Task.FromResult(JobConfigurationService.Clone(Data));}
        public Task SaveAsync(Guid tenant,SupplyState value,string expected,CancellationToken ct=default){lock(this){if(expected!=Data.Version)throw new InvalidOperationException("Stale");Data=JobConfigurationService.Clone(value);Data.Version=(++revision).ToString();return Task.CompletedTask;}}
    }
    private sealed class Fixture
    {
        public Store Store=new();public Mock<IEmailService> Email=new();public PurchaseOrder Order;public SupplyOrderDelivery Service;
        public Fixture(){var vendor=new VendorSupplyProfile{ContactId=Guid.NewGuid(),OrderingEmail="vendor@example.invalid"};Store.Data.Vendors.Add(vendor);Order=new(){Status="APPROVED",VendorId=vendor.ContactId,ApprovalPolicyHash=SupplyRules.PolicyHash(Store.Data.Policy)};Store.Data.Orders.Add(Order);
            var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(Guid.NewGuid());var profile=new Mock<ITenantCommunicationProfileResolver>();profile.Setup(p=>p.Resolve(It.IsAny<Guid>())).Returns(new TenantCommunicationProfile{EmailFromAddress="test@example.invalid"});Service=new(Store,new Mock<ISupplyAuthority>().Object,user.Object,Email.Object,profile.Object);}
    }
}
