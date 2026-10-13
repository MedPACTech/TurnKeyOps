using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class ConstructionPaymentIntegrity
{
 public static void ValidateTransition(FinanceState before,FinanceState after)
 {
  if(after.ConstructionPayments.Select(i=>i.Id).Distinct().Count()!=after.ConstructionPayments.Count||after.ConstructionPaymentReceipts.Select(r=>r.EventId).Distinct().Count()!=after.ConstructionPaymentReceipts.Count||after.ConstructionPaymentReceipts.Select(r=>r.SessionId).Distinct().Count()!=after.ConstructionPaymentReceipts.Count)throw new ArgumentException("Duplicate construction payment identity.");
  foreach(var old in before.ConstructionPayments)
  {
   var n=after.ConstructionPayments.SingleOrDefault(i=>i.Id==old.Id)??throw new ArgumentException("Construction payment intents cannot be removed.");
   if(n.OpenItemId!=old.OpenItemId||n.CustomerId!=old.CustomerId||n.JobId!=old.JobId||n.Amount!=old.Amount||n.Currency!=old.Currency||n.CashAccountId!=old.CashAccountId||n.MerchantAccount!=old.MerchantAccount||n.Reason!=old.Reason||n.Actor!=old.Actor||n.CreatedAtUtc!=old.CreatedAtUtc||old.SessionId!=null&&n.SessionId!=old.SessionId||n.Attempts<old.Attempts)throw new ArgumentException("Construction payment source identity is immutable.");
   var allowed=old.Status switch{"PENDING"=>new[]{"PENDING","READY","RECEIVED","CANCEL_PENDING"},"READY"=>new[]{"READY","RECEIVED","CANCEL_PENDING"},"CANCEL_PENDING"=>new[]{"CANCEL_PENDING","CANCELLED","RECEIVED"},"RECEIVED"=>new[]{"RECEIVED","PAID"},"PAID"=>new[]{"PAID"},"CANCELLED"=>new[]{"CANCELLED"},_=>Array.Empty<string>()};
   if(!allowed.Contains(n.Status))throw new ArgumentException("Invalid construction checkout transition.");
  }
  foreach(var old in before.ConstructionPaymentReceipts)
  {
   var n=after.ConstructionPaymentReceipts.SingleOrDefault(r=>r.EventId==old.EventId)??throw new ArgumentException("Provider payment receipts cannot be removed.");
   if(n.IntentId!=old.IntentId||n.SessionId!=old.SessionId||n.Amount!=old.Amount||n.Currency!=old.Currency||n.Date!=old.Date||old.Status=="POSTED"&&n!=old||old.Status=="PENDING"&&n.Status is not("PENDING" or "POSTED"))throw new ArgumentException("Provider payment receipt identity is immutable.");
  }
  foreach(var r in after.ConstructionPaymentReceipts.Where(r=>r.Status=="POSTED"))
  {
   var intent=after.ConstructionPayments.Single(i=>i.Id==r.IntentId);var settlement=after.Settlements.SingleOrDefault(s=>s.Id==r.SettlementId&&s.Origin=="construction-provider");
   if(settlement==null||settlement.Amount!=r.Amount||settlement.PartyId!=intent.CustomerId||settlement.SourceKey!="construction-stripe:"+r.SessionId||settlement.Allocations.Length!=1||settlement.Allocations[0].OpenItemId!=intent.OpenItemId||settlement.Allocations[0].Amount!=r.Amount)throw new ArgumentException("Construction receipt must reconcile to its AR settlement.");
  }
 }
}
