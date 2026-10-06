using Microsoft.Extensions.Configuration;
using TurnKeyOps.Lib.Entities;
namespace TurnKeyOps.Repositories;
public interface IJobNotificationStore
{
    Task<JobNotification?> GetAsync(string tenant,string key,CancellationToken ct);
    Task<JobNotification> SaveAsync(JobNotification receipt,bool create,CancellationToken ct);
    IAsyncEnumerable<JobNotification> ListAsync(string tenant,CancellationToken ct);
}
public sealed class JobNotificationStore(IConfiguration configuration):IJobNotificationStore
{
    private readonly VersionedEnvelopeStore<JobNotification> rows=new(configuration,"JobNotifications");
    public Task<JobNotification?> GetAsync(string tenant,string key,CancellationToken ct)=>rows.GetAsync(tenant,key,ct);
    public Task<JobNotification> SaveAsync(JobNotification receipt,bool create,CancellationToken ct)=>rows.SaveAsync(receipt,create,ct);
    public IAsyncEnumerable<JobNotification> ListAsync(string tenant,CancellationToken ct)=>rows.ListAsync(tenant,ct);
}
