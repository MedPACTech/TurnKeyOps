using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantSubscriptionController : ApiControllerBase
    {
        private readonly ITenantSubscriptionService _service;

        public TenantSubscriptionController(ITenantSubscriptionService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => OkResponse(await _service.GetAllAsync(ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var subscription = await _service.GetAsync(id, ct);
            return subscription is null ? NotFound() : OkResponse(subscription);
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] TenantSubscriptionDto dto, CancellationToken ct)
            => OkResponse(await _service.UpsertAsync(dto, ct));
    }
}
