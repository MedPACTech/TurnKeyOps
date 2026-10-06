using TurnKeyOps.Lib.Dtos;
using System.Globalization;
using MedInsights.Repositories.Interfaces;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public sealed class JobService : IJobService
{
    private static readonly HashSet<string> ChecklistKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer-confirmed", "site-access", "utility-locate", "base-material-ordered",
        "equipment-reserved", "concrete-ordered", "forms-reinforcement", "weather-check",
        "pour-confirmed", "cleanup-walkthrough"
    };

    private readonly IJobRepository _repo;
    private readonly IEstimateWorkflowPayloadStore _estimatePayloadStore;
    private readonly IJobWorkflowPayloadStore _jobPayloadStore;
    private readonly IInvoiceService _invoiceService;
    private readonly IUserContext _userContext;
    private readonly ITenantMembershipRepository _memberships;
    private readonly ITenantSettingsRepository _settings;
    private readonly ICalendarEventRepository _calendar;

    public JobService(
        IJobRepository repo,
        IEstimateWorkflowPayloadStore estimatePayloadStore,
        IJobWorkflowPayloadStore jobPayloadStore,
        IInvoiceService invoiceService,
        IUserContext userContext,
        ITenantMembershipRepository memberships,
        ITenantSettingsRepository settings,
        ICalendarEventRepository calendar)
    {
        _repo = repo;
        _estimatePayloadStore = estimatePayloadStore;
        _jobPayloadStore = jobPayloadStore;
        _invoiceService = invoiceService;
        _userContext = userContext;
        _memberships = memberships;
        _settings = settings;
        _calendar = calendar;
    }

    private string Partition() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);
    private static string Row(Guid id) => RepositoryKeyHelper.ToRowKey(id);

    public async Task<JobDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(id, ct);
        return entity is null ? null : await HydrateAsync(entity, ct);
    }

    public async Task<(IEnumerable<JobDto> Items, string? ContinuationToken)> GetPagedAsync(
        int pageSize,
        string? continuationToken,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = int.TryParse(continuationToken, out var parsed) && parsed >= 0 ? parsed : 0;
        var entities = await _repo.ListAsync(Partition(), ct);
        var page = entities.Skip(offset).Take(pageSize).ToArray();
        var items = new List<JobDto>(page.Length);
        foreach (var entity in page) items.Add(await HydrateAsync(entity, ct));
        var token = offset + page.Length < entities.Count ? (offset + page.Length).ToString() : null;
        return (items, token);
    }

    public async Task<IEnumerable<JobDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var entities = (await _repo.ListAsync(Partition(), ct))
            .Where(job => job.Status is not (JobStatus.Completed or JobStatus.Cancelled or JobStatus.Closed))
            .ToArray();
        var items = new List<JobDto>(entities.Length);
        foreach (var entity in entities) items.Add(await HydrateAsync(entity, ct));
        return items;
    }

    public async Task<JobDto> AddAsync(JobDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        dto.Id = dto.Id == Guid.Empty ? dto.InvoiceId.GetValueOrDefault(Guid.NewGuid()) : dto.Id;
        if (dto.Id == Guid.Empty) dto.Id = Guid.NewGuid();

        var existing = await GetEntityAsync(dto.Id, ct);
        if (existing is not null) return await HydrateAsync(existing, ct);
        if (dto.InvoiceId.HasValue)
        {
            var duplicate = (await _repo.ListAsync(Partition(), ct)).FirstOrDefault(item => item.InvoiceId == dto.InvoiceId);
            if (duplicate is not null) return await HydrateAsync(duplicate, ct);
        }

        ValidateCore(dto);
        await ValidateLocksmithAssignmentAsync(dto.TradeType, dto.LocksmithJobType, dto.AssignedTechnicianMembershipId, dto.Status, dto.ScheduledStart, ct);
        if (dto.TradeType == TradeType.DoorsLocksmith && dto.ScheduledStart.HasValue && string.IsNullOrWhiteSpace(dto.Crew))
            dto.Crew = $"Technician {dto.AssignedTechnicianMembershipId!.Value.ToString("N")[..8]}";
        await EnsureReleaseEligibleAsync(dto.Status, dto.InvoiceId, ct);
        await EnsureNoScheduleConflictAsync(dto.Id, dto.Crew, dto.AssignedTechnicianMembershipId, dto.ScheduledStart, dto.ScheduledEnd, ct);

        var now = DateTime.UtcNow;
        var entity = JobMapper.ToEntity(dto, Partition());
        entity.DateCreated = now;
        entity.DateUpdated = now;
        var payload = new JobWorkflowPayloadDto
        {
            AcceptedEstimate = dto.AcceptedEstimate,
            Planning = NormalizePlanning(dto.Planning, dto, now),
            Activity =
            [
                Activity("job_created", "Job created in the durable production workflow.", dto.Notes, now),
                Activity("scheduled", $"Scheduled for {dto.ScheduledStart:yyyy-MM-dd HH:mm} UTC.", dto.Notes, now)
            ]
        };
        entity.EstimateSnapshotBlobName = await _estimatePayloadStore.SaveJobEstimateSnapshotAsync(_userContext.TenantId, dto.Id, dto.EstimateSnapshot);
        entity.EstimateSnapshotJson = null;
        var saved = await PersistAsync(entity, payload, ct);
        return await HydrateAsync(saved, ct);
    }

    public async Task<JobDto> UpdateAsync(JobDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var existing = await RequireEntityAsync(dto.Id, ct);
        ValidateVersion(existing, dto.Version);
        ValidateCore(dto);
        await ValidateLocksmithAssignmentAsync(dto.TradeType, dto.LocksmithJobType, dto.AssignedTechnicianMembershipId, dto.Status, dto.ScheduledStart, ct);
        if (dto.TradeType == TradeType.DoorsLocksmith && dto.ScheduledStart.HasValue && string.IsNullOrWhiteSpace(dto.Crew))
            dto.Crew = $"Technician {dto.AssignedTechnicianMembershipId!.Value.ToString("N")[..8]}";
        if (existing.Status != dto.Status) ValidateTransition(existing.Status, dto.Status);
        if (!IsReleased(existing.Status) && IsReleased(dto.Status))
            await EnsureReleaseEligibleAsync(dto.Status, dto.InvoiceId, ct);
        await EnsureNoScheduleConflictAsync(dto.Id, dto.Crew, dto.AssignedTechnicianMembershipId, dto.ScheduledStart, dto.ScheduledEnd, ct);

        var payload = await _jobPayloadStore.LoadAsync(existing.WorkflowPayloadBlobName, ct);
        var entity = JobMapper.ToEntity(dto, existing.PartitionKey);
        entity.ETag = existing.ETag;
        entity.DateCreated = existing.DateCreated;
        entity.WorkflowPayloadBlobName = existing.WorkflowPayloadBlobName;
        entity.EstimateSnapshotBlobName = await _estimatePayloadStore.SaveJobEstimateSnapshotAsync(_userContext.TenantId, dto.Id, dto.EstimateSnapshot);
        entity.EstimateSnapshotJson = null;
        payload.Planning = NormalizePlanning(dto.Planning, dto, DateTime.UtcNow);
        payload.Activity.Add(Activity("job_updated", "Job details were updated.", dto.Notes, DateTime.UtcNow));
        var saved = await PersistAsync(entity, payload, ct);
        return await HydrateAsync(saved, ct);
    }

    public async Task<JobDto> ScheduleAsync(Guid id, JobScheduleInputDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var entity = await RequireEntityAsync(id, ct);
        ValidateVersion(entity, input.ExpectedVersion);
        var start = Utc(input.ScheduledStart);
        var end = Utc(input.ScheduledEnd);
        var crew = Clean(input.Crew, 160);
        var assignedTechnician = input.AssignedTechnicianMembershipId ?? entity.AssignedTechnicianMembershipId;
        if (start == default || end <= start) throw new ArgumentException("The schedule end must be after the start.");
        if (entity.TradeType == TradeType.DoorsLocksmith)
        {
            await ValidateLocksmithAssignmentAsync(entity.TradeType, entity.LocksmithJobType, assignedTechnician, JobStatus.Scheduled, start, ct);
            if (string.IsNullOrWhiteSpace(crew)) crew = entity.Crew ?? $"Technician {assignedTechnician!.Value.ToString("N")[..8]}";
        }
        else if (string.IsNullOrWhiteSpace(crew)) throw new ArgumentException("A crew is required.", nameof(input.Crew));

        var unchanged = entity.ScheduledStart == start && entity.ScheduledEnd == end &&
            string.Equals(entity.Crew, crew, StringComparison.OrdinalIgnoreCase) &&
            entity.AssignedTechnicianMembershipId == assignedTechnician;
        if (unchanged) return await HydrateAsync(entity, ct);

        await EnsureReleaseEligibleAsync(JobStatus.Scheduled, entity.InvoiceId, ct);
        await EnsureNoScheduleConflictAsync(id, crew, assignedTechnician, start, end, ct);
        if (entity.Status is JobStatus.Created or JobStatus.Lead or JobStatus.Estimated or JobStatus.Invoiced or JobStatus.Paid or JobStatus.OnHold)
            entity.Status = JobStatus.Scheduled;
        else if (entity.Status is JobStatus.Completed or JobStatus.Cancelled or JobStatus.Closed)
            throw new ArgumentException("A completed, cancelled, or closed job cannot be rescheduled.");

        var payload = await _jobPayloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct);
        var now = DateTime.UtcNow;
        entity.ScheduledStart = start;
        entity.ScheduledEnd = end;
        entity.Crew = crew;
        entity.AssignedTechnicianMembershipId = assignedTechnician;
        payload.Planning.TargetDate = DateOnly.FromDateTime(start);
        payload.Planning.UpdatedAtUtc = now;
        payload.Planning.UpdatedBy = Actor();
        payload.Activity.Add(Activity("rescheduled", $"Scheduled {crew} for {start:yyyy-MM-dd HH:mm}–{end:HH:mm} UTC.", input.Note, now));
        var saved = await PersistAsync(entity, payload, ct);
        return await HydrateAsync(saved, ct);
    }

    public async Task<JobDto> UpdateStatusAsync(Guid id, JobStatusInputDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var entity = await RequireEntityAsync(id, ct);
        ValidateVersion(entity, input.ExpectedVersion);
        if (entity.Status == input.Status) return await HydrateAsync(entity, ct);
        ValidateTransition(entity.Status, input.Status);
        if (!IsReleased(entity.Status) && IsReleased(input.Status))
            await EnsureReleaseEligibleAsync(input.Status, entity.InvoiceId, ct);
        if (input.Status == JobStatus.Scheduled)
        {
            await ValidateLocksmithAssignmentAsync(entity.TradeType, entity.LocksmithJobType, entity.AssignedTechnicianMembershipId, JobStatus.Scheduled, entity.ScheduledStart, ct);
            await EnsureNoScheduleConflictAsync(id, entity.Crew, entity.AssignedTechnicianMembershipId, entity.ScheduledStart, entity.ScheduledEnd, ct);
        }

        var payload = await _jobPayloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct);
        var prior = entity.Status;
        var now = DateTime.UtcNow;
        entity.Status = input.Status;
        if (input.Status == JobStatus.InProgress) entity.ActualStart ??= now;
        if (input.Status == JobStatus.Completed) entity.ActualEnd ??= now;
        payload.Activity.Add(Activity("status_updated", $"Status changed from {prior} to {input.Status}.", input.Note, now));
        var saved = await PersistAsync(entity, payload, ct);
        return await HydrateAsync(saved, ct);
    }

    public async Task<JobDto> UpdatePlanningAsync(Guid id, JobPlanningInputDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var entity = await RequireEntityAsync(id, ct);
        ValidateVersion(entity, input.ExpectedVersion);
        var payload = await _jobPayloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct);
        var now = DateTime.UtcNow;
        payload.Planning = NormalizePlanning(input.Planning, JobMapper.ToDto(entity), now);
        payload.Activity.Add(Activity("planning_updated", "Materials and production planning were updated.", null, now));
        var saved = await PersistAsync(entity, payload, ct);
        return await HydrateAsync(saved, ct);
    }

    public async Task<JobDto> AddNoteAsync(Guid id, JobNoteInputDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var entity = await RequireEntityAsync(id, ct);
        ValidateVersion(entity, input.ExpectedVersion);
        var note = Clean(input.Note, 4000);
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A note is required.", nameof(input.Note));
        var payload = await _jobPayloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct);
        var now = DateTime.UtcNow;
        entity.Notes = note;
        payload.Activity.Add(Activity("note", "Job note added.", note, now));
        var saved = await PersistAsync(entity, payload, ct);
        return await HydrateAsync(saved, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(id, ct);
        if (entity is null) return;
        if (entity.Status is JobStatus.InProgress or JobStatus.Completed or JobStatus.Closed)
            throw new ArgumentException("An active or completed job cannot be deleted.");
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity, ct);
    }

    private async Task EnsureReleaseEligibleAsync(JobStatus status, Guid? invoiceId, CancellationToken ct)
    {
        if (!IsReleased(status)) return;
        if (!invoiceId.HasValue || invoiceId.Value == Guid.Empty)
            throw new ArgumentException("A qualifying invoice is required before a job can be released.", nameof(invoiceId));
        var release = await _invoiceService.GetJobReleaseAsync(invoiceId.Value, ct);
        if (!release.IsEligible) throw new ArgumentException(release.Reason, nameof(invoiceId));
    }

    private async Task ValidateLocksmithAssignmentAsync(TradeType trade, string? jobType, Guid? membershipId, JobStatus status, DateTime? scheduledStart, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(Partition(), "SETTINGS|OPERATIONAL", ct);
        var isLocksmithTenant = settings is not null && !settings.IsDeleted && LocksmithPolicy.IsConfigured(settings.ValuesJson);
        if (trade != TradeType.DoorsLocksmith)
        {
            if (isLocksmithTenant)
                throw new ArgumentException("A doors and locksmith job requires the doors and locksmith trade.", nameof(trade));
            if (!string.IsNullOrWhiteSpace(jobType) || membershipId.HasValue)
                throw new ArgumentException("Locksmith job type and technician assignment require the doors and locksmith trade.");
            return;
        }

        if (jobType is not ("residential" or "commercial"))
            throw new ArgumentException("A residential or commercial locksmith job type is required.", nameof(jobType));
        if (!membershipId.HasValue || membershipId.Value == Guid.Empty)
        {
            if (scheduledStart.HasValue || status is JobStatus.Scheduled or JobStatus.InProgress)
                throw new ArgumentException("Assign an eligible technician before scheduling a locksmith job.", nameof(membershipId));
            return;
        }

        var member = (await _memberships.GetActiveAssignedByTenantAsync(_userContext.TenantId, ct))
            .FirstOrDefault(item => item.Id == membershipId.Value && item.TenantId == _userContext.TenantId &&
                !item.IsDeleted && !item.DateRemoved.HasValue &&
                !string.Equals(item.Role, "contact", StringComparison.OrdinalIgnoreCase));
        if (member is null)
            throw new ArgumentException("The selected technician is not an active assigned member of this tenant.", nameof(membershipId));

        if (settings is null || settings.IsDeleted || !LocksmithPolicy.CapabilitiesFor(settings.ValuesJson, member.Id).Contains(jobType))
            throw new ArgumentException("The selected technician is not eligible for this job type.", nameof(membershipId));
    }

    private async Task EnsureNoScheduleConflictAsync(Guid jobId, string? crew, Guid? membershipId, DateTime? start, DateTime? end, CancellationToken ct)
    {
        if (start is null || end is null || (membershipId is null && string.IsNullOrWhiteSpace(crew))) return;
        var normalizedStart = Utc(start.Value);
        var normalizedEnd = Utc(end.Value);
        var conflict = (await _repo.ListAsync(Partition(), ct)).Any(item =>
            item.Id != jobId &&
            item.Status is JobStatus.Scheduled or JobStatus.InProgress &&
            (membershipId.HasValue
                ? item.AssignedTechnicianMembershipId == membershipId
                : string.Equals(item.Crew, crew!.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            item.ScheduledStart < normalizedEnd && item.ScheduledEnd > normalizedStart);
        if (conflict) throw new ArgumentException("The selected crew already has an overlapping assignment.", nameof(crew));
        if (membershipId.HasValue)
        {
            var calendarConflict = (await _calendar.ListAsync(Partition(), ct)).Any(item =>
                item.AssignedTechnicianMembershipId == membershipId &&
                item.StartUtc < normalizedEnd && item.EndUtc > normalizedStart);
            if (calendarConflict)
                throw new ArgumentException("The selected technician already has an overlapping calendar assignment.", nameof(membershipId));
        }
    }

    private async Task<Job?> GetEntityAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return null;
        var entity = await _repo.GetAsync(Partition(), Row(id), ct);
        return entity is null || entity.IsDeleted ? null : entity;
    }

    private async Task<Job> RequireEntityAsync(Guid id, CancellationToken ct) =>
        await GetEntityAsync(id, ct) ?? throw new ArgumentException("Job not found.", nameof(id));

    private async Task<JobDto> HydrateAsync(Job entity, CancellationToken ct)
    {
        var dto = JobMapper.ToDto(entity);
        dto.EstimateSnapshot = await _estimatePayloadStore.LoadJobEstimateSnapshotAsync(entity.EstimateSnapshotBlobName, entity.EstimateSnapshotJson, ct);
        var payload = await _jobPayloadStore.LoadAsync(entity.WorkflowPayloadBlobName, ct);
        dto.AcceptedEstimate = payload.AcceptedEstimate;
        dto.Planning = payload.Planning;
        dto.Activity = [.. payload.Activity.OrderByDescending(item => item.OccurredAtUtc)];
        dto.Version = VersionOf(entity);
        return dto;
    }

    private async Task<Job> PersistAsync(Job entity, JobWorkflowPayloadDto payload, CancellationToken ct)
    {
        var oldBlob = entity.WorkflowPayloadBlobName;
        var newBlob = await _jobPayloadStore.SaveAsync(_userContext.TenantId, entity.Id, payload, ct);
        entity.WorkflowPayloadBlobName = newBlob;
        entity.DateUpdated = DateTime.UtcNow;
        try
        {
            var saved = await _repo.SaveAsync(entity, ct);
            if (!string.IsNullOrWhiteSpace(oldBlob) && oldBlob != newBlob)
            {
                try { await _jobPayloadStore.DeleteIfExistsAsync(oldBlob, ct); }
                catch { }
            }
            return saved;
        }
        catch
        {
            try { await _jobPayloadStore.DeleteIfExistsAsync(newBlob, CancellationToken.None); }
            catch { }
            throw;
        }
    }

    private JobPlanningDto NormalizePlanning(JobPlanningDto input, JobDto job, DateTime now)
    {
        input ??= new JobPlanningDto();
        var confirmation = input.CustomerConfirmationStatus.Trim().ToLowerInvariant();
        if (confirmation is not ("pending" or "confirmed" or "needs-reschedule"))
            throw new ArgumentException("Customer confirmation status is invalid.");
        input.CustomerConfirmationStatus = confirmation;
        input.CustomerConfirmedAtUtc = confirmation == "confirmed" ? input.CustomerConfirmedAtUtc ?? now : null;
        input.CustomerConfirmationNote = Clean(input.CustomerConfirmationNote, 2000);
        input.AccessNotes = Clean(input.AccessNotes, 2000);
        input.TargetDate ??= job.ScheduledStart.HasValue ? DateOnly.FromDateTime(job.ScheduledStart.Value) : null;
        input.Checklist = input.Checklist
            .Where(item => ChecklistKeys.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var key in ChecklistKeys) input.Checklist.TryAdd(key, false);
        input.Materials = input.Materials.Take(100).Select(material => NormalizeMaterial(material, now)).ToList();
        input.UpdatedAtUtc = now;
        input.UpdatedBy = Actor();
        return input;
    }

    private JobMaterialDto NormalizeMaterial(JobMaterialDto material, DateTime now)
    {
        material.Id = material.Id == Guid.Empty ? Guid.NewGuid() : material.Id;
        material.Kind = Clean(material.Kind, 80).ToLowerInvariant();
        material.Status = Clean(material.Status, 40).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(material.Kind)) throw new ArgumentException("Each material requires a kind.");
        if (material.Status is not ("not-started" or "requested" or "ordered" or "confirmed" or "delivered"))
            throw new ArgumentException("Material status is invalid.");
        if (material.Quantity is <= 0m or > 1_000_000m) throw new ArgumentException("Material quantity is outside the supported range.");
        material.Supplier = Clean(material.Supplier, 200);
        material.DeliveryWindow = Clean(material.DeliveryWindow, 100);
        material.Unit = Clean(material.Unit, 40);
        material.Specification = Clean(material.Specification, 500);
        material.Notes = Clean(material.Notes, 2000);
        material.UpdatedAtUtc = now;
        material.UpdatedBy = Actor();
        return material;
    }

    private static void ValidateCore(JobDto dto)
    {
        dto.Name = Clean(dto.Name, 300);
        dto.Description = Clean(dto.Description, 4000);
        dto.Crew = Clean(dto.Crew, 160);
        dto.Notes = Clean(dto.Notes, 4000);
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("A job name is required.", nameof(dto.Name));
        if (dto.ScheduledStart.HasValue != dto.ScheduledEnd.HasValue)
            throw new ArgumentException("Both schedule start and end are required together.");
        if (dto.ScheduledStart.HasValue)
        {
            dto.ScheduledStart = Utc(dto.ScheduledStart.Value);
            dto.ScheduledEnd = Utc(dto.ScheduledEnd!.Value);
            if (dto.ScheduledEnd <= dto.ScheduledStart) throw new ArgumentException("The schedule end must be after the start.");
            if (string.IsNullOrWhiteSpace(dto.Crew) && dto.TradeType != TradeType.DoorsLocksmith)
                throw new ArgumentException("A scheduled job requires a crew.", nameof(dto.Crew));
        }
    }

    private static void ValidateVersion(Job entity, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            throw new ArgumentException("The current job version is required. Refresh before retrying.", nameof(expected));
        if (!string.Equals(VersionOf(entity), expected.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("The job changed after it was loaded. Refresh before retrying.", nameof(expected));
    }

    private static string VersionOf(Job entity)
    {
        var etag = entity.ETag.ToString();
        return !string.IsNullOrWhiteSpace(etag)
            ? etag
            : entity.DateUpdated.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
    }

    private static void ValidateTransition(JobStatus from, JobStatus to)
    {
        var allowed = from switch
        {
            JobStatus.Created or JobStatus.Lead or JobStatus.Estimated or JobStatus.Invoiced or JobStatus.Paid =>
                to is JobStatus.Scheduled or JobStatus.Cancelled,
            JobStatus.Scheduled => to is JobStatus.InProgress or JobStatus.OnHold or JobStatus.Cancelled,
            JobStatus.InProgress => to is JobStatus.OnHold or JobStatus.Completed or JobStatus.Cancelled,
            JobStatus.OnHold => to is JobStatus.Scheduled or JobStatus.InProgress or JobStatus.Cancelled,
            JobStatus.Completed => to == JobStatus.Closed,
            _ => false
        };
        if (!allowed) throw new ArgumentException($"The job cannot move from {from} to {to}.");
    }

    private static bool IsReleased(JobStatus status) => status is JobStatus.Scheduled or JobStatus.InProgress;

    private JobActivityDto Activity(string type, string label, string? note, DateTime at) => new()
    {
        Id = Guid.NewGuid(), Type = type, Label = label, Note = Clean(note, 4000),
        Actor = Actor(), OccurredAtUtc = at
    };

    private string Actor()
    {
        var name = $"{_userContext.FirstName} {_userContext.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? _userContext.UserId.ToString("D") : name;
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static string Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
