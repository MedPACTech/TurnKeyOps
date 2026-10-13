using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

/// <summary>
/// The dispatch marker and immutable payload pointer commit in the same invoice entity write.
/// Ledger source IDs are durable acknowledgments. No message can be lost in a send-after-save gap.
/// </summary>
public sealed class FinanceInvoiceDispatch(IInvoiceRepository invoices, IInvoiceWorkflowPayloadStore payloads,
    ICustomerRepository customers, IFinanceStore finance, IFinanceSourceCoordinator coordinator)
{
    public async Task<object> GetStatusAsync(Guid tenant, CancellationToken ct = default)
    {
        if (tenant == Guid.Empty) throw new UnauthorizedAccessException();
        var partition = RepositoryKeyHelper.ToTenantPartitionKey(tenant);
        var pending = (await invoices.ListAsync(partition, ct)).Where(i => !i.IsDeleted && i.PartitionKey == partition && i.FinanceDispatchPending).ToArray();
        return new { Pending = pending.Length, Blocked = pending.Count(i => i.FinanceDispatchError is not null),
            Items = pending.Take(100).Select(i => new { i.Id, i.InvoiceNumber, Error = i.FinanceDispatchError, LastAttemptUtc = i.FinanceDispatchAttemptedAtUtc }).ToArray() };
    }

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var pending = await invoices.ListFinanceDispatchPendingAsync(250, ct);
        var completed = 0;
        var failures = new List<Exception>();
        foreach (var source in pending)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
            // Never infer tenant from a customer or an untrusted event property.
            if (!Guid.TryParse(source.PartitionKey, out var tenant) || tenant == Guid.Empty ||
                source.PartitionKey != RepositoryKeyHelper.ToTenantPartitionKey(tenant) || source.RowKey != RepositoryKeyHelper.ToRowKey(source.Id))
                throw new InvalidOperationException("Invalid invoice dispatch storage identity.");
            if (await DispatchAsync(tenant, source.Id, ct)) completed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Acquisition/read failures happen before DispatchAsync can record diagnostics.
                // Conditional metadata writes cannot replace a newer invoice/payload pointer.
                source.FinanceDispatchAttemptedAtUtc=DateTime.UtcNow;
                source.FinanceDispatchError="Invoice source dispatch failed; review coordination/storage diagnostics.";
                try { await invoices.SaveAsync(source,ct); } catch { /* A newer source version wins. */ }
                failures.Add(ex);
            }
        }
        if (failures.Count > 0) throw new AggregateException("Some invoice sources remain pending; other sources were processed.", failures);
        return completed;
    }

    public async Task<bool> DispatchAsync(Guid tenant, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenant == Guid.Empty || invoiceId == Guid.Empty) throw new UnauthorizedAccessException();
        await using var fence = await coordinator.AcquireAsync(tenant, ct);
        var partition = RepositoryKeyHelper.ToTenantPartitionKey(tenant);
        var source = await invoices.GetAsync(partition, RepositoryKeyHelper.ToRowKey(invoiceId), ct);
        if (source is null || source.IsDeleted || !source.FinanceDispatchPending) return false;
        if (source.PartitionKey != partition || source.Id != invoiceId) throw new InvalidOperationException("Invoice dispatch tenant mismatch.");
        var state = await finance.ReadAsync(tenant, ct);
        if (state.Accounts.Count == 0)
        {
            // Onboarding is explicit. Record the attempt so uninitialized tenants cannot monopolize
            // the oldest-source batch and starve initialized tenants later in storage order.
            source.FinanceDispatchAttemptedAtUtc=DateTime.UtcNow;
            source.FinanceDispatchError="Finance is not initialized; the source remains queued.";
            await invoices.SaveAsync(source,ct);
            return false;
        }
        try
        {
            var customer = await customers.GetAsync(partition, RepositoryKeyHelper.ToRowKey(source.CustomerId), ct);
            if (customer is null || customer.IsDeleted || customer.Id != source.CustomerId || customer.PartitionKey != partition)
                throw new ArgumentException("Invoice Customer is unavailable in this tenant.");
            if (source.WorkflowPayloadBlobName is null || !source.WorkflowPayloadBlobName.StartsWith($"{tenant:N}/{invoiceId:N}/", StringComparison.Ordinal))
                throw new ArgumentException("Invalid invoice event payload identity.");
            var payload = await payloads.LoadAsync(source.WorkflowPayloadBlobName, ct);
            var dto = InvoiceMapper.ToDto(source);
            dto.Payments = payload.Payments;
            dto.SentAtUtc = payload.SentAtUtc;
            var expected = state.Version;
            var count = state.Journals.Count;
            FinanceInvoicePosting.Apply(state, dto, "system:invoice-dispatch");
            if (state.Journals.Count != count)
            {
                state.Audit.Add(new(Guid.NewGuid(), "invoice-dispatch", invoiceId.ToString(), "system:invoice-dispatch", DateTime.UtcNow,
                    "Durable invoice/payment source events reconciled.", FinanceLedger.Hash(new { invoiceId, source.WorkflowPayloadBlobName })));
                await finance.SaveAsync(tenant, state, expected, ct);
            }
            // Commit Finance first. A failed acknowledgment leaves the marker for idempotent replay.
            source.FinanceDispatchPending = false;
            source.FinanceDispatchError = null;
            source.FinanceDispatchAttemptedAtUtc = DateTime.UtcNow;
            await invoices.SaveAsync(source, ct);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            source.FinanceDispatchPending = true;
            source.FinanceDispatchAttemptedAtUtc = DateTime.UtcNow;
            // Do not persist provider payloads, payment references or exception traces in source diagnostics.
            source.FinanceDispatchError = ex is ArgumentException ? ex.Message[..Math.Min(ex.Message.Length, 500)] : "Finance dispatch failed; see correlated server logs.";
            try { await invoices.SaveAsync(source, ct); } catch { /* Never overwrite a newer source event; retry its live pointer. */ }
            throw;
        }
    }
}

public sealed class FinanceInvoiceWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<FinanceInvoiceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Enabled by default; rollout can suspend dispatch without losing durable source markers.
        if (configuration["Finance:InvoiceDispatch:Enabled"] == "false") return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<FinanceInvoiceDispatch>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Finance invoice dispatch remains pending; the next scan will retry."); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
