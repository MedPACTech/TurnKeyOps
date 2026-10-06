using MedInsights.Repositories.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class CalendarEventService : ICalendarEventService
{
    private readonly ICalendarEventRepository _repo;
    private readonly IWeatherService _weatherService;
    private readonly IUserContext _userContext;
    private readonly ITenantMembershipRepository _memberships;
    private readonly ITenantSettingsRepository _settings;
    private readonly IJobRepository _jobs;
    private readonly IJobAuthority? _jobAuthority;
    private readonly IJobWorkflowPayloadStore? _jobPayloads;

    public CalendarEventService(
        ICalendarEventRepository repo,
        IWeatherService weatherService,
        IUserContext userContext,
        ITenantMembershipRepository memberships,
        ITenantSettingsRepository settings,
        IJobRepository jobs, IJobAuthority? jobAuthority = null, IJobWorkflowPayloadStore? jobPayloads = null)
    {
        _repo = repo;
        _weatherService = weatherService;
        _userContext = userContext;
        _memberships = memberships;
        _settings = settings;
        _jobs = jobs;
        _jobAuthority = jobAuthority;_jobPayloads=jobPayloads;
    }

    private string PartitionKeyForTenant() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<CalendarEventDto?> GetAsync(Guid id)
    {
        var partitionKey = PartitionKeyForTenant();
        var entity = await _repo.GetAsync(partitionKey, RepositoryKeyHelper.ToRowKey(id));
        return entity is null || entity.IsDeleted || entity.PartitionKey != partitionKey
            ? null : CalendarEventMapper.ToDto(entity);
    }

    public async Task<IEnumerable<CalendarEventDto>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        var pk = PartitionKeyForTenant();
        var all = await _repo.ListAsync(pk);
        var events = all
            .Where(e => e.PartitionKey == pk && !e.IsDeleted && e.StartUtc < end && e.EndUtc > start)
            .OrderBy(e => e.StartUtc)
            .Select(CalendarEventMapper.ToDto)
            .ToList();

        // Enrich with weather for events that have a job site
        foreach (var evt in events.Where(e => e.JobSiteId.HasValue))
        {
            try
            {
                var forecasts = await _weatherService.GetForecastForJobSiteAsync(evt.JobSiteId!.Value);
                var dayForecast = forecasts.FirstOrDefault(f =>
                    f.ForecastDate.HasValue && f.ForecastDate.Value.Date == evt.StartUtc.Date);
                if (dayForecast != null) evt.Weather = dayForecast;
            }
            catch { /* weather enrichment is best-effort */ }
        }

        return events;
    }

    public async Task<CalendarEventDto> AddAsync(CalendarEventDto dto)
    {
        if(dto.JobEventType is not null)throw new ArgumentException("Use the Job workspace for Job events.");
        await ValidateScheduleAsync(dto);
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        var entity = CalendarEventMapper.ToEntity(dto, PartitionKeyForTenant());
        await _repo.SaveAsync(entity);
        return CalendarEventMapper.ToDto(entity);
    }

    public async Task<CalendarEventDto> UpdateAsync(CalendarEventDto dto)
    {
        var partitionKey = PartitionKeyForTenant();
        var existing = await _repo.GetAsync(partitionKey, RepositoryKeyHelper.ToRowKey(dto.Id));
        if (existing is null || existing.IsDeleted || existing.PartitionKey != partitionKey)
            throw new ArgumentException("Calendar event not found", nameof(dto.Id));
        if(existing.JobEventType is not null || dto.JobEventType is not null)throw new ArgumentException("Use the Job workspace for Job events.");
        await ValidateScheduleAsync(dto);
        var entity = CalendarEventMapper.ToEntity(dto, existing.PartitionKey);
        entity.ETag = existing.ETag;
        entity.DateCreated = existing.DateCreated;
        await _repo.SaveAsync(entity);
        return CalendarEventMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var partitionKey = PartitionKeyForTenant();
        var entity = await _repo.GetAsync(partitionKey, RepositoryKeyHelper.ToRowKey(id));
        if (entity is null || entity.IsDeleted || entity.PartitionKey != partitionKey) return;
        if(entity.JobEventType is not null)throw new ArgumentException("Cancel Job events in the Job workspace.");
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }

    private async Task ValidateScheduleAsync(CalendarEventDto dto)
    {
        if(dto.MembershipIds.Count>0||dto.ResourceIds.Count>0)throw new ArgumentException("Manage operational resource assignments in the Job workspace.");
        if(dto.JobId is Guid jobId)
        {
            if(_jobAuthority is not null)await _jobAuthority.RequireAsync(true);
            var job=await _jobs.GetAsync(PartitionKeyForTenant(),RepositoryKeyHelper.ToRowKey(jobId));
            if(job is null||job.IsDeleted||job.PartitionKey!=PartitionKeyForTenant())throw new ArgumentException("Job not found.");
            if(_jobPayloads is not null&&(await _jobPayloads.LoadAsync(job.WorkflowPayloadBlobName)).Execution is not null)throw new ArgumentException("Manage this Job's events in its execution workspace.");
        }
        if (dto.EndUtc <= dto.StartUtc)
            throw new ArgumentException("The calendar end must be after the start.", nameof(dto.EndUtc));

        var partitionKey = PartitionKeyForTenant();
        var settings = await _settings.GetAsync(partitionKey, "SETTINGS|OPERATIONAL");
        var isLocksmithTenant = settings is not null && !settings.IsDeleted &&
            LocksmithPolicy.IsConfigured(settings.ValuesJson);

        if (!isLocksmithTenant || dto.EventType != CalendarEventType.SiteVisit)
        {
            if (!string.IsNullOrWhiteSpace(dto.LocksmithJobType) || dto.AssignedTechnicianMembershipId.HasValue)
                throw new ArgumentException("Locksmith assignment is only valid for a locksmith site visit.");
            return;
        }

        if (dto.LocksmithJobType is not ("residential" or "commercial"))
            throw new ArgumentException("A residential or commercial site visit type is required.", nameof(dto.LocksmithJobType));
        if (dto.AssignedTechnicianMembershipId is not { } membershipId || membershipId == Guid.Empty)
            throw new ArgumentException("Assign an eligible technician before confirming a site visit.", nameof(dto.AssignedTechnicianMembershipId));

        var member = (await _memberships.GetActiveAssignedByTenantAsync(_userContext.TenantId))
            .FirstOrDefault(item => item.Id == membershipId && item.TenantId == _userContext.TenantId &&
                !item.IsDeleted && !item.DateRemoved.HasValue &&
                !string.Equals(item.Role, "contact", StringComparison.OrdinalIgnoreCase));
        if (member is null || !LocksmithPolicy.CapabilitiesFor(settings!.ValuesJson, membershipId).Contains(dto.LocksmithJobType))
            throw new ArgumentException("The selected technician is not eligible for this site visit type.", nameof(dto.AssignedTechnicianMembershipId));

        var conflictingVisit = (await _repo.ListAsync(partitionKey)).Any(item =>
            item.Id != dto.Id && item.EventType == CalendarEventType.SiteVisit &&
            item.AssignedTechnicianMembershipId == membershipId &&
            item.StartUtc < dto.EndUtc && item.EndUtc > dto.StartUtc);
        var conflictingJob = (await _jobs.ListAsync(partitionKey)).Any(item =>
            item.Status is JobStatus.Scheduled or JobStatus.InProgress &&
            item.AssignedTechnicianMembershipId == membershipId &&
            item.ScheduledStart < dto.EndUtc && item.ScheduledEnd > dto.StartUtc);
        if (conflictingVisit || conflictingJob)
            throw new ArgumentException("The selected technician already has an overlapping assignment.", nameof(dto.AssignedTechnicianMembershipId));
    }
}
