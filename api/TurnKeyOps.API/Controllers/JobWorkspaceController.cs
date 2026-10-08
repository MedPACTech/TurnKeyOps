using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/job-workspace")]
[LeadErrors]
public sealed class JobWorkspaceController(JobExecutionService service,IJobAuthority authority,JobConfigurationService configuration,IUserContext user,ITenantSettingsService settings,IBobOperationsService bob,JobDeliveryService delivery):ApiControllerBase
{
    [HttpGet("{id:guid}/notifications")]public async Task<IActionResult> Notifications(Guid id,CancellationToken ct)=>OkResponse(await delivery.ListAsync(id,ct));
    [HttpGet("choices")]public async Task<IActionResult> Choices(CancellationToken ct)=>OkResponse(await service.ChoicesAsync(ct));
    [HttpGet]public async Task<IActionResult> List(CancellationToken ct)=>OkResponse(await service.ListAsync(ct));
    [HttpGet("{id:guid}")]public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>OkResponse(await service.WorkspaceAsync(id,ct));
    [HttpPost]public async Task<IActionResult> Create(JobCreateDto input,CancellationToken ct)=>OkResponse(await service.CreateAsync(input,ct));
    [HttpPost("{id:guid}/command")]public async Task<IActionResult> Command(Guid id,JobCommandDto input,CancellationToken ct)=>OkResponse(await service.CommandAsync(id,input,ct));
    [HttpPost("{id:guid}/events")]public async Task<IActionResult> Schedule(Guid id,JobEventInputDto input,CancellationToken ct)=>OkResponse(await service.ScheduleAsync(id,input,ct));
    [HttpGet("{id:guid}/recommendations")]public async Task<IActionResult> Recommend(Guid id,DateTime start,DateTime end,CancellationToken ct)=>OkResponse(await service.RecommendAsync(id,start,end,ct));
    [HttpGet("metrics")]public async Task<IActionResult> Metrics(CancellationToken ct)=>OkResponse(await service.MetricsAsync(ct));
    [HttpPost("{id:guid}/files")][Consumes("multipart/form-data")][RequestSizeLimit(50*1024*1024)]
    public async Task<IActionResult> Upload(Guid id,[FromForm]string expectedVersion,[FromForm]List<IFormFile> files,CancellationToken ct,[FromForm]string purpose="field")
    {var uploads=QuoteRequestAttachmentHttpMapper.Map(files);try{return OkResponse(await service.UploadAsync(id,expectedVersion,uploads,ct,purpose));}finally{QuoteRequestAttachmentHttpMapper.Dispose(uploads);}}
    [HttpGet("{id:guid}/files/{file:guid}")]
    public async Task<IActionResult> Download(Guid id,Guid file,CancellationToken ct){var result=await service.DownloadAsync(id,file,ct);Response.Headers.CacheControl="private, no-store";Response.Headers.XContentTypeOptions="nosniff";return File(result.Content,result.ContentType,result.FileName);}
    [HttpGet("configuration")]public async Task<IActionResult> Configuration(CancellationToken ct)
    {await authority.RequireAsync(false,ct);if(!await authority.IsOwnerAsync(ct))return Forbid();return OkResponse(new{policy=await configuration.GetAsync(user.TenantId,ct),settings=await settings.GetProtectedAsync("operational",ct)});}
    [HttpPut("configuration")]public async Task<IActionResult> Configure(JobPolicyInput input,CancellationToken ct)
    {
        await authority.RequireAsync(true,ct);if(!await authority.IsOwnerAsync(ct))return Forbid();JobConfigurationService.Validate(input.Policy);
        return OkResponse(await configuration.UpdateAsync(user.TenantId,input.Policy,input.ExpectedVersion,settings,ct));
    }
    [HttpPost("{id:guid}/bob")]public async Task<IActionResult> Bob(Guid id,ProposeBobActionDto input,CancellationToken ct)
    {await service.WorkspaceAsync(id,ct);return OkResponse(await bob.ProposeJobAsync(id,input,ct));}
    [HttpPost("{id:guid}/bob/{actionId:guid}/approve")]public async Task<IActionResult> ApproveBob(Guid id,Guid actionId,CancellationToken ct)
    {await authority.RequireAsync(true,ct);await service.WorkspaceAsync(id,ct);return OkResponse(await bob.ApproveJobAsync(id,actionId,ct));}
}
public sealed class JobPolicyInput{public string ExpectedVersion{get;set;}="";public JobConfigurationDto Policy{get;set;}=new();}
