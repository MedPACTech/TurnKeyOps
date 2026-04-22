using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class CalendarEventMapper
{
    public static CalendarEventDto ToDto(CalendarEvent entity)
    {
        var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
        return new CalendarEventDto
        {
            Id = id,
            Title = entity.Title,
            Description = entity.Description,
            EventType = entity.EventType,
            StartUtc = DateTime.SpecifyKind(entity.StartUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(entity.EndUtc, DateTimeKind.Utc),
            AllDay = entity.AllDay,
            JobId = entity.JobId,
            JobName = entity.JobName,
            JobSiteId = entity.JobSiteId,
            JobSiteName = entity.JobSiteName,
            Color = entity.Color,
            Weather = entity.WeatherSummary != null ? new WeatherForecastDto
            {
                Summary = entity.WeatherSummary,
                TempHigh = entity.WeatherTempHigh,
                TempLow = entity.WeatherTempLow,
                PrecipChance = entity.WeatherPrecipChance,
                Icon = entity.WeatherIcon
            } : null,
            DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
            DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
        };
    }

    public static CalendarEvent ToEntity(CalendarEventDto dto, string partitionKey)
    {
        return new CalendarEvent
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
            Title = dto.Title,
            Description = dto.Description,
            EventType = dto.EventType,
            StartUtc = DateTime.SpecifyKind(dto.StartUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(dto.EndUtc, DateTimeKind.Utc),
            AllDay = dto.AllDay,
            JobId = dto.JobId,
            JobName = dto.JobName,
            JobSiteId = dto.JobSiteId,
            JobSiteName = dto.JobSiteName,
            Color = dto.Color,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
