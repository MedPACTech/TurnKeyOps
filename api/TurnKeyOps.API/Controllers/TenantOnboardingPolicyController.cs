using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantOnboardingPolicyController : ApiControllerBase
    {
        private readonly ITenantOnboardingPolicyService _service;

        public TenantOnboardingPolicyController(ITenantOnboardingPolicyService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrent(CancellationToken ct)
            => OkResponse(await _service.GetCurrentAsync(ct));

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] TenantOnboardingPolicyDto dto, CancellationToken ct)
            => OkResponse(await _service.UpdateCurrentAsync(dto, ct));
    }
}
