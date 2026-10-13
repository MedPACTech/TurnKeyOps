using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public sealed record ConstructionCheckout(string Id,string Url);
public sealed record ConstructionVerifiedPayment(Guid TenantId,Guid IntentId,Guid OpenItemId,Guid CustomerId,string EventId,string SessionId,decimal Amount,string Currency,DateOnly Date,string Account);
public interface IConstructionPaymentProvider
{
 string MerchantAccount(Guid tenant);bool Configured(Guid tenant);Task<ConstructionCheckout> CreateAsync(Guid tenant,ConstructionPaymentIntent intent,CancellationToken ct=default);
 Task ExpireAsync(Guid tenant,ConstructionPaymentIntent intent,string sessionId,CancellationToken ct=default);
 Task<ConstructionVerifiedPayment> VerifyAsync(string json,IReadOnlyDictionary<string,string> headers,CancellationToken ct=default);
}
// Reuses the configured Stripe SDK and signature verifier. A tenant's explicitly configured
// connected account is mandatory; the platform subscription account is never assumed to be its bank.
public sealed class ConstructionStripePaymentProvider(IServiceProvider services,IConfiguration config):IConstructionPaymentProvider
{
 private string? Account(Guid tenant)=>config[$"ConstructionPayments:Tenants:{tenant:D}:StripeAccountId"];
 public string MerchantAccount(Guid tenant)=>Account(tenant)??"";
 public bool Configured(Guid tenant)=>config[$"ConstructionPayments:Tenants:{tenant:D}:Enabled"]=="true"&&Account(tenant)?.StartsWith("acct_",StringComparison.Ordinal)==true&&!string.IsNullOrWhiteSpace(config["StripeSettings:SecretKey"])&&!string.IsNullOrWhiteSpace(config["ConstructionPayments:WebhookSecret"])&&Uri.TryCreate(config["ConstructionPayments:ReturnUrl"],UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&string.IsNullOrEmpty(uri.UserInfo);
 public async Task<ConstructionCheckout> CreateAsync(Guid tenant,ConstructionPaymentIntent intent,CancellationToken ct=default)
 {
  if(!Configured(tenant))throw new ArgumentException("Construction Stripe payments are not configured for this tenant.");
  var metadata=new Dictionary<string,string>{{"tenant_id",tenant.ToString("D")},{"construction_intent_id",intent.Id.ToString("D")},{"construction_open_item_id",intent.OpenItemId.ToString("D")},{"customer_id",intent.CustomerId.ToString("D")}};
  var service=new Stripe.Checkout.SessionService(services.GetRequiredService<Stripe.StripeClient>());var session=await service.CreateAsync(new(){Mode="payment",PaymentMethodTypes=["card"],SuccessUrl=config["ConstructionPayments:ReturnUrl"],CancelUrl=config["ConstructionPayments:ReturnUrl"],ClientReferenceId=intent.Id.ToString("D"),Metadata=metadata,PaymentIntentData=new(){Metadata=metadata},LineItems=[new(){Quantity=1,PriceData=new(){Currency=intent.Currency.ToLowerInvariant(),UnitAmount=checked((long)(intent.Amount*100)),ProductData=new(){Name="Construction invoice payment"}}}]},new Stripe.RequestOptions{IdempotencyKey=$"construction:{tenant:N}:{intent.Id:N}",StripeAccount=intent.MerchantAccount},ct);
  var url=PortalFinanceService.SafePaymentUrl(session.Url)??throw new ArgumentException("Provider returned an invalid payment URL.");return new(session.Id,url);
 }
 public async Task ExpireAsync(Guid tenant,ConstructionPaymentIntent intent,string sessionId,CancellationToken ct=default)
 {if(string.IsNullOrWhiteSpace(intent.MerchantAccount))throw new ArgumentException("Original merchant account is required to expire checkout.");await new Stripe.Checkout.SessionService(services.GetRequiredService<Stripe.StripeClient>()).ExpireAsync(sessionId,new Stripe.Checkout.SessionExpireOptions(),new Stripe.RequestOptions{StripeAccount=intent.MerchantAccount,IdempotencyKey=$"construction-expire:{tenant:N}:{intent.Id:N}"},ct);}
 public Task<ConstructionVerifiedPayment> VerifyAsync(string json,IReadOnlyDictionary<string,string> headers,CancellationToken ct=default)
 {
  ct.ThrowIfCancellationRequested();var secret=config["ConstructionPayments:WebhookSecret"];if(string.IsNullOrWhiteSpace(secret))throw new InvalidOperationException("Dedicated construction webhook signing secret is not configured.");
  headers.TryGetValue("Stripe-Signature",out var signature);var verified=Stripe.EventUtility.ConstructEvent(json,signature,secret,throwOnApiVersionMismatch:false);
  using var doc=System.Text.Json.JsonDocument.Parse(json);var root=doc.RootElement;
  if(verified.Type!="checkout.session.completed")throw new ArgumentException("Only completed construction checkout sessions are accepted.");
  var obj=root.GetProperty("data").GetProperty("object");if(obj.GetProperty("payment_status").GetString()!="paid"||obj.GetProperty("mode").GetString()!="payment")throw new ArgumentException("Checkout payment is not confirmed paid.");
  var m=obj.GetProperty("metadata");Guid Id(string key)=>Guid.TryParse(m.GetProperty(key).GetString(),out var id)&&id!=Guid.Empty?id:throw new ArgumentException("Invalid construction payment metadata.");
  var tenant=Id("tenant_id");var account=root.TryGetProperty("account",out var a)?a.GetString():null;
  if(string.IsNullOrWhiteSpace(account)||!account.StartsWith("acct_",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(verified.Id))throw new ArgumentException("Construction provider tenant/account mismatch.");
  var amount=obj.GetProperty("amount_total").GetInt64()/100m;FinanceLedger.Positive(amount);var created=root.GetProperty("created").GetInt64();
  return Task.FromResult(new ConstructionVerifiedPayment(tenant,Id("construction_intent_id"),Id("construction_open_item_id"),Id("customer_id"),verified.Id,obj.GetProperty("id").GetString()??"",amount,obj.GetProperty("currency").GetString()?.ToUpperInvariant()??"",DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(created).UtcDateTime),account!));
 }
}
