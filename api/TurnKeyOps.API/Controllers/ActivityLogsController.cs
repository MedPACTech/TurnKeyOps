using System.Globalization;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/activity-logs")]
    [Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantAdmin)]
    public class ActivityLogsController : ApiControllerBase
    {
        private readonly IActivityLogService _service;

        public ActivityLogsController(IActivityLogService service)
        {
            _service = service;
        }

        // GET api/activity-logs?entryDate=2025-12-05
        [HttpGet]
        public async Task<IActionResult> GetByEntryDate([FromQuery] DateTime entryDate)
        {
            if (entryDate == default)
                return BadRequest("entryDate is required. Use format YYYY-MM-DD.");

            var result = await _service.GetEntryForUserByDateAsync(entryDate);

            // For a collection endpoint, prefer 200 with empty list over 404
            return OkResponse(result);
        }

        // GET api/activity-logs/month?month=202512
        [HttpGet("month")]
        public async Task<IActionResult> GetByMonth([FromQuery] DateTime month)
        {
            if (month == default)
                return BadRequest("month is required.");

            var result = await _service.GetEntriesForMonthAsync(month);

            return OkResponse(result);
        }


        // POST api/daily-activity-logs
        [HttpPost()]
        public async Task<IActionResult> AddAsync(
            [FromBody] ActivityLogUpsertDto dto, CancellationToken ct)
        {

            if (dto == null)
                return BadRequestResponse("ActivityLogUpsertDto is required.");

            if (dto.EntryDate == default)
                return BadRequestResponse("EntryDate is required. Use format YYYY-MM-DD.");

            if (dto.Narrative == null || dto.Narrative.Length < 1)
                return BadRequestResponse($"Narrative is required.");
                
            if (dto.Items.Count < 1)
                return BadRequestResponse($"At least one ActivityLogItem is required.");

            var result = await _service.UpsertAsync(dto, ct);
            return OkResponse(result);
        }
    }
}
