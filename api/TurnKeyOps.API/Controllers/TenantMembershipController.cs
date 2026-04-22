using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantMembershipController : ApiControllerBase
    {
        private readonly ITenantMembershipService _service;

        public TenantMembershipController(ITenantMembershipService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => OkResponse(await _service.GetAllAsync(ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var membership = await _service.GetAsync(id, ct);
            return membership is null ? NotFound() : OkResponse(membership);
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] TenantMembershipDto dto, CancellationToken ct)
            => OkResponse(await _service.UpsertAsync(dto, ct));

        [HttpPost("{id:guid}/role")]
        public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateMembershipRoleRequestDto dto, CancellationToken ct)
            => OkResponse(await _service.UpdateRoleAsync(id, dto, ct));

        [HttpPost("{id:guid}/reassign")]
        public async Task<IActionResult> Reassign(Guid id, [FromBody] ReassignMembershipRequestDto dto, CancellationToken ct)
            => OkResponse(await _service.ReassignAsync(id, dto, ct));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
            => OkResponse(await _service.RemoveAsync(id, ct));
    }
}
