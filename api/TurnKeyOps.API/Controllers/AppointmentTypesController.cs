using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/appointmenttypes")]
    [Authorize]
    public sealed class AppointmentTypesController : ApiControllerBase
    {
        private readonly IAppointmentTypeService _service;

        public AppointmentTypesController(IAppointmentTypeService service)
            : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true, CancellationToken ct = default)
        {
            var result = await _service.GetAllAsync(includeInactive, ct);
            return OkResponse(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentTypeDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return OkResponse(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppointmentTypeDto dto, CancellationToken ct)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            var updated = await _service.UpdateAsync(id, dto, ct);
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
