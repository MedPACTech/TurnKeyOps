using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Lib.Utils;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/inventory")][LeadErrors]
public sealed class InventoryController(SupplyService service,SupplyFileService files,ISupplyAuthority authority,IUserContext user,IBobOperationsService bob):ApiControllerBase
{

    [HttpPost("bob")]public async Task<IActionResult> Bob(ProposeBobActionDto input,CancellationToken ct)
    {await authority.RequireAsync("inventory",false,ct);var args=JsonNode.Parse(input.Input.GetRawText())!.AsObject();args["supplyId"]=user.TenantId.ToString();input.Input=JsonSerializer.SerializeToElement(args);return OkResponse(await bob.ProposeSupplyAsync(user.TenantId,input,ct));}
    [HttpPost("bob/{actionId:guid}/approve")]public async Task<IActionResult> ApproveBob(Guid actionId,CancellationToken ct)
    {await authority.RequireAsync("inventory",true,ct);return OkResponse(await bob.ApproveSupplyAsync(user.TenantId,actionId,ct));}
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct)=>OkResponse(await service.WorkspaceAsync("inventory",ct));
    [HttpPost("command")] public async Task<IActionResult> Command(SupplyCommand input,CancellationToken ct)=>OkResponse(await service.CommandAsync("inventory",input,ct));
    [HttpPost("{id:guid}/files")][Consumes("multipart/form-data")][RequestSizeLimit(50*1024*1024)]
    public async Task<IActionResult> Upload(Guid id,[FromForm]string expectedVersion,[FromForm]List<IFormFile> uploads,CancellationToken ct)
    {var batch=QuoteRequestAttachmentHttpMapper.Map(uploads);try{await files.UploadAsync("inventory",id,expectedVersion,batch,ct);return OkResponse(new{uploaded=true});}finally{QuoteRequestAttachmentHttpMapper.Dispose(batch);}}
    [HttpGet("{id:guid}/files/{file:guid}")]
    public async Task<IActionResult> Download(Guid id,Guid file,CancellationToken ct)
    {var result=await files.DownloadAsync("inventory",id,file,ct);Response.Headers.CacheControl="private, no-store";Response.Headers.XContentTypeOptions="nosniff";return File(result.Content,result.ContentType,result.FileName);}
}
