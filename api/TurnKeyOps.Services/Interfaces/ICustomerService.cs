using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface ICustomerService
{
    Task<CustomerDto?> GetAsync(Guid id);
    Task<(IEnumerable<CustomerDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken);
    Task<IEnumerable<CustomerDto>> SearchAsync(string query);
    Task<CustomerDto> AddAsync(CustomerDto dto);
    Task<CustomerDto> UpdateAsync(CustomerDto dto);
    Task DeleteAsync(Guid id);
}
