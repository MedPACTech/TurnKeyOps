using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;
public sealed class ConstructionPaymentService(IFinanceStore store,IFinanceSourceCoordinator coordinator,IFinanceAuthority authority,IUserContext user,IConstructionPaymentProvider provider)
{
 public async Task<object> ReadAsync(CancellationToken ct=default){await authority.RequireAsync(false,ct);var s=await store.ReadAsync(user.TenantId,ct);return new{s.Version,Configured=provider.Configured(user.TenantId),Intents=s.ConstructionPayments,Receipts=s.ConstructionPaymentReceipts};}
 public async Task<object> CommandAsync(ConstructionPaymentCommand c,CancellationToken ct=default)
 {
  await authority.RequireAsync(true,ct);if(!await authority.CanControlAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("Finance owner approval required to dispatch or reconcile construction payments.");
  if(c.Id==Guid.Empty)throw new ArgumentException("Command identity required.");FinanceLedger.Required(c.Reason);
  await using var fence=await coordinator.AcquireAsync(user.TenantId,ct);var s=await store.ReadAsync(user.TenantId,ct);
  if(c.Action=="cancel")
  {
   if(s.Version!=c.ExpectedVersion)throw new InvalidOperationException("Finance changed.");var cancel=s.ConstructionPayments.SingleOrDefault(i=>i.Id==c.TargetId)??throw new KeyNotFoundException();
   if(cancel.Status=="CANCELLED")return new{Cancelled=true};if(cancel.Status is "PAID" or "RECEIVED")throw new ArgumentException("Received payments require the audited refund workflow, not checkout cancellation.");
   var ix=s.ConstructionPayments.FindIndex(i=>i.Id==cancel.Id);s.ConstructionPayments[ix]=cancel with{Status="CANCEL_PENDING",Url=null};await store.SaveAsync(user.TenantId,s,s.Version,ct);
   var session=cancel.SessionId;if(session==null)session=(await provider.CreateAsync(user.TenantId,cancel,ct)).Id;
   await provider.ExpireAsync(user.TenantId,cancel,session,ct);s=await store.ReadAsync(user.TenantId,ct);ix=s.ConstructionPayments.FindIndex(i=>i.Id==cancel.Id);if(s.ConstructionPayments[ix].Status!="CANCEL_PENDING")throw new InvalidOperationException("Payment changed during cancellation.");s.ConstructionPayments[ix]=cancel with{Status="CANCELLED",Url=null,SessionId=session,Error=null};s.Audit.Add(new(c.Id,"construction.payment-cancelled",cancel.Id.ToString(),user.UserId.ToString(),DateTime.UtcNow,c.Reason,FinanceLedger.Hash(new{c.Id,c.TargetId,c.Reason})));await store.SaveAsync(user.TenantId,s,s.Version,ct);return new{Cancelled=true};
  }
  if(c.Action=="reconcile") {if(s.Version!=c.ExpectedVersion)throw new InvalidOperationException("Finance changed.");await PostPendingAsync(user.TenantId,s,ct);return new{Recorded=true};}
  ConstructionPaymentIntent intent;
  if(c.Action=="create")
  {
   var old=s.ConstructionPayments.SingleOrDefault(i=>i.Id==c.Id);
   if(old!=null){if(old.OpenItemId!=c.OpenItemId||old.CashAccountId!=c.CashAccountId||old.Reason!=c.Reason)throw new ArgumentException("Payment command identity conflict.");intent=old;}
   else
   {
    if(s.Version!=c.ExpectedVersion)throw new InvalidOperationException("Finance changed. Refresh before dispatch.");
    if(!provider.Configured(user.TenantId))throw new ArgumentException("Construction payment provider is not configured. No link has been created.");
    var item=ConstructionItem(s,c.OpenItemId);if(item.Date>DateOnly.FromDateTime(DateTime.UtcNow))throw new ArgumentException("Future-dated construction AR cannot be dispatched before its issue date.");var balance=FinanceLedger.Balance(s,item,DateOnly.MaxValue);FinanceLedger.Positive(balance);
    if(s.ConstructionPayments.Any(i=>i.OpenItemId==item.Id&&i.Status is "PENDING" or "READY" or "RECEIVED" or "CANCEL_PENDING"))throw new ArgumentException("An active payment intent already exists. Retry or reconcile that intent.");
    if(!s.CashAccounts.Any(a=>a.Id==c.CashAccountId&&a.Active))throw new ArgumentException("Choose an active receiving cash/clearing account.");
    if(s.Policy.Currency!="USD")throw new ArgumentException("Construction checkout currently supports USD only.");
    intent=new(c.Id,item.Id,item.PartyId,item.JobId!.Value,balance,s.Policy.Currency,c.CashAccountId,"PENDING",c.Reason,user.UserId.ToString(),DateTime.UtcNow,MerchantAccount:provider.MerchantAccount(user.TenantId));
    s.ConstructionPayments.Add(intent);s.Audit.Add(new(c.Id,"construction.payment-intent",item.Id.ToString(),user.UserId.ToString(),DateTime.UtcNow,c.Reason,FinanceLedger.Hash(new{CommandId=c.Id,OpenItemId=item.Id,c.CashAccountId,c.Reason})));
    await store.SaveAsync(user.TenantId,s,s.Version,ct);s=await store.ReadAsync(user.TenantId,ct);
   }
  }
  else if(c.Action=="retry") {if(s.Version!=c.ExpectedVersion)throw new InvalidOperationException("Finance changed.");intent=s.ConstructionPayments.SingleOrDefault(i=>i.Id==c.TargetId)??throw new KeyNotFoundException();}
  else throw new ArgumentException("Unsupported payment command.");
  if(intent.Status=="CANCEL_PENDING"||intent.Status=="CANCELLED")throw new ArgumentException("Checkout cancellation must be resolved before retrying dispatch.");
  if(intent.Status is "READY" or "PAID")return new{intent.Id,intent.Status,intent.Url};
  if(intent.Status=="RECEIVED")throw new ArgumentException("Payment already received; reconcile its pending ledger receipt.");
  if(intent.MerchantAccount!=provider.MerchantAccount(user.TenantId))throw new ArgumentException("Tenant merchant configuration changed. Restore the original account before retrying this intent.");
  var source=ConstructionItem(s,intent.OpenItemId);if(FinanceLedger.Balance(s,source,DateOnly.MaxValue)!=intent.Amount)throw new ArgumentException("Invoice balance changed after intent creation. Resolve the existing checkout before dispatching another amount.");
  try {var checkout=await provider.CreateAsync(user.TenantId,intent,ct);s=await store.ReadAsync(user.TenantId,ct);var index=s.ConstructionPayments.FindIndex(i=>i.Id==intent.Id);s.ConstructionPayments[index]=intent with{SessionId=checkout.Id,Url=checkout.Url,Status="READY",Error=null,Attempts=intent.Attempts+1};await store.SaveAsync(user.TenantId,s,s.Version,ct);return new{intent.Id,Status="READY",Url=checkout.Url};}
  catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
  catch {s=await store.ReadAsync(user.TenantId,ct);var index=s.ConstructionPayments.FindIndex(i=>i.Id==intent.Id);if(s.ConstructionPayments[index].Status=="PENDING")s.ConstructionPayments[index]=intent with{Error="Provider dispatch is unconfirmed. Retry this intent with its original identity; do not create another checkout.",Attempts=intent.Attempts+1};await store.SaveAsync(user.TenantId,s,s.Version,ct);throw;}
 }
 public async Task<object> ReceiveAsync(string json,IReadOnlyDictionary<string,string> headers,CancellationToken ct=default)
 {
  var payment=await provider.VerifyAsync(json,headers,ct);await using var fence=await coordinator.AcquireAsync(payment.TenantId,ct);var s=await store.ReadAsync(payment.TenantId,ct);
  var intent=s.ConstructionPayments.SingleOrDefault(i=>i.Id==payment.IntentId)??throw new ArgumentException("Unknown construction payment intent.");
  if(payment.OpenItemId!=intent.OpenItemId||payment.CustomerId!=intent.CustomerId||payment.Amount!=intent.Amount||payment.Currency!=intent.Currency||payment.Account!=intent.MerchantAccount||intent.SessionId!=null&&intent.SessionId!=payment.SessionId)throw new ArgumentException("Verified payment does not match the persisted construction checkout.");
  ConstructionItem(s,intent.OpenItemId);var prior=s.ConstructionPaymentReceipts.SingleOrDefault(r=>r.EventId==payment.EventId||r.SessionId==payment.SessionId);
  if(prior!=null){if(prior.IntentId!=intent.Id||prior.Amount!=payment.Amount||prior.Currency!=payment.Currency)throw new ArgumentException("Provider receipt identity conflict.");if(prior.Status=="PENDING")await PostPendingAsync(payment.TenantId,s,ct);return new{prior.Status,Replayed=true};}
  s.ConstructionPaymentReceipts.Add(new(payment.EventId,intent.Id,payment.SessionId,payment.Amount,payment.Currency,payment.Date,"PENDING"));
  var idx=s.ConstructionPayments.FindIndex(i=>i.Id==intent.Id);s.ConstructionPayments[idx]=intent with{Status="RECEIVED",SessionId=payment.SessionId,Url=null};await store.SaveAsync(payment.TenantId,s,s.Version,ct);s=await store.ReadAsync(payment.TenantId,ct);
  await PostPendingAsync(payment.TenantId,s,ct);return new{Recorded=true};
 }
 private async Task PostPendingAsync(Guid tenant,FinanceState s,CancellationToken ct)
 {
  foreach(var receipt in s.ConstructionPaymentReceipts.Where(r=>r.Status=="PENDING").ToArray())
  {
   var oldVersion=s.Version;
   try
   {
    var intent=s.ConstructionPayments.Single(i=>i.Id==receipt.IntentId);var item=ConstructionItem(s,intent.OpenItemId);
    if(receipt.Date<item.Date)throw new ArgumentException("Provider receipt predates its AR document; accounting review is required before posting.");
    var sourceKey="construction-stripe:"+receipt.SessionId;var existing=s.Settlements.SingleOrDefault(p=>p.SourceKey==sourceKey);
    if(existing==null)
    {
     if(FinanceLedger.Balance(s,item,DateOnly.MaxValue)<receipt.Amount)throw new ArgumentException("Received amount exceeds remaining AR. Review manual receipts and provider evidence before reconciliation.");
     var account=s.CashAccounts.SingleOrDefault(a=>a.Id==intent.CashAccountId&&a.Active)??throw new ArgumentException("Receiving account is unavailable.");
     var journal=FinanceLedger.Post(s,receipt.Date,"construction-payment",receipt.SessionId,"Construction checkout receipt",[new(account.LedgerAccount,receipt.Amount,0,CustomerId:item.PartyId),new(s.Policy.Accounts["ar"],0,receipt.Amount,item.JobId,CustomerId:item.PartyId)],"verified-stripe");
     existing=new(Guid.NewGuid(),"payment",item.PartyId,receipt.Date,receipt.Amount,account.Id,"Stripe",receipt.SessionId,sourceKey,journal.Id,[new(item.Id,receipt.Amount)],"construction-provider");s.Settlements.Add(existing);
     s.Audit.Add(new(Guid.NewGuid(),"construction.payment-received",item.Id.ToString(),"verified-stripe",DateTime.UtcNow,receipt.EventId,FinanceLedger.Hash(new{receipt.EventId,receipt.SessionId,receipt.Amount})));
    }
    var index=s.ConstructionPaymentReceipts.FindIndex(r=>r.EventId==receipt.EventId);s.ConstructionPaymentReceipts[index]=receipt with{Status="POSTED",SettlementId=existing.Id,Error=null};var ix=s.ConstructionPayments.FindIndex(i=>i.Id==intent.Id);s.ConstructionPayments[ix]=intent with{Status="PAID",Url=null,Error=null};await store.SaveAsync(tenant,s,oldVersion,ct);s=await store.ReadAsync(tenant,ct);
   }
   catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
   catch {s=await store.ReadAsync(tenant,ct);var index=s.ConstructionPaymentReceipts.FindIndex(r=>r.EventId==receipt.EventId);if(s.ConstructionPaymentReceipts[index].Status=="PENDING"){s.ConstructionPaymentReceipts[index]=receipt with{Error="Receipt recorded but ledger posting blocked. Review period, AR balance and receiving account; retry reconciliation."};await store.SaveAsync(tenant,s,s.Version,ct);}s=await store.ReadAsync(tenant,ct);}
  }
 }
 private static FinanceOpenItem ConstructionItem(FinanceState s,Guid id)
 {
  var item=s.OpenItems.SingleOrDefault(i=>i.Id==id&&i.Kind=="AR"&&i.JobId!=null)??throw new KeyNotFoundException();
  if(!s.Construction.Applications.Any(a=>a.InvoiceId==id&&a.JournalId==item.JournalId)&&!s.Construction.RetainageReleases.Any(r=>r.OpenItemId==id&&r.JournalId==item.JournalId))throw new ArgumentException("AR source is not an issued construction application or retainage release.");
  var con=s.Construction.Contracts.SingleOrDefault(c=>c.JobId==item.JobId&&c.CustomerId==item.PartyId)??throw new ArgumentException("Construction Customer source mismatch.");return item;
 }
}
