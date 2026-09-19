using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace MedInsights.Controllers;

[ApiController]
[Route("api/people")]
[PeopleInputErrors]
[Authorize(Policy = TurnKeyAuthorizationPolicies.TenantAdmin)]
public sealed class PeopleController(ManagedPeopleService people) : ControllerBase
{
    [HttpGet("customers")] public async Task<IActionResult> Customers(CancellationToken ct) => Ok(new { data = await people.CustomersAsync(ct) });
    [HttpPost("{id:guid}/role")] public async Task<IActionResult> Role(Guid id, [FromBody] UpdateMembershipRoleRequestDto input, CancellationToken ct) { await people.UpdateRoleAsync(id, input.Role, ct); return NoContent(); }
    [HttpPost("{id:guid}/invite")] public async Task<IActionResult> Invite(Guid id, [FromBody] UpdateMembershipRoleRequestDto input, CancellationToken ct) => Ok(new { data = await people.InviteAsync(id, input.Role, ct) });
    [HttpPost("{id:guid}/restore")] public async Task<IActionResult> Restore(Guid id, [FromQuery] string version, CancellationToken ct) { await people.RestoreAsync(id, version, ct); return NoContent(); }
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(new { data = await people.ListAsync(ct) });
    [HttpPost] public async Task<IActionResult> Create(SaveManagedPersonDto input, CancellationToken ct) => Ok(new { data = await people.SaveAsync(null, input, ct) });
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, SaveManagedPersonDto input, CancellationToken ct) => Ok(new { data = await people.SaveAsync(id, input, ct) });
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Archive(Guid id, [FromQuery] string version, CancellationToken ct) { await people.ArchiveAsync(id, version, ct); return NoContent(); }
}
[ApiController]
[Route("api/my-module-access")]
[Authorize(Policy = TurnKeyAuthorizationPolicies.AuthenticatedSession)]
public sealed class MyModuleAccessController(UserModuleAccessService access) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Ok(new { data = await access.GetAsync(ct) });
}

// Only known user-input/concurrency failures are exposed; unexpected failures use the normal middleware.
public sealed class PeopleInputErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var (status, message) = context.Exception switch {
            ArgumentException e => (400, e.Message),
            KeyNotFoundException e => (404, e.Message),
            Azure.RequestFailedException e when e.Status is 409 or 412 => (409, "This record changed. Reload and try again."),
            _ => (0, "")
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new { error = message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
