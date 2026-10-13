using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
namespace TurnKeyOps.Services;
public sealed record PortalInvoiceView(Guid Id,string Number,DateOnly Date,DateOnly DueDate,decimal Total,decimal Balance,Guid? JobId,PortalReceiptView[] Receipts,string? PaymentUrl,DateOnly? HistoryFrom=null);
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
        var asOf=DateOnly.FromDateTime(DateTime.UtcNow);
        foreach(var item in s.OpenItems.Where(i=>i.Kind=="AR"&&i.Date<=asOf))
        {
            var invoice=await invoices.GetAsync(partition,RepositoryKeyHelper.ToRowKey(item.Id),ct);
            if(invoice is null||invoice.Id!=item.Id)
            {
                var construction=s.Construction.Applications.Any(a=>a.InvoiceId==item.Id&&a.JournalId==item.JournalId)||s.Construction.RetainageReleases.Any(r=>r.OpenItemId==item.Id&&r.JournalId==item.JournalId);
                if(!construction||item.JobId is null)continue;
                var sourceJob=await jobs.GetAsync(partition,RepositoryKeyHelper.ToRowKey(item.JobId.Value),ct);
                if(sourceJob is null||sourceJob.IsDeleted||sourceJob.PartitionKey!=partition||sourceJob.CustomerId!=item.PartyId||!PortalAccessService.Allows(actor,"job",sourceJob.Id,sourceJob.CustomerId,sourceJob.JobSiteId))continue;
                var constructionReceipts=s.Settlements.Where(p=>p.Date<=asOf).SelectMany(p=>p.Allocations.Where(a=>a.OpenItemId==item.Id).Select(a=>new PortalReceiptView(p.Date,p.Kind,a.Amount,p.Reference))).ToArray();
                result.Add(new(item.Id,item.Number,item.Date,item.DueDate,item.Total,FinanceLedger.Balance(s,item,asOf),item.JobId,constructionReceipts,actor.Configuration.FinancePaymentsEnabled?SafePaymentUrl(s.ConstructionPayments.FirstOrDefault(p=>p.OpenItemId==item.Id&&p.Status=="READY"&&p.Amount==FinanceLedger.Balance(s,item,DateOnly.MaxValue))?.Url):null));continue;
            }
            var job=invoice.JobId.HasValue?await jobs.GetAsync(partition,RepositoryKeyHelper.ToRowKey(invoice.JobId.Value),ct):null;
            if(invoice.CustomerId!=item.PartyId||!AllowsInvoice(actor,invoice,job))continue;
            var receipts=s.Settlements.Where(p=>p.Date<=asOf).SelectMany(p=>p.Allocations.Where(a=>a.OpenItemId==item.Id).Select(a=>new PortalReceiptView(p.Date,p.Kind,a.Amount,p.Reference))).ToArray();
            var url=actor.Configuration.FinancePaymentsEnabled?SafePaymentUrl(invoice.StripePaymentUrl):null;
            var cutover=s.MigrationBatches.SingleOrDefault(b=>b.JournalId==item.JournalId)?.CutoverDate;
            result.Add(new(item.Id,string.IsNullOrWhiteSpace(invoice.InvoiceNumber)?item.Number:invoice.InvoiceNumber,invoice.IssueDate==default?item.Date:DateOnly.FromDateTime(invoice.IssueDate),invoice.DueDate==default?item.DueDate:DateOnly.FromDateTime(invoice.DueDate),invoice.Total,FinanceLedger.Balance(s,item,asOf),invoice.JobId,receipts,url,cutover));
        }
        return result.ToArray();
    }
    public async Task<object> ConstructionAsync(PortalActor actor,CancellationToken ct=default)
    {
        if(!actor.Configuration.FinanceEnabled)throw new KeyNotFoundException();
        var s=await store.ReadAsync(actor.TenantId,ct);var visible=new List<object>();
        foreach(var app in s.Construction.Applications.Where(a=>a.Status is "IN_REVIEW" or "APPROVED" or "INVOICED"))
        {
            var job=await jobs.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(actor.TenantId),RepositoryKeyHelper.ToRowKey(app.JobId),ct);
            var contract=s.Construction.Contracts.SingleOrDefault(c=>c.JobId==app.JobId);
            if(job is null||job.IsDeleted||contract is null||contract.CustomerId!=job.CustomerId||job.PartitionKey!=RepositoryKeyHelper.ToTenantPartitionKey(actor.TenantId)||!PortalAccessService.Allows(actor,"job",job.Id,job.CustomerId,job.JobSiteId))continue;
            visible.Add(new{app.Id,app.JobId,app.Number,app.Status,app.PeriodStart,app.PeriodEnd,app.PriorBilled,app.CurrentBilling,app.Retainage,app.TotalDue,app.InvoiceId,Lines=app.Lines.Select(l=>new{Description=contract.Lines.Single(v=>v.Id==l.SovLineId).Description,l.CurrentBilling,l.NewStoredMaterials,l.InstalledFromStored,l.BillableAmount}).ToArray(),CustomerApproved=s.Construction.CustomerApprovals.Any(a=>a.ApplicationId==app.Id)});
        }
        return new{Version=s.Version,Applications=visible};
    }
    public async Task ApproveConstructionAsync(PortalActor actor,Guid applicationId,ConstructionPortalApproval input,CancellationToken ct=default)
    {
        if(!actor.Configuration.FinanceEnabled)throw new KeyNotFoundException();
        FinanceLedger.Required(input.Name);if(input.Id==Guid.Empty)throw new ArgumentException("Approval identity required.");
        var s=await store.ReadAsync(actor.TenantId,ct);var app=s.Construction.Applications.SingleOrDefault(a=>a.Id==applicationId&&a.Status is "IN_REVIEW" or "APPROVED")??throw new KeyNotFoundException();
        var job=await jobs.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(actor.TenantId),RepositoryKeyHelper.ToRowKey(app.JobId),ct);
        var contract=s.Construction.Contracts.Single(c=>c.JobId==app.JobId);
        if(job is null||job.IsDeleted||job.CustomerId!=contract.CustomerId||job.PartitionKey!=RepositoryKeyHelper.ToTenantPartitionKey(actor.TenantId)||!PortalAccessService.Allows(actor,"job",job.Id,job.CustomerId,job.JobSiteId))throw new KeyNotFoundException();
        var existing=s.Construction.CustomerApprovals.SingleOrDefault(a=>a.Id==input.Id);
        if(existing!=null){if(existing.ApplicationId!=applicationId||existing.Actor!=actor.UserId||existing.Name!=input.Name)throw new ArgumentException("Approval identity conflict.");return;}
        if(s.Version!=input.ExpectedVersion)throw new InvalidOperationException("Finance changed. Refresh before approving.");
        if(s.Construction.CustomerApprovals.Any(a=>a.ApplicationId==applicationId))throw new ArgumentException("Application already acknowledged.");
        s.Construction.CustomerApprovals.Add(new(input.Id,applicationId,actor.UserId,DateTime.UtcNow,input.Name));
        s.Audit.Add(new(input.Id,"construction.customer-approval",applicationId.ToString(),actor.UserId.ToString(),DateTime.UtcNow,input.Name,FinanceLedger.Hash(new{applicationId,input.Id,input.Name,actor.UserId})));
        await store.SaveAsync(actor.TenantId,s,input.ExpectedVersion,ct);
    }
    public static string? SafePaymentUrl(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var url)&&url.Scheme=="https"&&string.IsNullOrEmpty(url.UserInfo)&&url.Host is "checkout.stripe.com" or "buy.stripe.com" or "invoice.stripe.com"?url.AbsoluteUri:null;
}
