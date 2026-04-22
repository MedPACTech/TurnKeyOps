using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantBillingAccountController : ApiControllerBase
    {
        private readonly ITenantBillingAccountService _service;

        public TenantBillingAccountController(ITenantBillingAccountService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrent(CancellationToken ct)
        {
            var account = await _service.GetCurrentAsync(ct);
            return account is null ? NotFound() : OkResponse(account);
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] TenantBillingAccountDto dto, CancellationToken ct)
            => OkResponse(await _service.UpsertAsync(dto, ct));
    }
}
