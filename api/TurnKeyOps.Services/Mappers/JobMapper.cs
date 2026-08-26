using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class JobMapper
{
    public static JobDto ToDto(Job entity)
    {
        var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
        return new JobDto
        {
            Id = id,
            Name = entity.Name,
            Description = entity.Description,
            TradeType = entity.TradeType,
            Status = entity.Status,
            CustomerId = entity.CustomerId,
            CustomerName = entity.CustomerName,
            JobSiteId = entity.JobSiteId,
            JobSiteName = entity.JobSiteName,
            EstimateId = entity.EstimateId,
            EstimateNumber = entity.EstimateNumber,
            InvoiceId = entity.InvoiceId,
            ProjectAddress = entity.ProjectAddress,
            ProjectName = entity.ProjectName,
            EstimateSnapshot = Deserialize<EstimateCalculationSnapshotDto>(entity.EstimateSnapshotJson),
            ScheduledStart = entity.ScheduledStart,
            ScheduledEnd = entity.ScheduledEnd,
            EstimatedTotal = entity.EstimatedTotal,
            InvoicedTotal = entity.InvoicedTotal,
            PaidTotal = entity.PaidTotal,
            Notes = entity.Notes,
            DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
            DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
        };
    }

    public static Job ToEntity(JobDto dto, string partitionKey)
    {
        return new Job
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
            Name = dto.Name,
            Description = dto.Description,
            TradeType = dto.TradeType,
            Status = dto.Status,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            JobSiteId = dto.JobSiteId,
            JobSiteName = dto.JobSiteName,
            EstimateId = dto.EstimateId,
            EstimateNumber = dto.EstimateNumber,
            InvoiceId = dto.InvoiceId,
            ProjectAddress = dto.ProjectAddress,
            ProjectName = dto.ProjectName,
            EstimateSnapshotBlobName = null,
            EstimateSnapshotJson = null,
            ScheduledStart = dto.ScheduledStart,
            ScheduledEnd = dto.ScheduledEnd,
            EstimatedTotal = dto.EstimatedTotal,
            InvoicedTotal = dto.InvoicedTotal,
            PaidTotal = dto.PaidTotal,
            Notes = dto.Notes,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json);
    }
}
