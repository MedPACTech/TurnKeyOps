using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantProfileController : ApiControllerBase
    {
        private readonly ITenantProfileService _service;

        public TenantProfileController(ITenantProfileService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrent(CancellationToken ct)
        {
            var profile = await _service.GetCurrentAsync(ct);
            if (profile is null)
                return NotFound();

            return OkResponse(profile);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var profile = await _service.GetAsync(id, ct);
            if (profile is null)
                return NotFound();

            return OkResponse(profile);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TenantProfileDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return OkResponse(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement payload, CancellationToken ct)
        {
            var updated = await _service.UpdateAsync(id, payload, ct);
            return OkResponse(updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
    }
}
