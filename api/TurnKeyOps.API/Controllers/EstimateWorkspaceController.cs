using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.AuthenticatedSession)]
[Route("api/estimate-workspace")]
[LeadErrors]
public sealed class EstimateWorkspaceController(QuoteEstimateService service,IEstimateAuthority authority,
    LeadService leads,EstimateDeliveryService delivery,IBobOperationsService bob,
    ITenantSettingsService settings):ApiControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)
    {
        await authority.RequireAsync(false,ct);
        var packets=await service.ListAsync(ct);var owner=await authority.CanApproveAsync(ct);var policy=await service.PricingPolicyAsync(ct);
        return OkResponse(new {packets=packets.Select(x=>new {x.Id,x.LeadId,x.CustomerName,x.SiteName,x.RevisionNumber,total=x.Totals.EstimatedTotal,state=QuoteEstimateService.State(x),stateLabel=policy.StageLabels.GetValueOrDefault(QuoteEstimateService.State(x)),trade=x.Document?.TradeProfile,modern=x.Document is not null}),canWrite=await authority.CanWriteAsync(ct),canConfigure=owner});
    }
    [HttpGet("metrics")] public async Task<IActionResult> Metrics(CancellationToken ct)=>OkResponse(await service.MetricsAsync(ct));
    [HttpGet("configuration")] public async Task<IActionResult> Configuration(CancellationToken ct)
    {await authority.RequireAsync(false,ct);if(!await authority.CanApproveAsync(ct))return Forbid();return OkResponse(new {policy=await service.PricingPolicyAsync(ct),settings=await settings.GetProtectedAsync("operational",ct)});}
    [HttpPut("configuration")] public async Task<IActionResult> Configure(EstimatePolicyUpdate input,CancellationToken ct)
    {
        await authority.RequireAsync(true,ct);if(!await authority.CanApproveAsync(ct))return Forbid();
        EstimatePricingEngine.Validate(input.Policy);
        return OkResponse(await service.UpdatePricingPolicyAsync(input.Policy,input.ExpectedVersion,settings,ct));
    }
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>OkResponse(await service.WorkspaceAsync(id,ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Price(Guid id,EstimateWorkspaceInputDto input,CancellationToken ct)
    {await service.PriceWorkspaceAsync(id,input,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
    [HttpPost("{id:guid}/extract")] public async Task<IActionResult> Extract(Guid id,EstimateTaskDto input,CancellationToken ct)
    {await service.StructureAsync(id,input,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
    [HttpPost("{id:guid}/approve")] public async Task<IActionResult> Approve(Guid id,EstimateTaskDto input,CancellationToken ct)
    {await service.ApproveWorkspaceAsync(id,input.ExpectedVersion,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
    [HttpPost("{id:guid}/issue")] public async Task<IActionResult> Issue(Guid id,EstimateTaskDto input,CancellationToken ct)
    {await service.IssueAsync(id,input.ExpectedVersion,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
    [HttpPost("{id:guid}/revision")] public async Task<IActionResult> Revise(Guid id,EstimateTaskDto input,CancellationToken ct)
    {await authority.RequireAsync(true,ct);await service.CreateRevisionAsync(id,input.ExpectedVersion,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
    [HttpPost("{id:guid}/void")] public async Task<IActionResult> Void(Guid id,EstimateTaskDto input,CancellationToken ct)
    {await service.VoidAsync(id,input.ExpectedVersion,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
    [HttpPost("{id:guid}/deliver")] public async Task<IActionResult> Deliver(Guid id,EstimateTaskDto input,CancellationToken ct)
    {await service.DeliverAsync(id,input,delivery,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
    [HttpPost("{id:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50*1024*1024)]
    public async Task<IActionResult> Upload(Guid id,[FromForm]string expectedVersion,[FromForm]List<IFormFile> files,[FromServices]IQuoteRequestAttachmentService attachments,CancellationToken ct)
    {
        var uploads=QuoteRequestAttachmentHttpMapper.Map(files);
        try {await service.UploadScopeFilesAsync(id,expectedVersion,uploads,attachments,ct);return OkResponse(await service.WorkspaceAsync(id,ct));}
        finally{QuoteRequestAttachmentHttpMapper.Dispose(uploads);}
    }
    [HttpPost("{id:guid}/job")] public async Task<IActionResult> Job(Guid id,EstimateTaskDto input,CancellationToken ct)
    {
        await authority.RequireAsync(true,ct);var packet=await service.GetAsync(id,ct);
        if(packet?.LeadId is not Guid leadId)return BadRequest("A linked Lead is required.");
        if(packet.Version!=input.ExpectedVersion)throw new ArgumentException("The estimate changed. Reload before Job handoff.");
        await service.ReconcileDecisionAsync(id,ct);
        var lead=await leads.GetAsync(leadId,ct)??throw new KeyNotFoundException();
        var converted=await leads.ConvertAsync(leadId,lead.Version,ct);
        if(converted.JobId.HasValue)await service.RecordJobHandoffAsync(id,converted.JobId.Value,ct);
        return OkResponse(converted);
    }
    [HttpPost("{id:guid}/bob")] public async Task<IActionResult> Bob(Guid id,ProposeBobActionDto input,CancellationToken ct)
    {await service.WorkspaceAsync(id,ct);return OkResponse(await bob.ProposeEstimateAsync(id,input,ct));}
    [HttpPost("{id:guid}/bob/{actionId:guid}/approve")] public async Task<IActionResult> ApproveBob(Guid id,Guid actionId,CancellationToken ct)
    {await service.WorkspaceAsync(id,ct);return OkResponse(await bob.ApproveEstimateAsync(id,actionId,ct));}
}
public sealed class EstimatePolicyUpdate{public string ExpectedVersion {get;set;}="";public EstimatePricingPolicyDto Policy {get;set;}=new();}
