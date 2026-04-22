using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantCreditBalanceController : ApiControllerBase
    {
        private readonly ITenantCreditBalanceService _service;

        public TenantCreditBalanceController(ITenantCreditBalanceService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrent(CancellationToken ct)
        {
            var balance = await _service.GetCurrentAsync(ct);
            return balance is null ? NotFound() : OkResponse(balance);
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] TenantCreditBalanceDto dto, CancellationToken ct)
            => OkResponse(await _service.UpsertAsync(dto, ct));
    }
}
