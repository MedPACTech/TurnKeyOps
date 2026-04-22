using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
[Route("api/admin/estimate-defaults")]
public class AdminEstimateDefaultsController : ApiControllerBase
{
    private readonly IEstimateDefaultsService _service;

    public AdminEstimateDefaultsController(IEstimateDefaultsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _service.GetAsync(ct);
        return OkResponse(result);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] EstimateDefaultsDto dto, CancellationToken ct)
    {
        var result = await _service.UpsertAsync(dto, ct);
        return OkResponse(result);
    }
}
