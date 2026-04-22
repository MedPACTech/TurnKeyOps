using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class EstimateLineItemMapper
{
    public static EstimateLineItemDto ToDto(EstimateLineItem entity)
    {
        return new EstimateLineItemDto
        {
            Id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey),
            EstimateId = entity.EstimateId,
            SortOrder = entity.SortOrder,
            Description = entity.Description,
            Category = entity.Category,
            Quantity = entity.Quantity,
            Unit = entity.Unit,
            UnitPrice = entity.UnitPrice,
            LineTotal = entity.LineTotal,
            IsCalculated = entity.IsCalculated,
            Notes = entity.Notes
        };
    }

    public static EstimateLineItem ToEntity(EstimateLineItemDto dto, string partitionKey)
    {
        return new EstimateLineItem
        {
            Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id),
            EstimateId = dto.EstimateId,
            SortOrder = dto.SortOrder,
            Description = dto.Description,
            Category = dto.Category,
            Quantity = dto.Quantity,
            Unit = dto.Unit,
            UnitPrice = dto.UnitPrice,
            LineTotal = dto.LineTotal,
            IsCalculated = dto.IsCalculated,
            Notes = dto.Notes,
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
