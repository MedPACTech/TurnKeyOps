using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantAccess)]
    public class UserProfileController : ApiControllerBase
    {
        private readonly IUserProfileService _service;
        private readonly IUserVerifiedContactService _verifiedContactService;

        public UserProfileController(IUserProfileService service, IUserVerifiedContactService verifiedContactService) : base()
        {
            _service = service;
            _verifiedContactService = verifiedContactService;
        }

        // GET: api/userprofile
        [HttpGet]
        [Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantAdmin)]
        public async Task<IActionResult> GetAsync()
        {
            var users = await _service.GetAllAsync();
            return OkResponse(users);
        }

        // GET: api/userprofile/{id}
        [HttpGet("{userId:guid}")]
        [Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantAdmin)]
        public async Task<IActionResult> GetByIdAsync(Guid userId)
        {
            var user = await _service.GetAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            return OkResponse(user);
        }

        // PUT: api/userprofile
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UserProfileDto dto)
        {
            var updatedUser = await _service.UpdateAsync(dto);
            return OkResponse(updatedUser);
        }

        // GET: api/userprofile/me
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUserAsync()
        {
            //var userId = AuthenticatedUserId;
            var user = await _service.GetCurrentAsync();
            if (user == null)
            {
                return NotFound();
            }
            return OkResponse(user);
        }

        [HttpPost("contact-change/request")]
        public async Task<IActionResult> RequestContactChangeAsync([FromBody] RequestUserContactChangeDto dto, CancellationToken ct)
            => OkResponse(await _verifiedContactService.RequestChangeAsync(dto, ct));

        [HttpPost("contact-change/verify")]
        public async Task<IActionResult> VerifyContactChangeAsync([FromBody] VerifyUserContactChangeDto dto, CancellationToken ct)
            => OkResponse(await _verifiedContactService.VerifyChangeAsync(dto, ct));
    }
}
