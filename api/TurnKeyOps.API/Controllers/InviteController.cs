using MedInsights.Lib.Dtos;
using Microsoft.AspNetCore.Authorization;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantAdmin)]
    public class InviteController : ApiControllerBase
    {
        private readonly IInviteService _service;

        public InviteController(IInviteService service) : base()
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => OkResponse(await _service.GetAllAsync(ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var invite = await _service.GetAsync(id, ct);
            return invite is null ? NotFound() : OkResponse(invite);
        }

        [AllowAnonymous]
        [HttpGet("{id:guid}/acceptance")]
        public async Task<IActionResult> GetAcceptanceContext(Guid id, [FromQuery] string token, CancellationToken ct)
            => OkResponse(await _service.GetAcceptanceContextAsync(id, token, ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInviteRequestDto dto, CancellationToken ct)
            => OkResponse(await _service.CreateAsync(dto, ct));

        [HttpPost("{id:guid}/resend")]
        public async Task<IActionResult> Resend(Guid id, CancellationToken ct)
            => OkResponse(await _service.ResendAsync(id, ct));

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
            => OkResponse(await _service.CancelAsync(id, ct));

        [HttpPost("{id:guid}/redeem")]
        public async Task<IActionResult> Redeem(Guid id, [FromBody] RedeemInviteRequestDto dto, CancellationToken ct)
            => OkResponse(await _service.RedeemAsync(id, dto, ct));
    }
}
