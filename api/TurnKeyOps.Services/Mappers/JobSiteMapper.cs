using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class JobSiteMapper
{
    public static JobSiteDto ToDto(JobSite entity)
    {
        var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
        return new JobSiteDto
        {
            Id = id,
            Name = entity.Name,
            Address = entity.Address,
            City = entity.City,
            State = entity.State,
            Zip = entity.Zip,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            Notes = entity.Notes,
            DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
            DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
        };
    }

    public static JobSite ToEntity(JobSiteDto dto, string partitionKey)
    {
        return new JobSite
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
            Name = dto.Name,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Zip = dto.Zip,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Notes = dto.Notes,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
