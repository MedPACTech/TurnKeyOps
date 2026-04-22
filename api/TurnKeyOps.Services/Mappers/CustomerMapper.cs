using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services.Mappers;

public static class CustomerMapper
{
    public static CustomerDto ToDto(Customer entity)
    {
        var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
        return new CustomerDto
        {
            Id = id,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            CompanyName = entity.CompanyName,
            Email = entity.Email,
            Phone = entity.Phone,
            Address = entity.Address,
            City = entity.City,
            State = entity.State,
            Zip = entity.Zip,
            Notes = entity.Notes,
            DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
            DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
        };
    }

    public static Customer ToEntity(CustomerDto dto, string partitionKey)
    {
        return new Customer
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CompanyName = dto.CompanyName,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Zip = dto.Zip,
            Notes = dto.Notes,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
