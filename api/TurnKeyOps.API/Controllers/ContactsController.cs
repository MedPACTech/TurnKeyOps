using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers;

[ApiController]
[Route("api/contacts")]
[PeopleInputErrors]
[Authorize(Policy = TurnKeyAuthorizationPolicies.TenantAdmin)]
public sealed class ContactsController(ManagedPeopleService people, ContactWorkService work) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(new { data = await people.ContactsAsync(ct) });
    [HttpGet("customers")] public async Task<IActionResult> Customers(CancellationToken ct) => Ok(new { data = await people.CustomersAsync(ct, true) });
    [HttpGet("{id:guid}/work")] public async Task<IActionResult> Work(Guid id, CancellationToken ct) => Ok(new { data = await work.GetAsync(id, ct) });
    [HttpPost] public async Task<IActionResult> Create(SaveContactRecordDto input, CancellationToken ct) => Ok(new { data = await people.SaveContactAsync(null, input, ct) });
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, SaveContactRecordDto input, CancellationToken ct) => Ok(new { data = await people.SaveContactAsync(id, input, ct) });
}
