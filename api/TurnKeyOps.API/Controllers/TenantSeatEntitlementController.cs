using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantSeatEntitlementController : ApiControllerBase
    {
        private readonly ITenantSeatEntitlementService _service;

        public TenantSeatEntitlementController(ITenantSeatEntitlementService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrent(CancellationToken ct)
        {
            var entitlement = await _service.GetCurrentAsync(ct);
            return entitlement is null ? NotFound() : OkResponse(entitlement);
        }
    }
}
