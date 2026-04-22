using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/referrals")]
    [Authorize]
    public sealed class ReferralsController : ApiControllerBase
    {
        private readonly IReferralWorkItemService _service;

        public ReferralsController(IReferralWorkItemService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? patientId,
            [FromQuery] Guid? encounterId,
            [FromQuery] string? status,
            [FromQuery] string? search,
            CancellationToken ct)
        {
            var items = await _service.GetAllAsync(patientId, encounterId, status, search, ct);
            return OkResponse(items);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var item = await _service.GetAsync(id, ct);
            if (item is null)
                return NotFound();

            return OkResponse(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReferralWorkItemDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto, ct);
            return OkResponse(created);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReferralWorkItemDto dto, CancellationToken ct)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");

            var updated = await _service.UpdateAsync(id, dto, ct);
            return OkResponse(updated);
        }

        [HttpPut("{id:guid}/workflow")]
        public async Task<IActionResult> UpdateWorkflow(Guid id, [FromBody] UpdateReferralWorkflowDto dto, CancellationToken ct)
        {
            var updated = await _service.UpdateWorkflowAsync(id, dto, ct);
            return OkResponse(updated);
        }

        [HttpPost("{id:guid}/actions")]
        public async Task<IActionResult> AddAction(Guid id, [FromBody] ReferralWorkItemActionDto dto, CancellationToken ct)
        {
            var updated = await _service.AddActionAsync(id, dto, ct);
            return OkResponse(updated);
        }

        [HttpPost("{id:guid}/case-summary/refresh")]
        public async Task<IActionResult> RefreshCaseSummary(Guid id, [FromQuery] bool forceRefresh = false, CancellationToken ct = default)
        {
            var updated = await _service.RefreshCaseSummaryAsync(id, forceRefresh, ct);
            return OkResponse(updated);
        }
    }
}
