using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IJobSiteRepository : IBaseRepositoryAsync<JobSite>
{
    Task<JobSite?> GetAsync(string partitionKey,string rowKey,CancellationToken ct=default);
}
