using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPromptTemplateRepository : IBaseRepositoryAsync<PromptTemplate>
    {
        Task<PromptTemplate?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IEnumerable<PromptTemplate>> GetAllAsync(string partitionKey);
    }
}
