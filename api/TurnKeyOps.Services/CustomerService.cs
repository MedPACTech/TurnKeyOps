using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repo;
    private readonly IUserContext _userContext;

    public CustomerService(ICustomerRepository repo, IUserContext userContext)
    {
        _repo = repo;
        _userContext = userContext;
    }

    private string PartitionKeyForTenant() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<CustomerDto?> GetAsync(Guid id)
    {
        var entity = await _repo.GetAsync(PartitionKeyForTenant(), RepositoryKeyHelper.ToRowKey(id));
        return entity is null || entity.IsDeleted || entity.PartitionKey != PartitionKeyForTenant()
            ? null : CustomerMapper.ToDto(entity);
    }

    public async Task<(IEnumerable<CustomerDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken)
    {
        var pk = PartitionKeyForTenant();
        var offset = int.TryParse(continuationToken, out var parsed) ? parsed : 0;
        var all = (await _repo.ListAsync(pk))
            .OrderByDescending(x => x.DateUpdated)
            .ToList();
        var items = all.Skip(offset).Take(pageSize).ToList();
        var token = offset + items.Count < all.Count ? (offset + items.Count).ToString() : null;
        return (items.Where(x => !x.IsDeleted).Select(CustomerMapper.ToDto), token);
    }

    public async Task<IEnumerable<CustomerDto>> SearchAsync(string query)
    {
        var pk = PartitionKeyForTenant();
        var all = await _repo.ListAsync(pk);
        var q = query.ToLowerInvariant();
        return all
            .Where(c => c.PartitionKey == pk && !c.IsDeleted &&
                (c.FirstName.ToLowerInvariant().Contains(q) ||
                 c.LastName.ToLowerInvariant().Contains(q) ||
                 (c.CompanyName?.ToLowerInvariant().Contains(q) ?? false) ||
                 (c.Email?.ToLowerInvariant().Contains(q) ?? false)))
            .Select(CustomerMapper.ToDto);
    }

    public async Task<CustomerDto> AddAsync(CustomerDto dto)
    {
        ValidateType(dto);
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        var entity = CustomerMapper.ToEntity(dto, PartitionKeyForTenant());
        await _repo.SaveAsync(entity);
        return CustomerMapper.ToDto(entity);
    }

    public async Task<CustomerDto> UpdateAsync(CustomerDto dto)
    {
        var existing = await _repo.GetAsync(PartitionKeyForTenant(), RepositoryKeyHelper.ToRowKey(dto.Id))
            ?? throw new ArgumentException("Customer not found", nameof(dto.Id));
        if (existing.IsDeleted || existing.PartitionKey != PartitionKeyForTenant())
            throw new ArgumentException("Customer not found", nameof(dto.Id));
        dto.CustomerType ??= existing.CustomerType;
        ValidateType(dto);
        var entity = CustomerMapper.ToEntity(dto, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await _repo.SaveAsync(entity);
        return CustomerMapper.ToDto(entity);
    }

    private static void ValidateType(CustomerDto dto)
    {
        if (dto.CustomerType is null) return; // Older clients and unclassified records remain valid.
        if (dto.CustomerType is not ("residential" or "commercial")) throw new ArgumentException("Choose Residential or Commercial.");
        if (dto.CustomerType == "commercial" && string.IsNullOrWhiteSpace(dto.CompanyName)) throw new ArgumentException("Enter the company name.");
        if (dto.CustomerType == "residential" && string.IsNullOrWhiteSpace(dto.FirstName) && string.IsNullOrWhiteSpace(dto.LastName)) throw new ArgumentException("Enter the customer name.");
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetAsync(PartitionKeyForTenant(), RepositoryKeyHelper.ToRowKey(id));
        if (entity is null || entity.IsDeleted || entity.PartitionKey != PartitionKeyForTenant()) return;
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }
}
