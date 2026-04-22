using MedInsights.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace TurnKeyOps.API.Controllers;

[Route("api/[controller]")]
public abstract class ApiControllerBase : MedInsights.Controllers.ApiControllerBase
{
}
