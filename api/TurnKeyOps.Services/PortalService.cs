using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using MedInsights.AzureServices.Interfaces;
namespace TurnKeyOps.Services;

public sealed partial class PortalService(IJobRepository jobs, ILeadRepository leads, ICalendarEventRepository calendar,
    IQuoteEstimateRepository estimateRows, QuoteEstimateService estimates, IJobWorkflowPayloadStore payloads,
    PortalAccessService access, IAzureBlobStorageService blobs)
{
    private static string Partition(PortalActor a) => RepositoryKeyHelper.ToTenantPartitionKey(a.TenantId);
    public static string Version(Job j) => string.IsNullOrWhiteSpace(j.ETag.ToString()) ? j.DateUpdated.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) : j.ETag.ToString();
    private async Task<Job> Job(PortalActor a, Guid id, CancellationToken ct)
    {
        var j = await jobs.GetAsync(Partition(a), RepositoryKeyHelper.ToRowKey(id), ct);
        if (j is null || j.Id != id || j.IsDeleted || j.PartitionKey != Partition(a) || !PortalAccessService.Allows(a, "job", id, j.CustomerId, j.JobSiteId)) throw new KeyNotFoundException();
        return j;
    }
    private async Task<Lead> Lead(PortalActor a, Guid id, CancellationToken ct)
    {
        var l = await leads.GetAsync(Partition(a), RepositoryKeyHelper.ToRowKey(id), ct);
        if (l is null || l.Id != id || l.IsDeleted || l.PartitionKey != Partition(a) || !PortalAccessService.Allows(a, "lead", id, l.Data.CustomerId)) throw new KeyNotFoundException();
        return l;
    }
    public async Task<Guid> RequireContextAsync(PortalActor a, string kind, Guid id, CancellationToken ct)
    {
        return kind switch {
            "job" => (await Job(a, id, ct)).CustomerId,
            "lead" => (await Lead(a, id, ct)).Data.CustomerId!.Value,
            "estimate" => (await estimates.PortalPacketAsync(a, id, ct)).CustomerId!.Value,
            "customer" when PortalAccessService.Allows(a, kind, id, id) => id,
            _ => throw new KeyNotFoundException()
        };
    }
    public async Task<object> HomeAsync(PortalActor a, CancellationToken ct)
    {
        var work = new List<PortalWork>();
        var recent = new List<object>();
        foreach (var j in await jobs.ListAsync(Partition(a), ct)) {
            if (j.IsDeleted || j.PartitionKey != Partition(a) || !PortalAccessService.Allows(a, "job", j.Id, j.CustomerId, j.JobSiteId)) continue;
            var state = JobExecutionRules.State(j.Status);
            work.Add(new("job", j.Id, j.CustomerId, j.JobSiteId, j.Name, a.Configuration.JobLabels.GetValueOrDefault(state, PortalRules.JobStatus(state)), PortalRules.NextStep(state), Version(j), j.JobSiteName));
            var payload=await LoadPayload(a,j,ct);
            foreach(var update in payload.Activity.Where(v=>v.CustomerVisible).OrderByDescending(v=>v.OccurredAtUtc).Take(5))recent.Add(new{kind="job",id=j.Id,title=j.Name,text=update.Label,atUtc=update.OccurredAtUtc});
        }
        foreach (var l in await leads.ListAsync(Partition(a), ct)) {
            if (l.IsDeleted || l.PartitionKey != Partition(a) || !PortalAccessService.Allows(a, "lead", l.Id, l.Data.CustomerId)) continue;
            work.Add(new("lead", l.Id, l.Data.CustomerId, null, l.Data.Title, PortalRules.LeadStatus(l.Data.Stage), "We will contact you about the next step.", l.ETag.ToString()));
        }
        foreach (var row in await estimateRows.ListAsync(Partition(a), ct)) {
            if (row.IsDeleted || row.PartitionKey != Partition(a)) continue;
            try {
                var p = await estimates.PortalPacketAsync(a, row.QuoteRequestId, ct);
                work.Add(new("estimate", p.QuoteRequestId, p.CustomerId, p.Document?.SiteId, p.ServiceSummary, p.ApprovalSignature is not null ? "Approved" : p.Outcome is not null || p.ExpiresAtUtc <= DateTime.UtcNow ? "Closed proposal" : p.Delivery?.Status == "sent" ? "Ready for review" : "Response received", "Review the issued proposal and its terms.", p.DocumentHash, p.SiteName));
            } catch (KeyNotFoundException) { }
        }
        var appointments = new List<PortalAppointment>();
        foreach (var e in await calendar.ListAsync(Partition(a), ct)) if (await CanSeeEvent(a, e, ct)) appointments.Add(EventView(e));
        return new { configuration = a.Configuration, work, recent, appointments = appointments.OrderBy(e => e.StartUtc),
            contexts = work.Where(w => w.SiteId.HasValue).Select(w => w.SiteId).Distinct(), userId = a.UserId, notificationChannels = await access.PreferencesAsync(a,ct) };
    }
    public async Task<object> WorkAsync(PortalActor a, string kind, Guid id, CancellationToken ct)
    {
        if (kind == "estimate") { var result = await estimates.PortalProposalAsync(a, id, ct); await access.Audit(a.TenantId, a.UserId, "proposal.viewed", id, ct); return result; }
        if (kind == "lead") { var l = await Lead(a,id,ct); return new { id, title = l.Data.Title, status = PortalRules.LeadStatus(l.Data.Stage), scope = l.Data.RequestedWork }; }
        var j = await Job(a,id,ct); var p = await LoadPayload(a,j,ct); var x = p.Execution;
        var state = JobExecutionRules.State(j.Status);
        return new { id, title = j.Name, version = Version(j), site = j.JobSiteName, status = a.Configuration.JobLabels.GetValueOrDefault(state,PortalRules.JobStatus(state)), nextStep = PortalRules.NextStep(state),
            scope = x?.SoldScope ?? p.AcceptedEstimate?.Document?.Scope ?? "", acceptedProposal = p.AcceptedEstimate is null ? null : new { p.AcceptedEstimate.Revision, p.AcceptedEstimate.DocumentHash, scope=p.AcceptedEstimate.Document?.Scope,terms=p.AcceptedEstimate.Document?.Terms,exclusions=p.AcceptedEstimate.Document?.Exclusions,timing=p.AcceptedEstimate.Document?.Timing, options=p.AcceptedEstimate.SelectedOptions.Select(o=>new{o.Name,o.Total}),signer=p.AcceptedEstimate.Signature?.SignerPrintedName,signedAtUtc=p.AcceptedEstimate.Signature?.SignedAtUtc },
            updates = p.Activity.Where(v => v.CustomerVisible).Select(v => new PortalUpdate(v.Id,v.Label,v.OccurredAtUtc)),
            files = (x?.Evidence.Where(f=>f.CustomerVisible).Select(f=>new PortalFile(f.Id,f.Name,f.ContentType))??[]).Concat(p.AcceptedEstimate?.Document?.Attachments.Select(f=>new PortalFile(f.Id,f.Name,f.ContentType))??[]),
            changes = x?.Changes.Where(c=>c.CustomerVisible).Select(c=>new { c.Id,c.Description,c.Reason,c.ScopeImpact,c.ScheduleImpact,c.PricingImpact,c.Status,hash=PortalRules.ChangeHash(c),c.AcceptedEstimateId,c.AcceptedRevision,c.AcceptedDocumentHash }),
            issues = x?.Issues.Where(i=>i.PortalUserId.HasValue).Select(i=>new { i.Id,i.Description,i.CreatedAtUtc,status=i.ResolvedAtUtc.HasValue?"Resolved":"Under review" }),
            completion = x is null ? null : new { ready = a.Configuration.CompletionAcceptanceEnabled && state is "COMPLETION_REVIEW" or "COMPLETED" && JobExecutionRules.Blockers(x,"complete").Count==0 && !x.Acceptances.Any(v=>v.CompletionRevision==x.CompletionRevision),
                revision=x.CompletionRevision,hash=PortalRules.CompletionHash(x),statement=PortalRules.CompletionStatement,
                acceptedAtUtc=x.Acceptances.LastOrDefault(v=>v.CompletionRevision==x.CompletionRevision)?.AcceptedAtUtc, x.Profile.AllowQualifiedAcceptance },
            warrantyEndsAtUtc = x?.WarrantyEndsAtUtc };
    }
    private async Task<JobWorkflowPayloadDto> LoadPayload(PortalActor a, Job j, CancellationToken ct)
    {
        if (j.WorkflowPayloadBlobName is not null && !j.WorkflowPayloadBlobName.StartsWith($"{a.TenantId:N}/{j.Id:N}/",StringComparison.Ordinal)) throw new KeyNotFoundException();
        return await payloads.LoadAsync(j.WorkflowPayloadBlobName,ct);
    }
    private async Task<bool> CanSeeEvent(PortalActor a, CalendarEvent e, CancellationToken ct)
    {
        if(e.IsDeleted || !e.CustomerVisible || e.PartitionKey != Partition(a)) return false;
        try { if(e.JobId.HasValue) { await Job(a,e.JobId.Value,ct); return true; } if(e.LeadId.HasValue) { await Lead(a,e.LeadId.Value,ct); return true; } } catch(KeyNotFoundException) { }
        return false;
    }
    private static PortalAppointment EventView(CalendarEvent e) => new(e.Id,e.JobId,e.JobId.HasValue?null:e.LeadId,e.StartUtc,e.EndUtc,e.EventStatus,e.CustomerResponse,e.ETag.ToString(),e.CustomerSlots.Where(s=>s.StartUtc>DateTime.UtcNow).ToArray());
    public async Task<object> AppointmentAsync(PortalActor a, Guid id, PortalCommand input, CancellationToken ct)
    {
        var e=await calendar.GetAsync(Partition(a),RepositoryKeyHelper.ToRowKey(id),ct);
        if(e is null || !await CanSeeEvent(a,e,ct)) throw new KeyNotFoundException();
        PortalRules.ExactVersion(input.ExpectedVersion,e.ETag.ToString());
        if(e.EventStatus is not ("scheduled" or "offered") || e.EndUtc <= DateTime.UtcNow) throw new ArgumentException("This appointment is no longer available for a response.");
        if(input.Action is not ("confirmed" or "declined" or "reschedule-requested")) throw new ArgumentException("Choose an appointment response.");
        if(input.SlotId.HasValue){
            if(!a.Configuration.SelfBookingEnabled||input.Action!="confirmed")throw new ArgumentException("Booking is not enabled for this response.");
            var slot=e.CustomerSlots.SingleOrDefault(s=>s.Id==input.SlotId&&s.StartUtc>DateTime.UtcNow)??throw new ArgumentException("Choose a current offered time.");
            e.StartUtc=slot.StartUtc;e.EndUtc=slot.EndUtc;e.EventStatus="scheduled";e.CustomerSlots=[];
        }else if(e.EventStatus=="offered"&&input.Action=="confirmed")throw new ArgumentException("Choose an offered appointment time.");
        e.CustomerResponse=input.Action; e.CustomerResponseUserId=a.UserId; e.CustomerRespondedAtUtc=DateTime.UtcNow;
        e.CustomerResponseNote=string.IsNullOrWhiteSpace(input.Text)?"":PortalRules.Text(input.Text,2000);
        // All saves use the shared CalendarReservationStore, revalidating resource availability atomically.
        var saved=await calendar.SaveAsync(e,ct);
        await access.Audit(a.TenantId,a.UserId,"appointment."+input.Action,id,ct);
        return EventView(saved);
    }
    public async Task<object> ExplainAsync(PortalActor a, string kind, Guid id, CancellationToken ct)
    {
        if(!a.Configuration.CustomerBobEnabled) throw new KeyNotFoundException();
        var visible=await WorkAsync(a,kind,id,ct);
        // Customer Bob is a bounded explanation tool; it has no employee tools or private retrieval context.
        var data=System.Text.Json.JsonSerializer.SerializeToElement(visible,JobConfigurationService.Json);
        string Value(string key)=>data.TryGetProperty(key,out var v)&&v.ValueKind==System.Text.Json.JsonValueKind.String?v.GetString()??"":"";
        var explanation=kind=="estimate"?$"This is proposal revision {data.GetProperty("revisionNumber")}. Review the scope, options, exclusions and terms before signing. Your approval applies to this exact revision. You can also request a change or ask a question. Shared scope: {Value("scope")}":$"Current status: {Value("status")}. {Value("nextStep")} Shared scope: {Value("scope")}. Use Messages to ask your contractor a question; use Report issue if work needs correction.";
        return new { explanation, sharedDetails=visible };
    }
}
