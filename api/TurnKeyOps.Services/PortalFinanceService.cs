using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
namespace TurnKeyOps.Services;
public sealed record PortalInvoiceView(Guid Id,string Number,DateOnly Date,DateOnly DueDate,decimal Total,decimal Balance,Guid? JobId,PortalReceiptView[] Receipts,string? PaymentUrl);
public sealed record PortalReceiptView(DateOnly Date,string Kind,decimal Amount,string Reference);
public sealed class PortalFinanceService(IFinanceStore store,IInvoiceRepository invoices,IJobRepository jobs)
{
    public static bool AllowsInvoice(PortalActor actor,Invoice invoice,Job? job)
    {
        var partition=RepositoryKeyHelper.ToTenantPartitionKey(actor.TenantId);
        if(!actor.Configuration.FinanceEnabled||invoice.IsDeleted||invoice.PartitionKey!=partition)return false;
        if(PortalAccessService.Allows(actor,"invoice",invoice.Id,invoice.CustomerId))return true;
        return job is not null&&!job.IsDeleted&&job.Id==invoice.JobId&&job.CustomerId==invoice.CustomerId&&job.PartitionKey==partition&&PortalAccessService.Allows(actor,"job",job.Id,job.CustomerId,job.JobSiteId);
    }
    public async Task<PortalInvoiceView[]> ListAsync(PortalActor actor,CancellationToken ct=default)
    {
        if(!actor.Configuration.FinanceEnabled)throw new KeyNotFoundException();
        var s=await store.ReadAsync(actor.TenantId,ct);var result=new List<PortalInvoiceView>();var partition=RepositoryKeyHelper.ToTenantPartitionKey(actor.TenantId);
        foreach(var item in s.OpenItems.Where(i=>i.Kind=="AR"))
        {
            var invoice=await invoices.GetAsync(partition,RepositoryKeyHelper.ToRowKey(item.Id),ct);if(invoice is null)continue;
            var job=invoice.JobId.HasValue?await jobs.GetAsync(partition,RepositoryKeyHelper.ToRowKey(invoice.JobId.Value),ct):null;
            if(invoice.CustomerId!=item.PartyId||!AllowsInvoice(actor,invoice,job))continue;
            var receipts=s.Settlements.SelectMany(p=>p.Allocations.Where(a=>a.OpenItemId==item.Id).Select(a=>new PortalReceiptView(p.Date,p.Kind,a.Amount,p.Reference))).ToArray();
            var url=actor.Configuration.FinancePaymentsEnabled?SafePaymentUrl(invoice.StripePaymentUrl):null;
            result.Add(new(item.Id,item.Number,item.Date,item.DueDate,item.Total,FinanceLedger.Balance(s,item,DateOnly.FromDateTime(DateTime.UtcNow)),item.JobId,receipts,url));
        }
        return result.ToArray();
    }
    public static string? SafePaymentUrl(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var url)&&url.Scheme=="https"&&string.IsNullOrEmpty(url.UserInfo)&&url.Host is "checkout.stripe.com" or "buy.stripe.com" or "invoice.stripe.com"?url.AbsoluteUri:null;
}
