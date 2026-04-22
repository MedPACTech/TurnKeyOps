using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FacilitiesController : ApiControllerBase
    {
        private readonly IFacilityService _service;

        public FacilitiesController(IFacilityService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var facilities = await _service.GetAllAsync(ct);
            return OkResponse(facilities);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var facility = await _service.GetAsync(id, ct);
            if (facility is null)
                return NotFound();

            return OkResponse(facility);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FacilityDto dto, CancellationToken ct)
        {
            var created = await _service.AddAsync(dto, ct);
            return OkResponse(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] FacilityDto dto, CancellationToken ct)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            var updated = await _service.UpdateAsync(dto, ct);
            return OkResponse(updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }

        [HttpGet("{facilityId:guid}/patients")]
        public async Task<IActionResult> GetPatients(Guid facilityId, [FromQuery] bool includeDischarged = true, CancellationToken ct = default)
        {
            var assignments = await _service.GetPatientAssignmentsAsync(facilityId, includeDischarged, ct);
            return OkResponse(assignments);
        }

        [HttpPost("{facilityId:guid}/patients")]
        public async Task<IActionResult> AdmitPatient(Guid facilityId, [FromBody] AdmitFacilityPatientDto dto, CancellationToken ct)
        {
            var assignment = await _service.AdmitPatientAsync(facilityId, dto, ct);
            return OkResponse(assignment);
        }

        [HttpPut("{facilityId:guid}/patients/{assignmentId:guid}/discharge")]
        public async Task<IActionResult> DischargePatient(Guid facilityId, Guid assignmentId, [FromBody] DischargeFacilityPatientDto? dto, CancellationToken ct)
        {
            var assignment = await _service.DischargePatientAsync(facilityId, assignmentId, dto, ct);
            return OkResponse(assignment);
        }
    }
}
