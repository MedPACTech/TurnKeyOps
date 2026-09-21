using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Utils;
using TurnKeyOps.Lib.Entities;

namespace MedInsights.Services;

public sealed record ContactJob(Guid Id, string Name, string Status, string? Address);
public sealed record ContactInvoice(Guid Id, string Number, string Status);
public sealed record ContactWork(ContactJob[] Jobs, ContactInvoice[] Invoices, bool CanViewJobs, bool CanViewInvoices);

public sealed class ContactWorkService(ManagedPeopleService people, UserModuleAccessService access, IUserContext user,
    IAzureTablesRepositoryStore<Job> jobs, IAzureTablesRepositoryStore<Invoice> invoices)
{
    public async Task<ContactWork> GetAsync(Guid id, CancellationToken ct)
    {
        var contact = await people.ContactAsync(id, ct);
        var permissions = await access.GetAsync(ct);
        var viewJobs = UserModulePermissions.Allows(permissions, "jobs", false);
        var viewInvoices = UserModulePermissions.Allows(permissions, "invoices", false);
        var jobRows = new List<ContactJob>(); var invoiceRows = new List<ContactInvoice>();
        if (contact.CustomerId is Guid customerId) {
            var partition = EntityKeyPolicy.TenantPartition(user.TenantId);
            if (viewJobs)
                await foreach (var job in jobs.QueryAsync(j => j.PartitionKey == partition, ct, "PartitionKey"))
                    if (!job.IsDeleted && job.CustomerId == customerId) jobRows.Add(new(job.Id, job.Name, job.Status.ToString(), job.ProjectAddress));
            if (viewInvoices)
                await foreach (var invoice in invoices.QueryAsync(i => i.PartitionKey == partition, ct, "PartitionKey"))
                    if (!invoice.IsDeleted && invoice.CustomerId == customerId) invoiceRows.Add(new(invoice.Id, invoice.InvoiceNumber, invoice.Status.ToString()));
        }
        return new(jobRows.OrderBy(j => j.Name).ToArray(), invoiceRows.OrderBy(i => i.Number).ToArray(), viewJobs, viewInvoices);
    }
}
