using Azure;using Azure.Data.Tables;using Microsoft.Extensions.Configuration;using TurnKeyOps.Lib.Entities;using TurnKeyOps.Repositories;
namespace TurnKeyOps.Authorization.Tests;
public sealed class JobStorageTests
{
    [AzuriteFact][Trait("Category","StorageIntegration")]
    public async Task ConcurrentCalendarWritersCannotReserveTheSamePersonOrEquipment()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var pk="CALENDAR-RACE-"+Guid.NewGuid().ToString("N");var table=new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("CalendarEvents");
        var store=new CalendarReservationStore(config);var reads=new VersionedEnvelopeStore<CalendarEvent>(config,"CalendarEvents");
        CalendarEvent Event(string partition,Guid member,string resource)=>new(){Id=Guid.NewGuid(),RowKey=Guid.NewGuid().ToString("N"),PartitionKey=partition,StartUtc=DateTime.UtcNow.AddDays(1),EndUtc=DateTime.UtcNow.AddDays(1).AddHours(2),MembershipIds=[member],ResourceIds=[resource]};
        try{
            var member=Guid.NewGuid();var a=Event(pk,member,"lift");var b=Event(pk,member,"other");
            async Task<bool> TrySave(CalendarEvent e){try{await new CalendarReservationStore(config).SaveAsync(e,default);return true;}catch(ArgumentException){return false;}}
            var results=await Task.WhenAll(TrySave(a),TrySave(b));Assert.Single(results.Where(ok=>ok));
            var winner=results[0]?a:b;var loser=results[0]?b:a;
            await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(Event(pk,Guid.NewGuid(),winner.ResourceIds[0].ToUpperInvariant()),default));
            var foreign=Event(pk+"other",member,"lift");await store.SaveAsync(foreign,default);
            var stale=(await reads.GetAsync(pk,winner.RowKey,default))!;
            winner.EventStatus="cancelled";await store.SaveAsync(winner,default);
            await store.SaveAsync(loser,default);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(stale,default));
            var adjacent=Event(pk,member,"independent");adjacent.StartUtc=loser.EndUtc;adjacent.EndUtc=adjacent.StartUtc.AddHours(1);await store.SaveAsync(adjacent,default);
        }finally{
            foreach(var partition in new[]{pk,pk+"other"})
                await foreach(var row in table.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {partition}")))await table.DeleteEntityAsync(partition,row.RowKey,ETag.All);
        }
    }
    [AzuriteFact][Trait("Category","StorageIntegration")]
    public async Task JobAndCalendarEnvelopesPreservePhysicalVersionsAndTenantIsolation()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var pk="JOBS-TEST-"+Guid.NewGuid().ToString("N");var id=Guid.NewGuid();var row=id.ToString("D");
        var jobs=new VersionedEnvelopeStore<Job>(config,"Jobs");var events=new VersionedEnvelopeStore<CalendarEvent>(config,"CalendarEvents");var tables=new TableServiceClient("UseDevelopmentStorage=true");
        try{
            await jobs.SaveAsync(new(){Id=id,PartitionKey=pk,RowKey=row,Name="Test",WorkflowPayloadBlobName="first"},true,default);
            var first=(await jobs.GetAsync(pk,row,default))!;var stale=(await jobs.GetAsync(pk,row,default))!;
            first.WorkflowPayloadBlobName="second";await jobs.SaveAsync(first,false,default);
            Assert.Equal(412,(await Assert.ThrowsAsync<RequestFailedException>(()=>jobs.SaveAsync(stale,false,default))).Status);
            Assert.Null(await jobs.GetAsync(pk+"other",row,default));
            var saved=await events.SaveAsync(new(){Id=id,PartitionKey=pk,RowKey=row,JobId=id,JobEventType="work",MembershipIds=[id]},true,default);
            var current=(await events.GetAsync(pk,row,default))!;Assert.Equal(saved.ETag,current.ETag);Assert.Equal(id,Assert.Single(current.MembershipIds));
            current.EventStatus="cancelled";await events.SaveAsync(current,false,default);
            Assert.Equal(412,(await Assert.ThrowsAsync<RequestFailedException>(()=>events.SaveAsync(saved,false,default))).Status);
        }finally{await tables.GetTableClient("Jobs").DeleteEntityAsync(pk,row,ETag.All);await tables.GetTableClient("CalendarEvents").DeleteEntityAsync(pk,row,ETag.All);}
    }
}
