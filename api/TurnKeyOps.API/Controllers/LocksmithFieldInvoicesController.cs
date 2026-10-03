using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/locksmith/field-invoices")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LocksmithFieldInvoicesController(
    IInvoiceService invoices,
    IQuoteRequestRepository requests,
    IJobRepository jobs,
    ITenantMembershipRepository memberships,
    ITenantSettingsRepository settings,
    IUserContext user) : ApiControllerBase
{
    private string Partition => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
    private static string Row(Guid id) => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(id);

    private async Task<(TenantMembership Member, string[] Capabilities)?> ContextAsync(CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty || user.UserId == Guid.Empty) return null;
        var member = await memberships.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(user.TenantId), user.UserId, ct);
        if (member is null || member.TenantId != user.TenantId || member.UserId != user.UserId || member.IsDeleted || member.DateRemoved.HasValue ||
            !string.Equals(member.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase) || string.Equals(member.Role, "contact", StringComparison.OrdinalIgnoreCase)) return null;
        var document = await settings.GetAsync(Partition, "SETTINGS|OPERATIONAL", ct);
        var capabilities = document is null || document.IsDeleted ? [] : LocksmithPolicy.CapabilitiesFor(document.ValuesJson, member.Id);
        return capabilities.Length == 0 ? null : (member, capabilities);
    }

    private async Task<bool> CanAccessAsync(InvoiceDto invoice, TenantMembership member, string[] capabilities, IReadOnlyCollection<Job> tenantJobs, CancellationToken ct)
    {
        if (invoice.QuoteRequestId is not Guid requestId || requestId == Guid.Empty) return false;
        var request = await requests.GetAsync(Partition, Row(requestId), ct);
        if (request is null || request.IsDeleted || request.TenantId != user.TenantId || !capabilities.Contains(request.PropertyType.ToLowerInvariant())) return false;
        var linked = tenantJobs.Where(job => !job.IsDeleted && job.PartitionKey == Partition &&
            (job.Id == invoice.JobId || job.InvoiceId == invoice.Id || job.QuoteRequestId == requestId)).ToArray();
        if (invoice.JobId.HasValue && !linked.Any(job => job.Id == invoice.JobId && job.QuoteRequestId == requestId)) return false;
        return linked.Length > 0 && linked.All(job => (string.IsNullOrWhiteSpace(job.LocksmithJobType) || string.Equals(job.LocksmithJobType, request.PropertyType, StringComparison.OrdinalIgnoreCase)) &&
            (!job.AssignedTechnicianMembershipId.HasValue || job.AssignedTechnicianMembershipId.Value == member.Id));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var context = await ContextAsync(ct);
        if (context is null) return Forbid();
        var tenantJobs = await jobs.ListAsync(Partition, ct);
        var results = new List<InvoiceDto>();
        string? token = null;
        var seenTokens = new HashSet<string>();
        do
        {
            ct.ThrowIfCancellationRequested();
            var page = await invoices.GetPagedAsync(100, token);
            foreach (var invoice in page.Items)
                if (await CanAccessAsync(invoice, context.Value.Member, context.Value.Capabilities, tenantJobs, ct)) results.Add(invoice);
            token = page.ContinuationToken;
            if (token is not null && !seenTokens.Add(token)) throw new InvalidOperationException("Invoice pagination did not advance.");
        } while (token is not null);
        return OkResponse(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var context = await ContextAsync(ct);
        if (context is null) return Forbid();
        var invoice = await invoices.GetAsync(id);
        if (invoice is null || !await CanAccessAsync(invoice, context.Value.Member, context.Value.Capabilities, await jobs.ListAsync(Partition, ct), ct)) return NotFound();
        return OkResponse(invoice);
    }

    [HttpPost("{id:guid}/completion-signature")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] InvoiceCompletionSignatureInputDto input, CancellationToken ct)
    {
        var context = await ContextAsync(ct);
        if (context is null) return Forbid();
        var invoice = await invoices.GetAsync(id);
        if (invoice is null || !await CanAccessAsync(invoice, context.Value.Member, context.Value.Capabilities, await jobs.ListAsync(Partition, ct), ct)) return NotFound();
        return OkResponse(await invoices.RecordCompletionSignatureAsync(id, input, ct));
    }
}
