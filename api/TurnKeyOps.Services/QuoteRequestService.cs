using System.Net.Mail;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public sealed class QuoteRequestService : IQuoteRequestService
{
    private static readonly HashSet<string> Priorities =
        new(["standard", "priority", "emergency"], StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["new"] = new(["in-review", "needs-info", "qualified", "contacted", "closed"], StringComparer.OrdinalIgnoreCase),
            ["in-review"] = new(["needs-info", "qualified", "contacted", "closed"], StringComparer.OrdinalIgnoreCase),
            ["needs-info"] = new(["in-review", "qualified", "contacted", "closed"], StringComparer.OrdinalIgnoreCase),
            ["qualified"] = new(["contacted", "inspection-scheduled", "estimate-drafted", "closed"], StringComparer.OrdinalIgnoreCase),
            ["contacted"] = new(["needs-info", "qualified", "inspection-scheduled", "estimate-drafted", "closed"], StringComparer.OrdinalIgnoreCase),
            ["inspection-scheduled"] = new(["qualified", "estimate-drafted", "closed"], StringComparer.OrdinalIgnoreCase),
            ["estimate-drafted"] = new(["estimate-sent", "closed"], StringComparer.OrdinalIgnoreCase),
            ["estimate-sent"] = new(["won", "closed"], StringComparer.OrdinalIgnoreCase),
            ["won"] = new(StringComparer.OrdinalIgnoreCase),
            ["closed"] = new(["in-review"], StringComparer.OrdinalIgnoreCase)
        };

    private readonly IQuoteRequestRepository _repository;
    private readonly IUserContext _userContext;
    private readonly QuoteRequestTenantOptions _tenantOptions;

    public QuoteRequestService(
        IQuoteRequestRepository repository,
        IUserContext userContext,
        IOptions<QuoteRequestTenantOptions> tenantOptions)
    {
        _repository = repository;
        _userContext = userContext;
        _tenantOptions = tenantOptions.Value;
    }

    public async Task<IReadOnlyCollection<QuoteRequestDto>> ListAsync(CancellationToken ct = default)
    {
        var entities = await _repository.ListAsync(PartitionKey(_userContext.TenantId), ct);
        return entities.Select(QuoteRequestMapper.ToDto).ToArray();
    }

    public async Task<QuoteRequestDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("A quote request id is required.", nameof(id));
        var entity = await _repository.GetAsync(PartitionKey(_userContext.TenantId), RowKey(id), ct);
        return entity is null || entity.IsDeleted ? null : QuoteRequestMapper.ToDto(entity);
    }

    public async Task<QuoteRequestDto> CreatePublicAsync(
        string tenantSlug,
        CreateQuoteRequestDto dto,
        CancellationToken ct = default)
    {
        var tenant = ResolveTenant(tenantSlug);
        ValidateCreate(dto);

        var id = dto.Id.GetValueOrDefault();
        if (id == Guid.Empty) id = Guid.NewGuid();
        var existing = await _repository.GetAsync(PartitionKey(tenant.TenantId), RowKey(id), ct);
        if (existing is not null && !existing.IsDeleted)
        {
            if (!MatchesPublicRequest(existing, dto))
                throw new ArgumentException("The request id is already in use.", nameof(dto.Id));
            return QuoteRequestMapper.ToDto(existing);
        }

        var now = DateTime.UtcNow;
        var attachments = NormalizeAttachments(dto.Attachments, tenant.TenantId, now);
        var submittedPayload = new QuoteRequestSubmittedPayloadDto
        {
            CompanyName = Clean(dto.CompanyName),
            ContactName = Clean(dto.ContactName),
            Email = Clean(dto.Email),
            Phone = Clean(dto.Phone),
            SiteName = Clean(dto.SiteName),
            ServiceAddress = Clean(dto.ServiceAddress),
            ServiceType = Clean(dto.ServiceType),
            PropertyType = Clean(dto.PropertyType),
            RequestedTimeline = Clean(dto.RequestedTimeline),
            Priority = dto.Priority.Trim().ToLowerInvariant(),
            Need = Clean(dto.Need),
            Attachments = attachments
        };
        var result = new QuoteRequestDto
        {
            Id = id,
            TenantId = tenant.TenantId,
            SubmittedAtUtc = now,
            CompanyName = submittedPayload.CompanyName,
            ContactName = submittedPayload.ContactName,
            CustomerName = submittedPayload.ContactName,
            Email = submittedPayload.Email,
            Phone = submittedPayload.Phone,
            SiteName = submittedPayload.SiteName,
            ServiceAddress = submittedPayload.ServiceAddress,
            ServiceType = submittedPayload.ServiceType,
            ProjectType = submittedPayload.ServiceType,
            PropertyType = submittedPayload.PropertyType,
            RequestedTimeline = submittedPayload.RequestedTimeline,
            PreferredTimeline = submittedPayload.RequestedTimeline,
            Priority = submittedPayload.Priority,
            Need = submittedPayload.Need,
            Message = submittedPayload.Need,
            Attachments = attachments,
            Source = "public-site",
            Status = "new",
            AssignedTo = Clean(tenant.DefaultAssignedTo),
            NextAction = Clean(tenant.DefaultNextAction),
            IntakeSummary = BuildIntakeSummary(submittedPayload),
            Qualification = new QuoteRequestQualificationDto(),
            SubmittedPayload = submittedPayload,
            Timeline =
            [
                NewEvent(now, "submitted", "Customer", "Quote request submitted", payload: submittedPayload)
            ],
            UpdatedAtUtc = now
        };

        var saved = await _repository.SaveAsync(QuoteRequestMapper.ToEntity(result), ct);
        return QuoteRequestMapper.ToDto(saved);
    }

    public async Task<QuoteRequestDto?> UpdateAsync(
        Guid id,
        QuoteRequestDto dto,
        CancellationToken ct = default)
    {
        if (id == Guid.Empty || (dto.Id != Guid.Empty && dto.Id != id))
            throw new ArgumentException("The route and body quote request ids must match.", nameof(id));

        var tenantId = _userContext.TenantId;
        if (dto.TenantId != Guid.Empty && dto.TenantId != tenantId)
            throw new UnauthorizedAccessException("The quote request does not belong to the current tenant.");

        var entity = await _repository.GetAsync(PartitionKey(tenantId), RowKey(id), ct);
        if (entity is null || entity.IsDeleted) return null;

        var current = QuoteRequestMapper.ToDto(entity);
        ValidateUpdate(current, dto);
        var now = DateTime.UtcNow;
        var actor = Actor();
        var timeline = current.Timeline.ToList();
        var nextStatus = Clean(dto.Status).ToLowerInvariant();
        var nextAssignedTo = Clean(dto.AssignedTo);

        if (!string.Equals(current.Status, nextStatus, StringComparison.OrdinalIgnoreCase))
            timeline.Add(NewEvent(now, "operator-updated", actor, $"Status changed · {current.Status} → {nextStatus}"));
        if (!string.Equals(current.AssignedTo, nextAssignedTo, StringComparison.Ordinal))
            timeline.Add(NewEvent(now, "operator-updated", actor, $"Owner reassigned · {current.AssignedTo} → {nextAssignedTo}"));

        var nextSchedule = NormalizeSchedule(dto.SiteVisitSchedule, actor, now);
        if (!SchedulesEqual(current.SiteVisitSchedule, nextSchedule))
        {
            var type = nextSchedule is null ? "site-visit-cancelled" : current.SiteVisitSchedule is null
                ? "site-visit-scheduled"
                : "site-visit-rescheduled";
            var label = nextSchedule is null ? "Site visit cancelled" : current.SiteVisitSchedule is null
                ? "Site visit scheduled"
                : "Site visit rescheduled";
            timeline.Add(NewEvent(now, type, actor, label, siteVisitSchedule: nextSchedule));
        }

        var detailsChanged = DetailsChanged(current, dto);
        if (detailsChanged && timeline.All(item => item.OccurredAtUtc != now))
            timeline.Add(NewEvent(now, "operator-updated", actor, "Request details updated"));

        var updated = new QuoteRequestDto
        {
            Id = current.Id,
            TenantId = current.TenantId,
            SubmittedAtUtc = current.SubmittedAtUtc,
            CompanyName = CleanOrCurrent(dto.CompanyName, current.CompanyName),
            ContactName = CleanOrCurrent(dto.ContactName, current.ContactName),
            CustomerName = CleanOrCurrent(dto.ContactName, current.ContactName),
            Email = Clean(dto.Email),
            Phone = Clean(dto.Phone),
            SiteName = CleanOrCurrent(dto.SiteName, current.SiteName),
            ServiceAddress = CleanOrCurrent(dto.ServiceAddress, current.ServiceAddress),
            ServiceType = CleanOrCurrent(dto.ServiceType, current.ServiceType),
            ProjectType = CleanOrCurrent(dto.ServiceType, current.ServiceType),
            PropertyType = CleanOrCurrent(dto.PropertyType, current.PropertyType),
            RequestedTimeline = CleanOrCurrent(dto.RequestedTimeline, current.RequestedTimeline),
            PreferredTimeline = CleanOrCurrent(dto.RequestedTimeline, current.RequestedTimeline),
            Priority = CleanOrCurrent(dto.Priority, current.Priority).ToLowerInvariant(),
            Need = CleanOrCurrent(dto.Need, current.Need),
            Message = CleanOrCurrent(dto.Need, current.Need),
            Attachments = current.Attachments,
            Source = current.Source,
            Status = nextStatus,
            AssignedTo = nextAssignedTo,
            NextAction = Clean(dto.NextAction),
            IntakeSummary = current.IntakeSummary,
            Qualification = new QuoteRequestQualificationDto
            {
                MissingInfoReasonCodes = NormalizeReasonCodes(dto.Qualification?.MissingInfoReasonCodes),
                ReviewedAtUtc = now,
                ReviewedBy = actor
            },
            SubmittedPayload = current.SubmittedPayload,
            Timeline = timeline,
            SiteVisitSchedule = nextSchedule,
            UpdatedAtUtc = now
        };

        var updatedEntity = QuoteRequestMapper.ToEntity(updated);
        updatedEntity.DateCreated = entity.DateCreated;
        updatedEntity.ETag = entity.ETag;
        var saved = await _repository.SaveAsync(updatedEntity, ct);
        return QuoteRequestMapper.ToDto(saved);
    }

    private QuoteRequestTenantDefinition ResolveTenant(string tenantSlug)
    {
        var slug = Clean(tenantSlug);
        if (!_tenantOptions.Tenants.TryGetValue(slug, out var tenant) || tenant.TenantId == Guid.Empty)
            throw new ArgumentException("The quote request tenant is not configured.", nameof(tenantSlug));
        return tenant;
    }

    private static void ValidateCreate(CreateQuoteRequestDto dto)
    {
        Required(dto.CompanyName, nameof(dto.CompanyName), 200);
        Required(dto.ContactName, nameof(dto.ContactName), 200);
        Required(dto.ServiceAddress, nameof(dto.ServiceAddress), 500);
        Required(dto.ServiceType, nameof(dto.ServiceType), 200);
        Required(dto.Need, nameof(dto.Need), 4000);
        if (string.IsNullOrWhiteSpace(dto.Email) && string.IsNullOrWhiteSpace(dto.Phone))
            throw new ArgumentException("An email address or phone number is required.", nameof(dto.Phone));
        ValidateEmail(dto.Email);
        if (!Priorities.Contains(Clean(dto.Priority)))
            throw new ArgumentException("Priority must be standard, priority, or emergency.", nameof(dto.Priority));
        if (dto.Attachments.Count > 25)
            throw new ArgumentException("A quote request cannot contain more than 25 attachments.", nameof(dto.Attachments));
    }

    private static void ValidateUpdate(QuoteRequestDto current, QuoteRequestDto dto)
    {
        var nextStatus = Clean(dto.Status).ToLowerInvariant();
        if (!AllowedTransitions.ContainsKey(nextStatus))
            throw new ArgumentException("The quote request status is invalid.", nameof(dto.Status));
        if (!string.Equals(current.Status, nextStatus, StringComparison.OrdinalIgnoreCase) &&
            (!AllowedTransitions.TryGetValue(current.Status, out var allowed) || !allowed.Contains(nextStatus)))
            throw new ArgumentException($"A quote request cannot move from {current.Status} to {nextStatus}.", nameof(dto.Status));
        Required(dto.AssignedTo, nameof(dto.AssignedTo), 200);
        Required(dto.NextAction, nameof(dto.NextAction), 1000);
        ValidateEmail(dto.Email);
        if (string.IsNullOrWhiteSpace(dto.Email) && string.IsNullOrWhiteSpace(dto.Phone))
            throw new ArgumentException("An email address or phone number is required.", nameof(dto.Phone));
        if (nextStatus == "needs-info" && (dto.Qualification?.MissingInfoReasonCodes.Count ?? 0) == 0)
            throw new ArgumentException("At least one missing-information reason is required.", nameof(dto.Qualification));
        if (nextStatus == "inspection-scheduled" && dto.SiteVisitSchedule is null)
            throw new ArgumentException("A site visit schedule is required for this status.", nameof(dto.SiteVisitSchedule));
    }

    private static List<QuoteRequestAttachmentDto> NormalizeAttachments(
        IEnumerable<QuoteRequestAttachmentDto> attachments,
        Guid tenantId,
        DateTime now) => attachments.Select(item => new QuoteRequestAttachmentDto
        {
            Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
            FileName = Clean(item.FileName),
            ContentType = CleanOrCurrent(item.ContentType, "application/octet-stream"),
            SizeBytes = Math.Max(0, item.SizeBytes),
            UploadedAtUtc = item.UploadedAtUtc == default ? now : item.UploadedAtUtc.ToUniversalTime(),
            TenantId = tenantId,
            BlobContainer = item.BlobContainer,
            BlobName = item.BlobName,
            BlobUrl = item.BlobUrl
        }).ToList();

    private static QuoteRequestSiteVisitScheduleDto? NormalizeSchedule(
        QuoteRequestSiteVisitScheduleDto? schedule,
        string actor,
        DateTime now)
    {
        if (schedule is null) return null;
        Required(schedule.VisitDate, nameof(schedule.VisitDate), 20);
        Required(schedule.WindowStart, nameof(schedule.WindowStart), 20);
        Required(schedule.WindowEnd, nameof(schedule.WindowEnd), 20);
        Required(schedule.SiteContact, nameof(schedule.SiteContact), 200);
        Required(schedule.AssignedFieldResource, nameof(schedule.AssignedFieldResource), 200);
        return new QuoteRequestSiteVisitScheduleDto
        {
            VisitDate = Clean(schedule.VisitDate),
            WindowStart = Clean(schedule.WindowStart),
            WindowEnd = Clean(schedule.WindowEnd),
            SiteContact = Clean(schedule.SiteContact),
            SiteContactPhone = Clean(schedule.SiteContactPhone),
            AssignedFieldResource = Clean(schedule.AssignedFieldResource),
            Notes = Clean(schedule.Notes),
            ScheduledAtUtc = now,
            ScheduledBy = actor
        };
    }

    private static QuoteRequestTimelineEventDto NewEvent(
        DateTime occurredAtUtc,
        string type,
        string actor,
        string label,
        string? note = null,
        QuoteRequestSubmittedPayloadDto? payload = null,
        QuoteRequestSiteVisitScheduleDto? siteVisitSchedule = null) => new()
    {
        Id = Guid.NewGuid(),
        OccurredAtUtc = occurredAtUtc,
        Type = type,
        Actor = actor,
        Label = label,
        Note = note,
        Payload = payload,
        SiteVisitSchedule = siteVisitSchedule
    };

    private static string BuildIntakeSummary(QuoteRequestSubmittedPayloadDto payload) =>
        string.Join(" · ", new[] { payload.ServiceType, payload.PropertyType, payload.RequestedTimeline }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static bool MatchesPublicRequest(TurnKeyOps.Lib.Entities.QuoteRequest existing, CreateQuoteRequestDto dto) =>
        string.Equals(existing.CompanyName, Clean(dto.CompanyName), StringComparison.Ordinal) &&
        string.Equals(existing.ContactName, Clean(dto.ContactName), StringComparison.Ordinal) &&
        string.Equals(existing.Email, Clean(dto.Email), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(existing.Phone, Clean(dto.Phone), StringComparison.Ordinal) &&
        string.Equals(existing.ServiceAddress, Clean(dto.ServiceAddress), StringComparison.Ordinal) &&
        string.Equals(existing.ServiceType, Clean(dto.ServiceType), StringComparison.Ordinal) &&
        string.Equals(existing.Need, Clean(dto.Need), StringComparison.Ordinal);

    private static bool DetailsChanged(QuoteRequestDto current, QuoteRequestDto next) =>
        !string.Equals(current.ContactName, Clean(next.ContactName), StringComparison.Ordinal) ||
        !string.Equals(current.Email, Clean(next.Email), StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(current.Phone, Clean(next.Phone), StringComparison.Ordinal) ||
        !string.Equals(current.ServiceAddress, Clean(next.ServiceAddress), StringComparison.Ordinal) ||
        !string.Equals(current.NextAction, Clean(next.NextAction), StringComparison.Ordinal);

    private static bool SchedulesEqual(QuoteRequestSiteVisitScheduleDto? left, QuoteRequestSiteVisitScheduleDto? right) =>
        left is null && right is null || left is not null && right is not null &&
        left.VisitDate == right.VisitDate && left.WindowStart == right.WindowStart &&
        left.WindowEnd == right.WindowEnd && left.SiteContact == right.SiteContact &&
        left.SiteContactPhone == right.SiteContactPhone &&
        left.AssignedFieldResource == right.AssignedFieldResource && left.Notes == right.Notes;

    private static List<string> NormalizeReasonCodes(IEnumerable<string>? codes) =>
        (codes ?? []).Select(Clean).Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private string Actor()
    {
        var name = Clean($"{_userContext.FirstName} {_userContext.LastName}");
        return name.Length == 0 ? "Tenant Admin" : name;
    }

    private static void Required(string? value, string field, int maxLength)
    {
        var cleaned = Clean(value);
        if (cleaned.Length == 0) throw new ArgumentException($"{field} is required.", field);
        if (cleaned.Length > maxLength) throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", field);
    }

    private static void ValidateEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try { _ = new MailAddress(value.Trim()); }
        catch (FormatException) { throw new ArgumentException("Email must be a valid address.", "Email"); }
    }

    private static string PartitionKey(Guid tenantId) => RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private static string RowKey(Guid id) => RepositoryKeyHelper.ToRowKey(id);
    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
    private static string CleanOrCurrent(string? value, string current) =>
        string.IsNullOrWhiteSpace(value) ? current : value.Trim();
}
