using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurnKeyOps.Services;
namespace TurnKeyOps.Authorization.Tests;
public sealed class ConstructionPaymentProviderTests
{
 private const string Dedicated="test-construction-endpoint-only";
 private static Dictionary<string,string> Sign(string json,string secret=Dedicated)
 {var timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds();var digest=Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(timestamp+"."+json)));return new(){{"Stripe-Signature",$"t={timestamp},v1={digest}"}};}
 private static (ConstructionStripePaymentProvider Service,string Payload,Guid Tenant,IConfiguration Config) Fixture(string eventType="checkout.session.completed",string status="paid",string? metadataTenant=null,bool secret=true)
 {
  var tenant=Guid.NewGuid();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConstructionPayments:Tenants:"+tenant.ToString("D")+":Enabled","true"},{"ConstructionPayments:Tenants:"+tenant.ToString("D")+":StripeAccountId","acct_tenant"},{"ConstructionPayments:ReturnUrl","https://example.invalid/portal/finance"},{"StripeSettings:SecretKey","test-only-not-live"},{"StripeSettings:WebhookSecret","test-saas-endpoint-only"},{"ConstructionPayments:WebhookSecret",secret?Dedicated:null}}).Build();
  var payload=JsonSerializer.Serialize(new{id="evt_one",@object="event",api_version="2025-12-15.clover",livemode=false,pending_webhooks=1,request=new{id="req_test",idempotency_key="construction:test"},type=eventType,account="acct_tenant",created=1789502400,data=new{@object=new{@object="checkout.session",id="cs_one",mode="payment",payment_status=status,amount_total=1234,currency="usd",metadata=new{tenant_id=metadataTenant??tenant.ToString("D"),construction_intent_id=Guid.NewGuid(),construction_open_item_id=Guid.NewGuid(),customer_id=Guid.NewGuid()}}}});
  return(new(new ServiceCollection().BuildServiceProvider(),config),payload,tenant,config);
 }
 [Fact]public async Task DedicatedSignedCardCheckoutPreservesMetadataAndCents(){var f=Fixture();var result=await f.Service.VerifyAsync(f.Payload,Sign(f.Payload));Assert.Equal(f.Tenant,result.TenantId);Assert.Equal(12.34m,result.Amount);Assert.Equal("USD",result.Currency);Assert.Equal("acct_tenant",result.Account);Assert.True(f.Service.Configured(f.Tenant));}
 [Theory][InlineData("payment_intent.succeeded","paid")][InlineData("charge.refunded","paid")][InlineData("checkout.session.completed","unpaid")]
 public async Task OtherEventTypesAndUnpaidSessionsAreRejected(string type,string status){var f=Fixture(type,status);await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.VerifyAsync(f.Payload,Sign(f.Payload)));}
 [Fact]public async Task PlatformSaasWebhookSecretCannotAuthenticateConstructionReceipt(){var f=Fixture();await Assert.ThrowsAsync<Stripe.StripeException>(()=>f.Service.VerifyAsync(f.Payload,Sign(f.Payload,"test-saas-endpoint-only")));}
 [Fact]public async Task TamperedTenantMetadataFailsSignatureVerification(){var f=Fixture();var tampered=f.Payload.Replace(f.Tenant.ToString("D"),Guid.NewGuid().ToString("D"));await Assert.ThrowsAsync<Stripe.StripeException>(()=>f.Service.VerifyAsync(tampered,Sign(f.Payload)));}
 [Fact]public async Task InvalidTenantMetadataIsRejectedAfterSignatureVerification(){var f=Fixture(metadataTenant:"not-a-tenant");await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.VerifyAsync(f.Payload,Sign(f.Payload)));}
 [Fact]public async Task MissingDedicatedSecretDisablesCheckoutWithoutSaasFallback(){var f=Fixture(secret:false);Assert.False(f.Service.Configured(f.Tenant));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.VerifyAsync(f.Payload,Sign(f.Payload,"test-saas-endpoint-only")));}
 [Fact]public void DisabledProviderDoesNotResolveSdkOrCredentials(){var service=new ConstructionStripePaymentProvider(new ServiceCollection().BuildServiceProvider(),new ConfigurationBuilder().Build());Assert.False(service.Configured(Guid.NewGuid()));}
}
