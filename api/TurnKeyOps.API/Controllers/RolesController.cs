using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly ITenantRoleDefinitionService _roles;

    public RolesController(ITenantRoleDefinitionService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(new { success = true, data = await _roles.GetAllAsync(ct) });

    [HttpGet("assignable")]
    public async Task<IActionResult> GetAssignable(CancellationToken ct)
        => Ok(new { success = true, data = await _roles.GetAssignableAsync(ct) });

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
        => Ok(new { success = true, data = await _roles.GetPermissionCatalogAsync(ct) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTenantRoleRequestDto dto, CancellationToken ct)
        => Ok(new { success = true, data = await _roles.CreateAsync(dto, ct) });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertTenantRoleRequestDto dto, CancellationToken ct)
        => Ok(new { success = true, data = await _roles.UpdateAsync(id, dto, ct) });

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, [FromBody] UpdateRolePermissionsRequestDto dto, CancellationToken ct)
        => Ok(new { success = true, data = await _roles.UpdatePermissionsAsync(id, dto, ct) });

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _roles.DeleteAsync(id, ct);
        return NoContent();
    }
}
