namespace TurnKeyOps.Lib.Dtos;
public sealed record ConstructionPaymentIntent(Guid Id,Guid OpenItemId,Guid CustomerId,Guid JobId,decimal Amount,string Currency,string CashAccountId,string Status,string Reason,string Actor,DateTime CreatedAtUtc,string? SessionId=null,string? Url=null,string? Error=null,int Attempts=0,string MerchantAccount="");
public sealed record ConstructionPaymentReceipt(string EventId,Guid IntentId,string SessionId,decimal Amount,string Currency,DateOnly Date,string Status,Guid? SettlementId=null,string? Error=null);
public sealed class ConstructionPaymentCommand
{
 public Guid Id{get;set;}=Guid.NewGuid();public string ExpectedVersion{get;set;}="";public Guid OpenItemId{get;set;}public Guid TargetId{get;set;}public string CashAccountId{get;set;}="";public string Reason{get;set;}="";public string Action{get;set;}="create";
}
