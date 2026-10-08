using Azure;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories;
using TurnKeyOps.Services;
namespace TurnKeyOps.Authorization.Tests;
public sealed class JobNotificationTests
{
    [Fact] public async Task ConcurrentAndRepeatedRequestsSendOnce()
    {
        var store=new Store();var dispatcher=new JobNotificationDispatcher(store);var calls=0;
        async Task Send(JobNotification r,CancellationToken ct){Interlocked.Increment(ref calls);await Task.Delay(20,ct);}
        var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>dispatcher.DispatchAsync(Receipt(),Send,default)));
        Assert.Equal(1,calls);
        var replay=await dispatcher.DispatchAsync(Receipt(),Send,default);
        Assert.Equal("provider-accepted",replay.Status);Assert.Equal(1,calls);
        Assert.Equal("Frozen message",replay.Body);
    }
    [Fact] public async Task ProviderTimeoutCannotBeBlindlyRetried()
    {
        var store=new Store();var dispatcher=new JobNotificationDispatcher(store);var calls=0;
        Task Send(JobNotification r,CancellationToken ct){calls++;throw new TimeoutException("Outcome unknown");}
        Assert.Equal("unconfirmed",(await dispatcher.DispatchAsync(Receipt(),Send,default)).Status);
        await dispatcher.DispatchAsync(Receipt(),Send,default);Assert.Equal(1,calls);
    }
    [Fact] public async Task LostAcknowledgementCannotCauseDuplicateDelivery()
    {
        var store=new Store{FailAcknowledgement=true};var dispatcher=new JobNotificationDispatcher(store);var calls=0;
        Task Send(JobNotification r,CancellationToken ct){calls++;return Task.CompletedTask;}
        Assert.Equal("unconfirmed",(await dispatcher.DispatchAsync(Receipt(),Send,default)).Status);
        Assert.Equal("sending",(await dispatcher.DispatchAsync(Receipt(),Send,default)).Status);
        Assert.Equal(1,calls);
    }
    [Fact] public async Task FailedReservationNeverCallsTransportAndTenantsAreIndependent()
    {
        var store=new Store{FailReservation=true};var dispatcher=new JobNotificationDispatcher(store);var calls=0;
        Task Send(JobNotification r,CancellationToken ct){calls++;return Task.CompletedTask;}
        await Assert.ThrowsAsync<InvalidOperationException>(()=>dispatcher.DispatchAsync(Receipt(),Send,default));Assert.Equal(0,calls);
        store.FailReservation=false;
        await dispatcher.DispatchAsync(Receipt(),Send,default);var foreign=Receipt();foreign.PartitionKey="tenant-b";
        await dispatcher.DispatchAsync(foreign,Send,default);Assert.Equal(2,calls);
    }
    private static JobNotification Receipt()=>new(){PartitionKey="tenant-a",RowKey="request-one",JobId=Guid.NewGuid(),Body="Frozen message"};
    private sealed class Store:IJobNotificationStore
    {
        private readonly Dictionary<string,JobNotification> rows=[];private int version;
        public bool FailAcknowledgement,FailReservation;
        private static JobNotification Copy(JobNotification r){var c=JobConfigurationService.Clone(r);c.ETag=r.ETag;return c;}
        public Task<JobNotification?> GetAsync(string tenant,string key,CancellationToken ct){lock(rows)return Task.FromResult(rows.TryGetValue(tenant+key,out var r)?Copy(r):null);}
        public Task<JobNotification> SaveAsync(JobNotification r,bool create,CancellationToken ct){lock(rows){
            if(FailReservation&&r.Status=="sending"||FailAcknowledgement&&r.Status=="provider-accepted")throw new InvalidOperationException("Storage interrupted");
            var key=r.PartitionKey+r.RowKey;rows.TryGetValue(key,out var old);
            if(create&&old is not null)throw new RequestFailedException(409,"Exists");
            if(!create&&(old is null||old.ETag!=r.ETag))throw new RequestFailedException(412,"Stale");
            r.ETag=new ETag((++version).ToString());rows[key]=Copy(r);return Task.FromResult(Copy(r));
        }}
        public async IAsyncEnumerable<JobNotification> ListAsync(string tenant,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct){await Task.CompletedTask;yield break;}
    }
}
