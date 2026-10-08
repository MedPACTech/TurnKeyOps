using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.API.Controllers;

public sealed class PortalErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var result=context.Exception switch {
            UnauthorizedAccessException => (401,"Please sign in to your customer portal."),
            KeyNotFoundException => (404,"This item is unavailable."),
            ArgumentException e => (400,e.Message),
            InvalidOperationException => (409,"This item changed or the action could not be confirmed. Refresh before trying again."),
            _ => (0,"") };
        if(result.Item1==0)return;
        context.Result=new ObjectResult(new{message=result.Item2}){StatusCode=result.Item1};context.ExceptionHandled=true;
    }
}

// Identity-token exchange is the ONLY portal endpoint that accepts iBeam bearer identity.
// It does not select a tenant membership or grant any employee role.
[ApiController, Route("api/portal-identity/{slug}/session"), Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.AuthenticatedSession), PortalErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PortalIdentityController(PortalAccessService access,IQuoteRequestTenantResolver tenants):ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Activate(string slug,CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("uid")??User.FindFirstValue("sub"),out var user))return Unauthorized();
        return Ok(await access.ActivateAsync(tenants.Resolve(slug).TenantId,user,ct));
    }
}

// Anonymous to EMPLOYEE auth only. Every action requires a dedicated, live portal session and record entitlement.
[ApiController,Route("api/portal/{slug}"),AllowAnonymous,PortalErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PortalController(PortalAccessService access,PortalService portal,PortalMessages messages,QuoteEstimateService estimates,IQuoteRequestTenantResolver tenants):ControllerBase
{
    private Task<PortalActor> Actor(string slug,CancellationToken ct)=>access.AuthenticateAsync(tenants.Resolve(slug).TenantId,Request.Headers["X-Portal-Session"].FirstOrDefault(),ct);
    [HttpGet]
    public async Task<IActionResult> Home(string slug,CancellationToken ct)=>Ok(await portal.HomeAsync(await Actor(slug,ct),ct));
    [HttpPost("preferences")]
    public async Task<IActionResult> Preferences(string slug,List<string> channels,CancellationToken ct){await access.SavePreferencesAsync(await Actor(slug,ct),channels,ct);return NoContent();}
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(string slug,CancellationToken ct){var a=await Actor(slug,ct);await access.LogoutAsync(a.TenantId,Request.Headers["X-Portal-Session"].ToString(),ct);return NoContent();}
    [HttpGet("work/{kind}/{id:guid}")]
    public async Task<IActionResult> Work(string slug,string kind,Guid id,CancellationToken ct)=>Ok(await portal.WorkAsync(await Actor(slug,ct),kind,id,ct));
    [HttpPost("work/{kind}/{id:guid}/explain")]
    public async Task<IActionResult> Explain(string slug,string kind,Guid id,CancellationToken ct)=>Ok(await portal.ExplainAsync(await Actor(slug,ct),kind,id,ct));
    [HttpPost("jobs/{id:guid}")]
    public async Task<IActionResult> JobAction(string slug,Guid id,PortalCommand input,CancellationToken ct)=>Ok(await portal.JobActionAsync(await Actor(slug,ct),id,input,ct));
    [HttpPost("appointments/{id:guid}")]
    public async Task<IActionResult> Appointment(string slug,Guid id,PortalCommand input,CancellationToken ct)=>Ok(await portal.AppointmentAsync(await Actor(slug,ct),id,input,ct));
    [HttpPost("proposals/{id:guid}/{decision}")]
    public async Task<IActionResult> Proposal(string slug,Guid id,string decision,QuoteEstimateDecisionDto input,CancellationToken ct)
    {
        if(decision is not ("accept" or "decline" or "request-change"))throw new ArgumentException("Choose a proposal response.");
        input.Decline=decision=="decline";var a=await Actor(slug,ct);
        var result=await estimates.PortalDecideAsync(a,slug,id,input,decision=="accept",ct);
        await access.Audit(a.TenantId,a.UserId,"proposal."+decision,id,ct);return Ok(result);
    }
    [HttpGet("files/{kind}/{id:guid}/{fileId:guid}")]
    public async Task<IActionResult> File(string slug,string kind,Guid id,Guid fileId,CancellationToken ct)
    {
        var a=await Actor(slug,ct);var file=kind switch {"job"=>await portal.JobFileAsync(a,id,fileId,ct),"estimate"=>await estimates.PortalFileAsync(a,id,fileId,ct),_=>throw new KeyNotFoundException()};
        return File(file.Content,file.ContentType,file.FileName);
    }
    [HttpGet("messages/{kind}/{id:guid}")]
    public async Task<IActionResult> Messages(string slug,string kind,Guid id,CancellationToken ct)=>Ok(await messages.ListAsync(await Actor(slug,ct),kind,id,ct));
    [HttpPost("messages/{kind}/{id:guid}")]
    public async Task<IActionResult> Message(string slug,string kind,Guid id,PortalCommand input,CancellationToken ct)=>Ok(await messages.SendAsync(await Actor(slug,ct),kind,id,input.Text,ct));
    [HttpPost("messages/{kind}/{id:guid}/files"),RequestSizeLimit(11*1024*1024)]
    public async Task<IActionResult> Upload(string slug,string kind,Guid id,IFormFile file,CancellationToken ct)
    {await using var stream=file.OpenReadStream();return Ok(await messages.UploadAsync(await Actor(slug,ct),kind,id,file.FileName,file.ContentType,stream,ct));}
    [HttpGet("messages/{kind}/{id:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> MessageFile(string slug,string kind,Guid id,Guid fileId,CancellationToken ct)
    {var f=await messages.DownloadAsync(await Actor(slug,ct),kind,id,fileId,ct);return File(f.Content,f.ContentType,f.FileName);}
}
