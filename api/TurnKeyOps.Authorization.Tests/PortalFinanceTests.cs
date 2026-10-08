using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class PortalFinanceTests
{
    [Fact]public void CustomerProjectionRequiresEnabledFinanceAndExactTenantCustomerAndJobScope()
    {
        var tenant=Guid.NewGuid();var customer=Guid.NewGuid();var user=Guid.NewGuid();var jobId=Guid.NewGuid();var config=new PortalConfiguration{FinanceEnabled=true};
        var actor=new PortalActor(tenant,user,[new(){UserId=user,CustomerId=customer,Scope="job",RecordId=jobId}],config);
        var invoice=new Invoice{Id=Guid.NewGuid(),PartitionKey=RepositoryKeyHelper.ToTenantPartitionKey(tenant),CustomerId=customer,JobId=jobId};var job=new Job{Id=jobId,CustomerId=customer,PartitionKey=invoice.PartitionKey};
        Assert.True(PortalFinanceService.AllowsInvoice(actor,invoice,job));invoice.PartitionKey=RepositoryKeyHelper.ToTenantPartitionKey(Guid.NewGuid());Assert.False(PortalFinanceService.AllowsInvoice(actor,invoice,job));invoice.PartitionKey=job.PartitionKey;
        invoice.CustomerId=Guid.NewGuid();Assert.False(PortalFinanceService.AllowsInvoice(actor,invoice,job));invoice.CustomerId=customer;config.FinanceEnabled=false;Assert.False(PortalFinanceService.AllowsInvoice(actor,invoice,job));
    }
    [Theory][InlineData("https://evil.example/checkout")][InlineData("http://checkout.stripe.com/pay")][InlineData("https://checkout.stripe.com.evil.example/pay")][InlineData("https://user@checkout.stripe.com/pay")]
    public void PortalPaymentLinksRejectUntrustedDestinations(string value)=>Assert.Null(PortalFinanceService.SafePaymentUrl(value));
    [Fact]public void PortalPaymentLinksAcceptConfiguredStripeHosts()=>Assert.Equal("https://checkout.stripe.com/c/pay",PortalFinanceService.SafePaymentUrl("https://checkout.stripe.com/c/pay"));
    [Fact]public async Task PortalProjectionContainsOnlyCustomerReceivableFields()
    {
        var state=FinanceTests.State();var tenant=Guid.NewGuid();var user=Guid.NewGuid();var customer=Guid.NewGuid();var id=Guid.NewGuid();var date=new DateOnly(2026,9,15);
        var journal=FinanceLedger.Post(state,date,"invoice",id.ToString(),"INV-1",[new("1100",100,0),new("4000",0,100)],"owner");state.OpenItems.Add(new(id,"AR",customer,"INV-1",date,date,100,journal.Id));
        var store=new Mock<IFinanceStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(state);var invoices=new Mock<IInvoiceRepository>();invoices.Setup(r=>r.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(tenant),RepositoryKeyHelper.ToRowKey(id),It.IsAny<CancellationToken>())).ReturnsAsync(new Invoice{Id=id,CustomerId=customer,PartitionKey=RepositoryKeyHelper.ToTenantPartitionKey(tenant)});
        var service=new PortalFinanceService(store.Object,invoices.Object,Mock.Of<IJobRepository>());var actor=new PortalActor(tenant,user,[new(){UserId=user,CustomerId=customer,RecordId=customer}],new(){FinanceEnabled=true});var visible=await service.ListAsync(actor);
        Assert.Single(visible);Assert.Equal(100,visible[0].Balance);Assert.DoesNotContain(typeof(PortalInvoiceView).GetProperties(),p=>p.Name.Contains("Cost")||p.Name.Contains("Margin")||p.Name.Contains("Vendor"));
    }
}
