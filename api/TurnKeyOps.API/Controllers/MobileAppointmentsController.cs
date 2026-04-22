using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
[Route("api/mobile/appointments")]
public sealed class MobileAppointmentsController : ApiControllerBase
{
    private readonly IMobileAppointmentContextService _service;

    public MobileAppointmentsController(IMobileAppointmentContextService service)
    {
        _service = service;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var result = await _service.GetCurrentAsync(ct);
        return OkResponse(result);
    }
}
