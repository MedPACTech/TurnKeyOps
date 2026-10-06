using Azure;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories;
namespace TurnKeyOps.Services;

public sealed class JobNotificationDispatcher(IJobNotificationStore store)
{
    public async Task<JobNotification> DispatchAsync(JobNotification proposed,Func<JobNotification,CancellationToken,Task> send,CancellationToken ct)
    {
        var receipt=await store.GetAsync(proposed.PartitionKey,proposed.RowKey,ct);
        if(receipt is null){
            try{receipt=await store.SaveAsync(proposed,true,ct);}
            catch(RequestFailedException ex) when(ex.Status==409){receipt=await store.GetAsync(proposed.PartitionKey,proposed.RowKey,ct)??throw new InvalidOperationException("Notification reservation unavailable.");}
        }
        if(receipt.Status!="prepared")return receipt;
        // Only the winner of this CAS may call the provider. Sending is deliberately
        // never retried automatically: after a crash the provider outcome is unknown.
        receipt.Status="sending";receipt.AttemptedAtUtc=DateTime.UtcNow;
        try{receipt=await store.SaveAsync(receipt,false,ct);}
        catch(RequestFailedException ex) when(ex.Status==412){return await store.GetAsync(proposed.PartitionKey,proposed.RowKey,ct)??throw new InvalidOperationException("Notification receipt unavailable.");}
        try{await send(receipt,ct);}
        catch{
            receipt.Status="unconfirmed";
            try{await store.SaveAsync(receipt,false,CancellationToken.None);}catch{/* Durable sending receipt already requires review. */}
            return receipt;
        }
        receipt.Status="provider-accepted";receipt.ProviderAcceptedAtUtc=DateTime.UtcNow;
        try{return await store.SaveAsync(receipt,false,CancellationToken.None);}
        catch{
            // Provider accepted, but acknowledgement was not persisted. Never resend.
            receipt.Status="unconfirmed";return receipt;
        }
    }
}
