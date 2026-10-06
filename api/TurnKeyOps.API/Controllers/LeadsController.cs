using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.AuthenticatedSession)]
[Route("api/leads")]
[LeadErrors]
public sealed class LeadsController(LeadService service, LeadConfigurationService configuration,
    TurnKeyOps.Lib.Utils.IUserContext user, TurnKeyOps.Services.Interfaces.ITenantSettingsService settings,
    TurnKeyOps.Services.Interfaces.IBobOperationsService bob) : ApiControllerBase
{
    [HttpPost("{id:guid}/bob")]
    public async Task<IActionResult> Bob(Guid id, ProposeBobActionDto input, CancellationToken ct)
    {
        if (await service.GetAsync(id, ct) is null) return NotFound();
        return OkResponse(await bob.ProposeLeadAsync(id, input, ct));
    }
    [HttpPost("{id:guid}/bob/{actionId:guid}/approve")]
    public async Task<IActionResult> ApproveBob(Guid id, Guid actionId, CancellationToken ct)
    {
        if (await service.GetAsync(id, ct) is null) return NotFound();
        // Action authority is checked again by the existing actor-partitioned executor.
        return OkResponse(await bob.ApproveLeadAsync(id, actionId, ct));
    }
    [HttpPut("configuration")]
    public async Task<IActionResult> Configure(LeadConfigurationUpdateDto input, CancellationToken ct) =>
        OkResponse(await configuration.UpdateAsync(user.TenantId, input.Configuration, input.ExpectedVersion, settings, ct));
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => OkResponse(await service.WorkspaceAsync(ct));
    [HttpGet("metrics")] public async Task<IActionResult> Metrics(CancellationToken ct) => OkResponse(await service.MetricsAsync(ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct) is { } lead ? OkResponse(lead) : NotFound();
    [HttpPost] public async Task<IActionResult> Create(CreateLeadDto input, CancellationToken ct) => OkResponse(await service.CreateAsync(input, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, UpdateLeadDto input, CancellationToken ct) => OkResponse(await service.UpdateAsync(id, input, ct));
    [HttpPost("reconcile-intake")] public async Task<IActionResult> Reconcile(CancellationToken ct) => OkResponse(await service.ReconcileAsync(ct));
    [HttpPost("{id:guid}/stage")] public async Task<IActionResult> Stage(Guid id, LeadStageDto input, CancellationToken ct) => OkResponse(await service.StageAsync(id, input, ct));
    [HttpPost("{id:guid}/activity")] public async Task<IActionResult> Note(Guid id, LeadNoteDto input, CancellationToken ct) => OkResponse(await service.NoteAsync(id, input, ct));
    [HttpPost("{id:guid}/customer")] public async Task<IActionResult> Customer(Guid id, LeadVersionDto input, CancellationToken ct) => OkResponse(await service.CreateCustomerAsync(id, input.ExpectedVersion, ct));
    [HttpGet("{id:guid}/duplicates")] public async Task<IActionResult> Duplicates(Guid id, CancellationToken ct) => OkResponse(await service.DuplicatesAsync(id, ct));
    [HttpPost("{id:guid}/assign")] public async Task<IActionResult> Assign(Guid id, LeadVersionDto input, CancellationToken ct) => OkResponse(await service.AssignAsync(id, input.ExpectedVersion, ct));
    [HttpPost("{id:guid}/estimate")] public async Task<IActionResult> Estimate(Guid id, LeadVersionDto input, CancellationToken ct) => OkResponse(await service.EstimateAsync(id, input.ExpectedVersion, ct));
    [HttpPost("{id:guid}/job")] public async Task<IActionResult> Job(Guid id, LeadVersionDto input, CancellationToken ct) => OkResponse(await service.ConvertAsync(id, input.ExpectedVersion, ct));
    [HttpPost("{id:guid}/schedule")] public async Task<IActionResult> Schedule(Guid id, LeadScheduleDto input, CancellationToken ct) => OkResponse(await service.ScheduleAsync(id, input, ct));
    [HttpGet("{id:guid}/intake")] public async Task<IActionResult> Intake(Guid id, CancellationToken ct) => OkResponse(await service.IntakeAsync(id, ct));
    [HttpPost("{id:guid}/draft")] public async Task<IActionResult> Draft(Guid id, LeadCommunicationDto input, CancellationToken ct) => OkResponse(await service.CommunicationAsync(id, input, false, ct));
    [HttpPost("{id:guid}/send")] public async Task<IActionResult> Send(Guid id, LeadCommunicationDto input, CancellationToken ct) => OkResponse(await service.CommunicationAsync(id, input, true, ct));
    [HttpPost("{id:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, [FromForm] string expectedVersion, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        var uploads = QuoteRequestAttachmentHttpMapper.Map(files);
        try { return OkResponse(await service.UploadAsync(id, expectedVersion, uploads, ct)); }
        finally { QuoteRequestAttachmentHttpMapper.Dispose(uploads); }
    }
    [HttpGet("{id:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid fileId, CancellationToken ct)
    {
        var file = await service.DownloadAsync(id, fileId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }
}
public sealed class LeadVersionDto { public string ExpectedVersion { get; set; } = ""; }

public sealed class LeadConfigurationUpdateDto { public LeadConfigurationDto Configuration { get; set; } = new(); public string ExpectedVersion { get; set; } = ""; }
