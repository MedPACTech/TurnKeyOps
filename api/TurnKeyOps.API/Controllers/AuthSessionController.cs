using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers;

[ApiController]
[Route("api/auth/session")]
[Authorize]
public sealed class AuthSessionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated == true
        });
    }
}
