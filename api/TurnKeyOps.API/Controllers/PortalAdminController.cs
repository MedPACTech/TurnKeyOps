using MedInsights.Lib.Authorization;
using MedInsights.Services;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.API.Controllers;

[ApiController,Route("api/admin/portal"),Authorize(Policy=TurnKeyAuthorizationPolicies.TenantAdmin),PortalErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PortalAdminController(IPortalAccessStore store,UserModuleAccessService modules,IUserContext user,IUserProfileRepository profiles,
    ICustomerRepository customers,IJobRepository jobs,ILeadRepository leads,IJobSiteRepository sites,IJobWorkflowPayloadStore payloads,
    IQuoteEstimateService estimates,PortalAccessService access,PortalMessages messages,ICalendarEventRepository calendar,PortalNotifications notifications,PortalService portal):ControllerBase
{
    private string Partition=>RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
    private async Task Require(CancellationToken ct){if(!await modules.IsOwnerAsync(ct)||!UserModulePermissions.Allows(await modules.GetAsync(ct),"settings",!HttpMethods.IsGet(Request.Method)))throw new UnauthorizedAccessException();}
    private async Task RequireContextModule(string kind,bool write,CancellationToken ct)
    {
        await Require(ct);var module=kind switch{"job"=>"jobs","lead"=>"leads","estimate"=>"estimates","customer"=>"contacts",_=>throw new KeyNotFoundException()};
        if(!UserModulePermissions.Allows(await modules.GetAsync(ct),module,write))throw new MedInsights.Lib.ForbiddenAccessException("This module is not available to you.");
    }
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await Require(ct);var state=await store.ReadAsync(user.TenantId,ct);var people=new List<object>();
        string? cursor=null; do {var page=await profiles.GetByPartitionPagedAsync(Partition,200,cursor,ct); foreach(var p in page.Results)if(p.PartitionKey==Partition&&!p.IsDeleted&&p.IsActive&&p.CustomerId.HasValue)people.Add(new{userId=p.ApplicationUserId,p.FirstName,p.LastName,p.CustomerId}); cursor=page.ContinuationToken;}while(cursor is not null);
        return Ok(new{state.Version,state.Configuration,state.Grants,people});
    }
    public sealed record Configure(string ExpectedVersion,PortalConfiguration Configuration);
    [HttpPut("configuration")]
    public async Task<IActionResult> Configuration(Configure input,CancellationToken ct)
    {
        await Require(ct);var s=await store.ReadAsync(user.TenantId,ct);if(s.Version!=input.ExpectedVersion)throw new InvalidOperationException();
        var c=input.Configuration;c.CompanyName=PortalRules.Text(c.CompanyName,100);
        if(c.LogoPath.Length>200||c.LogoPath.Length>0&&(!c.LogoPath.StartsWith("/")||c.LogoPath.StartsWith("//")||c.LogoPath.Contains("\\")||c.LogoPath.Any(char.IsControl))||c.ContactInfo.Length>500||c.Accent is not ("teal" or "blue" or "violet")||c.NotificationTemplates.Count>20||c.NotificationTemplates.Any(t=>t.Key.Length>50||string.IsNullOrWhiteSpace(t.Value)||t.Value.Length>2000)||c.JobLabels.Any(l=>!JobExecutionRules.States.ContainsKey(l.Key)||l.Value.Length>100||string.IsNullOrWhiteSpace(l.Value)))throw new ArgumentException("Use a supported accent and valid customer labels.");
        s.Configuration=c;await store.SaveAsync(user.TenantId,s,s.Version,ct);await access.Audit(user.TenantId,user.UserId,"configuration.updated",null,ct);return Ok();
    }
    public sealed record GrantInput(string ExpectedVersion,PortalGrant Grant);
    [HttpPost("grants")]
    public async Task<IActionResult> Grant(GrantInput input,CancellationToken ct)
    {
        await Require(ct);var s=await store.ReadAsync(user.TenantId,ct);if(s.Version!=input.ExpectedVersion)throw new InvalidOperationException();
        var g=input.Grant;var p=await profiles.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(g.UserId),ct);
        if(!PortalAccessService.ProfileAllows(p,user.TenantId,g.UserId,g.CustomerId))throw new ArgumentException("Choose an active Customer contact linked to this customer.");
        var customer=await customers.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(g.CustomerId),ct);
        if(customer is null||customer.IsDeleted||customer.PartitionKey!=Partition||g.ExpiresAtUtc<=DateTime.UtcNow||g.ExpiresAtUtc>DateTime.UtcNow.AddYears(2))throw new ArgumentException("Choose a valid customer and expiry within two years.");
        var valid=false;
        switch(g.Scope){
            case "customer":valid=g.RecordId==g.CustomerId;break;
            case "site":var site=await sites.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(g.RecordId),ct);valid=site is {IsDeleted:false}&&site.PartitionKey==Partition;break;
            case "job":var j=await jobs.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(g.RecordId),ct);valid=j is {IsDeleted:false}&&j.PartitionKey==Partition&&j.CustomerId==g.CustomerId;break;
            case "lead":var l=await leads.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(g.RecordId),ct);valid=l is {IsDeleted:false}&&l.PartitionKey==Partition&&l.Data.CustomerId==g.CustomerId;break;
            case "estimate":var e=await estimates.GetAsync(g.RecordId,ct);valid=e?.CustomerId==g.CustomerId;break;
        }
        if(!valid)throw new ArgumentException("Select an existing customer, site, request, proposal or project.");
        if(s.Grants.Any(v=>!v.Revoked&&v.UserId==g.UserId&&v.CustomerId==g.CustomerId&&v.Scope==g.Scope&&v.RecordId==g.RecordId&&v.ExpiresAtUtc>DateTime.UtcNow))throw new ArgumentException("An active grant already exists.");
        g.Id=Guid.NewGuid();g.CreatedBy=user.UserId;g.CreatedAtUtc=DateTime.UtcNow;g.Revoked=false;s.Grants.Add(g);
        await store.SaveAsync(user.TenantId,s,s.Version,ct);await access.Audit(user.TenantId,user.UserId,"grant.created",g.Id,ct);return Ok(g);
    }
    [HttpPost("grants/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id,PortalCommand input,CancellationToken ct)
    {
        await Require(ct);var s=await store.ReadAsync(user.TenantId,ct);PortalRules.ExactVersion(input.ExpectedVersion,s.Version);
        var grant=s.Grants.SingleOrDefault(g=>g.Id==id)??throw new KeyNotFoundException();grant.Revoked=true;
        await store.SaveAsync(user.TenantId,s,s.Version,ct);await access.Audit(user.TenantId,user.UserId,"grant.revoked",id,ct);return Ok();
    }
    [HttpPost("contacts/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeContact(Guid id, PortalCommand input, CancellationToken ct)
    {
        await Require(ct);
        var person = await profiles.GetAsync(Partition, RepositoryKeyHelper.ToRowKey(id), ct);
        if (person is null || person.PartitionKey != Partition || person.ApplicationUserId != id) throw new KeyNotFoundException();
        await access.RevokeContactAsync(user.TenantId, user.UserId, id, input.ExpectedVersion, ct);
        return Ok();
    }
    [HttpPost("jobs/{id:guid}/share")]
    public async Task<IActionResult> Share(Guid id,PortalCommand input,CancellationToken ct)
    {
        await Require(ct);if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"jobs",true))return Forbid();
        var j=await jobs.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(id),ct);if(j is null||j.IsDeleted||j.PartitionKey!=Partition)throw new KeyNotFoundException();
        PortalRules.ExactVersion(input.ExpectedVersion,PortalService.Version(j));var p=await payloads.LoadAsync(j.WorkflowPayloadBlobName,ct);var x=p.Execution??throw new ArgumentException("Adopt Job execution first.");
        switch(input.Action){
            case "update":p.Activity.Add(new(){Id=Guid.NewGuid(),CustomerVisible=true,Type="customer-update",Label=PortalRules.Text(input.Text),Actor=user.UserId.ToString(),OccurredAtUtc=DateTime.UtcNow});break;
            case "file":var f=x.Evidence.SingleOrDefault(f=>f.Id==input.ItemId)??throw new KeyNotFoundException();f.CustomerVisible=input.Consent;break;
            case "change":var c=x.Changes.SingleOrDefault(c=>c.Id==input.ItemId)??throw new KeyNotFoundException();
                if(c.Status!="APPROVAL_REQUIRED")throw new ArgumentException("Complete internal review before sharing a change for approval.");
                if(c.PricingImpact){var e=await estimates.GetAsync(input.ChangeEstimateId??Guid.Empty,ct);if(e is null||e.SentAtUtc is null||e.CustomerId!=j.CustomerId||e.Document?.SiteId!=j.JobSiteId||e.QuoteRequestId==p.AcceptedEstimate?.EstimateId)throw new ArgumentException("Link the issued change proposal for this customer and site.");c.AcceptedEstimateId=e.QuoteRequestId;c.AcceptedRevision=e.RevisionNumber;c.AcceptedDocumentHash=e.DocumentHash;}
                c.CustomerVisible=input.Consent;break;
            default:throw new ArgumentException("Choose update, file or change.");
        }
        if(x.Acceptances.Any(v=>v.CompletionRevision==x.CompletionRevision)&&input.Action is "file" or "change")x.CompletionRevision++;
        p.PreviousVersionBlobName=j.WorkflowPayloadBlobName;j.WorkflowPayloadBlobName=await payloads.SaveAsync(user.TenantId,id,p,ct);j.DateUpdated=DateTime.UtcNow;await jobs.SaveAsync(j,ct);
        await access.Audit(user.TenantId,user.UserId,"visibility.updated",id,ct);return Ok();
    }
    public sealed record OfferInput(string ExpectedVersion,List<PortalSlot> Slots);
    [HttpPost("appointments/{id:guid}/offer")]
    public async Task<IActionResult> Offer(Guid id,OfferInput input,CancellationToken ct)
    {
        await Require(ct);if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"calendar",true))return Forbid();
        var e=await calendar.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(id),ct);
        if(e is null||e.IsDeleted||e.PartitionKey!=Partition||!e.CustomerVisible||e.JobId is null&&e.LeadId is null)throw new KeyNotFoundException();
        PortalRules.ExactVersion(input.ExpectedVersion,e.ETag.ToString());
        if(input.Slots.Count>12||input.Slots.Any(s=>s.Id==Guid.Empty||s.StartUtc.Kind!=DateTimeKind.Utc||s.EndUtc.Kind!=DateTimeKind.Utc||s.StartUtc<=DateTime.UtcNow||s.EndUtc<=s.StartUtc||s.EndUtc-s.StartUtc>TimeSpan.FromDays(1))||input.Slots.Select(s=>s.Id).Distinct().Count()!=input.Slots.Count)throw new ArgumentException("Offer up to twelve future UTC appointment windows.");
        e.CustomerSlots=input.Slots;await calendar.SaveAsync(e,ct);await access.Audit(user.TenantId,user.UserId,"appointment.options-shared",id,ct);return Ok();
    }
    public sealed record NotifyInput(Guid UserId,string Channel,string Template,string ExpectedVersion);
    [HttpPost("notify/{kind}/{id:guid}")]
    public async Task<IActionResult> Notify(string kind,Guid id,NotifyInput input,CancellationToken ct)
    {await RequireContextModule(kind,true,ct);return Ok(await notifications.SendAsync(user.TenantId,user.UserId,input.UserId,kind,id,input.ExpectedVersion,input.Channel,input.Template,ct));}
    [HttpGet("workspace/{customer:guid}")]
    public async Task<IActionResult> Workspace(Guid customer,CancellationToken ct){foreach(var kind in new[]{"job","lead","estimate"})await RequireContextModule(kind,false,ct);if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"calendar",false))return Forbid();return Ok(await portal.HomeAsync(await CustomerActor(customer,ct),ct));}
    [HttpGet("workspace/{customer:guid}/{kind}/{id:guid}")]
    public async Task<IActionResult> Work(Guid customer,string kind,Guid id,CancellationToken ct){await RequireContextModule(kind,false,ct);return Ok(await portal.WorkAsync(await CustomerActor(customer,ct),kind,id,ct));}
    [HttpGet("jobs/{id:guid}/share-options")]
    public async Task<IActionResult> ShareOptions(Guid id,CancellationToken ct)
    {
        await Require(ct);if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"jobs",false))return Forbid();
        var j=await jobs.GetAsync(Partition,RepositoryKeyHelper.ToRowKey(id),ct);if(j is null||j.IsDeleted||j.PartitionKey!=Partition)throw new KeyNotFoundException();
        var p=await payloads.LoadAsync(j.WorkflowPayloadBlobName,ct);
        return Ok(new{files=p.Execution?.Evidence.Select(f=>new{f.Id,f.Name,f.CustomerVisible}),changes=p.Execution?.Changes.Where(c=>c.Status=="APPROVAL_REQUIRED").Select(c=>new{c.Id,c.Description,c.CustomerVisible,c.PricingImpact})});
    }
    private async Task<PortalActor> CustomerActor(Guid customer,CancellationToken ct)
    {await Require(ct);var s=await store.ReadAsync(user.TenantId,ct);return new(user.TenantId,user.UserId,[new(){UserId=user.UserId,CustomerId=customer,Scope="customer",RecordId=customer}],s.Configuration);}
    [HttpGet("messages/{customer:guid}/{kind}/{id:guid}")]
    public async Task<IActionResult> Messages(Guid customer,string kind,Guid id,CancellationToken ct){await RequireContextModule(kind,false,ct);return Ok(await messages.ListAsync(await CustomerActor(customer,ct),kind,id,ct));}
    [HttpPost("messages/{customer:guid}/{kind}/{id:guid}")]
    public async Task<IActionResult> Reply(Guid customer,string kind,Guid id,PortalCommand input,CancellationToken ct){await RequireContextModule(kind,true,ct);return Ok(await messages.SendAsync(await CustomerActor(customer,ct),kind,id,input.Text,ct,true));}
}
