using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using System.Text.Json;

namespace TurnKeyOps.Services.Mappers;

public static class EstimateMapper
{
    public static EstimateDto ToDto(Estimate entity, IEnumerable<EstimateLineItem>? lineItems = null)
    {
        var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
        return new EstimateDto
        {
            Id = id,
            EstimateNumber = entity.EstimateNumber,
            Status = entity.Status,
            TradeType = entity.TradeType,
            AppointmentId = entity.AppointmentId,
            CustomerId = entity.CustomerId,
            CustomerName = entity.CustomerName,
            CustomerCompany = entity.CustomerCompany,
            JobId = entity.JobId,
            JobName = entity.JobName,
            JobSiteId = entity.JobSiteId,
            ConvertedJobId = entity.ConvertedJobId,
            ProjectAddress = entity.ProjectAddress,
            EstimatorName = entity.EstimatorName,
            ProjectName = entity.ProjectName,
            Subtotal = entity.Subtotal,
            TaxRate = entity.TaxRate,
            TaxAmount = entity.TaxAmount,
            Total = entity.Total,
            TotalSqft = entity.TotalSqft,
            DepthInches = entity.DepthInches,
            CubicYards = entity.CubicYards,
            NumberOfPours = entity.NumberOfPours,
            WallLinearFeet = entity.WallLinearFeet,
            StudCount = entity.StudCount,
            SentDate = entity.SentDate,
            SubmittedDate = entity.SubmittedDate,
            ExpiryDate = entity.ExpiryDate,
            RevisedDate = entity.RevisedDate,
            AwardedDate = entity.AwardedDate,
            RejectedDate = entity.RejectedDate,
            ConvertedToJobDate = entity.ConvertedToJobDate,
            SignatureDataUrl = entity.SignatureDataUrl,
            SignedByName = entity.SignedByName,
            SignedDate = entity.SignedDate,
            Notes = entity.Notes,
            StructuredInput = Deserialize<StructuredEstimateInputDto>(entity.StructuredInputJson),
            CalculationSnapshot = Deserialize<EstimateCalculationSnapshotDto>(entity.CalculationSnapshotJson),
            BobTranscript = new(),
            LineItems = lineItems?.Select(EstimateLineItemMapper.ToDto).ToList() ?? new(),
            DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
            DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
        };
    }

    public static Estimate ToEntity(EstimateDto dto, string partitionKey)
    {
        return new Estimate
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
            EstimateNumber = dto.EstimateNumber,
            Status = dto.Status,
            TradeType = dto.TradeType,
            AppointmentId = dto.AppointmentId,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerCompany = dto.CustomerCompany,
            JobId = dto.JobId,
            JobName = dto.JobName,
            JobSiteId = dto.JobSiteId,
            ConvertedJobId = dto.ConvertedJobId,
            ProjectAddress = dto.ProjectAddress,
            EstimatorName = dto.EstimatorName,
            ProjectName = dto.ProjectName,
            Subtotal = dto.Subtotal,
            TaxRate = dto.TaxRate,
            TaxAmount = dto.TaxAmount,
            Total = dto.Total,
            TotalSqft = dto.TotalSqft,
            DepthInches = dto.DepthInches,
            CubicYards = dto.CubicYards,
            NumberOfPours = dto.NumberOfPours,
            WallLinearFeet = dto.WallLinearFeet,
            StudCount = dto.StudCount,
            SentDate = dto.SentDate,
            SubmittedDate = dto.SubmittedDate,
            ExpiryDate = dto.ExpiryDate,
            RevisedDate = dto.RevisedDate,
            AwardedDate = dto.AwardedDate,
            RejectedDate = dto.RejectedDate,
            ConvertedToJobDate = dto.ConvertedToJobDate,
            SignatureDataUrl = dto.SignatureDataUrl,
            SignedByName = dto.SignedByName,
            SignedDate = dto.SignedDate,
            Notes = dto.Notes,
            StructuredInputBlobName = null,
            CalculationSnapshotBlobName = null,
            BobTranscriptBlobName = null,
            StructuredInputJson = null,
            CalculationSnapshotJson = null,
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
