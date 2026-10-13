using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;

namespace TurnKeyOps.Services;

public interface IFinanceSourceCoordinator
{
    Task<IAsyncDisposable> AcquireAsync(Guid tenant, CancellationToken ct = default);
}

// Infinite leases deliberately fail closed after a process crash. Do not use an expiring
// mutex for cross-store accounting: expiration can allow a paused writer past period close.
// Recovery requires stopping writers and explicitly breaking this tenant's storage lease.
public sealed class FinanceSourceCoordinator(IConfiguration configuration) : IFinanceSourceCoordinator
{
    public const string Container = "finance-source-locks";
    public async Task<IAsyncDisposable> AcquireAsync(Guid tenant, CancellationToken ct = default)
    {
        if (tenant == Guid.Empty) throw new UnauthorizedAccessException();
        var connection = configuration["IBeam:Repositories:AzureTables:ConnectionString"]
            ?? throw new InvalidOperationException("Finance storage not configured.");
        var container = new BlobServiceClient(connection, new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_12_02)).GetBlobContainerClient(Container);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = container.GetBlobClient($"{tenant:N}/invoice-source");
        try { await blob.UploadAsync(BinaryData.FromString("Finance source serialization fence"), overwrite: false, ct); }
        catch (RequestFailedException ex) when (ex.Status == 409) { }
        var lease = blob.GetBlobLeaseClient(Guid.NewGuid().ToString());
        // Bound acquisition wait; an orphan lease is an operational alert, not an automatic unlock.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (true)
            {
                try { await lease.AcquireAsync(TimeSpan.FromSeconds(-1), cancellationToken: timeout.Token); return new Handle(lease); }
                catch (RequestFailedException ex) when (ex.Status == 409)
                { await Task.Delay(100, timeout.Token); }
            }
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        { throw new InvalidOperationException("Invoice accounting coordination is busy. Retry shortly; an owner must review persistent source-lock failures before recovery.", ex); }
    }
    private sealed class Handle(BlobLeaseClient lease) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            // Never use the request's cancelled token to release a committed operation's fence.
            await lease.ReleaseAsync(cancellationToken: CancellationToken.None);
        }
    }
}
