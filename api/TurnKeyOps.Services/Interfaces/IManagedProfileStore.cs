using Azure;
using Azure.Data.Tables;
using MedInsights.Lib.Entities;
namespace MedInsights.Services.Interfaces;
public interface IManagedProfileStore
{
    Task<UserProfile?> GetByKeysAsync(string partition, string row, CancellationToken ct);
    IAsyncEnumerable<UserProfile> ListAsync(string partition, CancellationToken ct);
    Task<UserProfile> AddAsync(Guid? tenant, UserProfile profile, CancellationToken ct);
    Task<UserProfile> UpdateAsync(Guid? tenant, UserProfile profile, TableUpdateMode mode, ETag? version, CancellationToken ct);
}
