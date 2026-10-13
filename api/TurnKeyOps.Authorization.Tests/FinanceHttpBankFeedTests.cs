using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceHttpBankFeedTests
{
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(respond(request));}
    [Fact]public async Task TenantConfiguredGatewayAuthenticatesAndOverridesProviderCashIdentity()
    {
        var tenant=Guid.NewGuid();var path="Finance:BankFeeds:Tenants:"+tenant.ToString("D")+":";
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{path+"Enabled","true"},{path+"ProviderId","gateway"},{path+"AccountReference","source-account"},{path+"BaseUrl","https://bank.example/api"},{path+"ApiKey","test-secret"}}).Build();
        using var client=new HttpClient(new Handler(request=>{Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);Assert.Equal("test-secret",request.Headers.Authorization?.Parameter);Assert.Equal("?account=source-account",request.RequestUri?.Query);return new(HttpStatusCode.OK){Content=new StringContent("[{\"externalId\":\"t1\",\"cashAccountId\":\"foreign\",\"date\":\"2026-09-15\",\"amount\":100,\"reference\":\"Deposit\"}]")};}));
        var clients=new Mock<IHttpClientFactory>();clients.Setup(f=>f.CreateClient("FinanceBankFeed")).Returns(client);var provider=new FinanceHttpBankFeedProvider(clients.Object,config);
        var result=await provider.ReadAsync(tenant,new("gateway","source-account","operating",true));Assert.Equal("operating",result.Transactions.Single().CashAccountId);Assert.Contains(tenant.ToString("N"),result.ProviderAccountKey);
        await Assert.ThrowsAsync<ArgumentException>(()=>provider.ReadAsync(Guid.NewGuid(),new("gateway","source-account","operating",true)));
        await Assert.ThrowsAsync<ArgumentException>(()=>provider.ReadAsync(tenant,new("gateway","foreign-account","operating",true)));
    }
    [Fact]public async Task DisabledGatewayNeverMakesNetworkCall()
    {
        var factory=new Mock<IHttpClientFactory>();var provider=new FinanceHttpBankFeedProvider(factory.Object,new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(()=>provider.ReadAsync(Guid.NewGuid(),new("gateway","account","operating",true)));factory.Verify(f=>f.CreateClient(It.IsAny<string>()),Times.Never);
    }
    [Fact]public void OfxQfxParsesStableFitIdAndRejectsCurrencyOrMultipleAccounts()
    {
        var ofx="OFXHEADER:100\n<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>USD<ACCTID>acct-1<BANKTRANLIST><STMTTRN><DTPOSTED>20260915000000[-5:EST]<TRNAMT>-12.34<FITID>fit-1<NAME>Vendor<MEMO>ACH</STMTTRN></BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var batch=FinanceBankFeedOfx.Parse(ofx,"bank/account","operating");Assert.Equal("fit-1",batch.Transactions.Single().ExternalId);Assert.Equal(-12.34m,batch.Transactions.Single().Amount);
        Assert.Throws<ArgumentException>(()=>FinanceBankFeedOfx.Parse(ofx.Replace("<CURDEF>USD","<CURDEF>EUR"),"bank/account","operating"));
        Assert.Throws<ArgumentException>(()=>FinanceBankFeedOfx.Parse(ofx+"<ACCTID>acct-2","bank/account","operating"));
    }
    [Fact]public async Task EmptyGatewayResponseIsSuccessfulNoOp()
    {
        var tenant=Guid.NewGuid();var path="Finance:BankFeeds:Tenants:"+tenant.ToString("D")+":";var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{path+"Enabled","true"},{path+"ProviderId","gateway"},{path+"AccountReference","account"},{path+"BaseUrl","https://bank.example"},{path+"ApiKey","test"}}).Build();
        using var client=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent("[]")}));var clients=new Mock<IHttpClientFactory>();clients.Setup(f=>f.CreateClient("FinanceBankFeed")).Returns(client);
        Assert.Empty((await new FinanceHttpBankFeedProvider(clients.Object,config).ReadAsync(tenant,new("gateway","account","operating",true))).Transactions);
    }

}
