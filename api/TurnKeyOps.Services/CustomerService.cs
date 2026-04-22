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
        var entity = await _repo.GetByIdAsync(id);
        return entity is null || entity.IsDeleted ? null : CustomerMapper.ToDto(entity);
    }

    public async Task<(IEnumerable<CustomerDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken)
    {
        var pk = PartitionKeyForTenant();
        var offset = int.TryParse(continuationToken, out var parsed) ? parsed : 0;
        var all = (await _repo.GetAllAsync(false, false))
            .Where(x => x.PartitionKey == pk && !x.IsDeleted)
            .OrderByDescending(x => x.DateUpdated)
            .ToList();
        var items = all.Skip(offset).Take(pageSize).ToList();
        var token = offset + items.Count < all.Count ? (offset + items.Count).ToString() : null;
        return (items.Where(x => !x.IsDeleted).Select(CustomerMapper.ToDto), token);
    }

    public async Task<IEnumerable<CustomerDto>> SearchAsync(string query)
    {
        var pk = PartitionKeyForTenant();
        var all = await _repo.GetAllAsync(false, false);
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
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        var entity = CustomerMapper.ToEntity(dto, PartitionKeyForTenant());
        await _repo.SaveAsync(entity);
        return CustomerMapper.ToDto(entity);
    }

    public async Task<CustomerDto> UpdateAsync(CustomerDto dto)
    {
        var existing = await _repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException("Customer not found", nameof(dto.Id));
        var entity = CustomerMapper.ToEntity(dto, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await _repo.SaveAsync(entity);
        return CustomerMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null) return;
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }
}
