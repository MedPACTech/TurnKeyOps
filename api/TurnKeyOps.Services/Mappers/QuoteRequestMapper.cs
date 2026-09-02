using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class QuoteRequestMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static QuoteRequestDto ToDto(QuoteRequest entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        SubmittedAtUtc = Utc(entity.SubmittedAtUtc),
        CompanyName = entity.CompanyName,
        ContactName = entity.ContactName,
        CustomerName = entity.ContactName,
        Email = entity.Email,
        Phone = entity.Phone,
        SiteName = entity.SiteName,
        ServiceAddress = entity.ServiceAddress,
        ServiceType = entity.ServiceType,
        ProjectType = entity.ServiceType,
        PropertyType = entity.PropertyType,
        RequestedTimeline = entity.RequestedTimeline,
        PreferredTimeline = entity.RequestedTimeline,
        Priority = entity.Priority,
        Need = entity.Need,
        Message = entity.Need,
        Attachments = Deserialize(entity.AttachmentsJson, new List<QuoteRequestAttachmentDto>()),
        Source = entity.Source,
        Status = entity.Status,
        AssignedTo = entity.AssignedTo,
        NextAction = entity.NextAction,
        IntakeSummary = entity.IntakeSummary,
        Qualification = Deserialize(entity.QualificationJson, new QuoteRequestQualificationDto()),
        SubmittedPayload = Deserialize<QuoteRequestSubmittedPayloadDto?>(entity.SubmittedPayloadJson, null),
        Timeline = Deserialize(entity.TimelineJson, new List<QuoteRequestTimelineEventDto>()),
        SiteVisitSchedule = Deserialize<QuoteRequestSiteVisitScheduleDto?>(entity.SiteVisitScheduleJson, null),
        UpdatedAtUtc = Utc(entity.DateUpdated)
    };

    public static QuoteRequest ToEntity(QuoteRequestDto dto) => new()
    {
        Id = dto.Id,
        TenantId = dto.TenantId,
        PartitionKey = RepositoryKeyHelper.ToTenantPartitionKey(dto.TenantId),
        RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
        SubmittedAtUtc = Utc(dto.SubmittedAtUtc),
        CompanyName = dto.CompanyName,
        ContactName = dto.ContactName,
        Email = dto.Email,
        Phone = dto.Phone,
        SiteName = dto.SiteName,
        ServiceAddress = dto.ServiceAddress,
        ServiceType = dto.ServiceType,
        PropertyType = dto.PropertyType,
        RequestedTimeline = dto.RequestedTimeline,
        Priority = dto.Priority,
        Need = dto.Need,
        Source = dto.Source,
        Status = dto.Status,
        AssignedTo = dto.AssignedTo,
        NextAction = dto.NextAction,
        IntakeSummary = dto.IntakeSummary,
        AttachmentsJson = Serialize(dto.Attachments),
        QualificationJson = Serialize(dto.Qualification),
        SubmittedPayloadJson = Serialize(dto.SubmittedPayload),
        TimelineJson = Serialize(dto.Timeline),
        SiteVisitScheduleJson = dto.SiteVisitSchedule is null ? null : Serialize(dto.SiteVisitSchedule),
        IsDeleted = false,
        DateCreated = Utc(dto.SubmittedAtUtc),
        DateUpdated = Utc(dto.UpdatedAtUtc)
    };

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback; }
        catch (JsonException) { return fallback; }
    }

    private static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
