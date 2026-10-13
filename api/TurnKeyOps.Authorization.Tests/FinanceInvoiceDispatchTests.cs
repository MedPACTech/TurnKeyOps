using Azure;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using Xunit;

namespace TurnKeyOps.Authorization.Tests;

public sealed class FinanceInvoiceDispatchTests
{
    [Fact]
    public async Task CrashAfterLedgerCommitBeforeAckReplaysWithoutDuplicateRevenueOrCash()
    {
        var f = new Fixture(); f.FailAck = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.True(f.Source.FinanceDispatchPending);
        Assert.Equal(2, f.State.Journals.Count);
        f.FailAck = false;
        Assert.True(await f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.False(f.Source.FinanceDispatchPending);
        Assert.Equal(2, f.State.Journals.Count);
        Assert.Single(f.State.Settlements);
        Assert.Equal(60, FinanceLedger.Balance(f.State, Assert.Single(f.State.OpenItems), DateOnly.MaxValue));
        Assert.False(await f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
    }

    [Fact]
    public async Task ClosedPeriodLeavesSourceDurableAndRetriesAfterAuthorizedReopen()
    {
        var f = new Fixture(); f.State.Periods[0].Status = "CLOSED";
        await Assert.ThrowsAsync<ArgumentException>(() => f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.Empty(f.State.Journals);
        Assert.True(f.Source.FinanceDispatchPending);
        Assert.NotNull(f.Source.FinanceDispatchError);
        f.State.Periods[0].Status = "OPEN";
        Assert.True(await f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.Null(f.Source.FinanceDispatchError);
    }

    [Fact]
    public async Task UninitializedFinanceDoesNotSilentlyCreateLedgerOrAcknowledgeSource()
    {
        var f = new Fixture(); f.State = new();
        Assert.False(await f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.True(f.Source.FinanceDispatchPending);
        Assert.Empty(f.State.Journals);
        Assert.NotNull(f.Source.FinanceDispatchAttemptedAtUtc);
        Assert.Contains("not initialized",f.Source.FinanceDispatchError);
    }

    [Fact]
    public async Task CrossTenantPayloadAndCustomerAreRejectedWithoutPosting()
    {
        var f = new Fixture(); f.Source.WorkflowPayloadBlobName = $"{Guid.NewGuid():N}/{f.Source.Id:N}/wrong.json";
        await Assert.ThrowsAsync<ArgumentException>(() => f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.Empty(f.State.Journals);
        f.Source.WorkflowPayloadBlobName = $"{f.Tenant:N}/{f.Source.Id:N}/source.json";
        f.Customer.PartitionKey = RepositoryKeyHelper.ToTenantPartitionKey(Guid.NewGuid());
        await Assert.ThrowsAsync<ArgumentException>(() => f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.Empty(f.State.Journals);
    }

    [Fact]
    public async Task ConcurrentDispatchSerializesSourceAndPostsOnce()
    {
        var f = new Fixture();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id)));
        Assert.Single(results, x => x);
        Assert.Equal(2, f.State.Journals.Count);
        Assert.Single(f.State.Settlements);
    }

    [Fact]
    public async Task FenceFailureLeavesDurableSourceAndRecordsAttemptForFairRetryDiscovery()
    {
        var coordinator=new Mock<IFinanceSourceCoordinator>();
        coordinator.Setup(c=>c.AcquireAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Orphaned source fence"));
        var f=new Fixture(coordinator.Object);
        await Assert.ThrowsAsync<AggregateException>(()=>f.Dispatch.RunOnceAsync());
        Assert.True(f.Source.FinanceDispatchPending);Assert.NotNull(f.Source.FinanceDispatchAttemptedAtUtc);
        Assert.NotNull(f.Source.FinanceDispatchError);Assert.Empty(f.State.Journals);
    }

    [Fact]
    public async Task SourceSnapshotChangedAfterPostingFailsRatherThanEditingHistory()
    {
        var f = new Fixture(); await f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id);
        f.Source.FinanceDispatchPending = true; f.Payload.Payments[0].Amount = 41;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.Equal(40, Assert.Single(f.State.Settlements).Amount);
    }

    [Fact]
    public async Task LegacyOpeningImportAlsoFencesHistoricalEventsWithoutInventingMigrationMetadata()
    {
        var f = new Fixture(); var cutover = new DateOnly(2026, 10, 1);
        var opening = FinanceLedger.Post(f.State, cutover, "opening-import", "legacy-source", "Opening AR", [new("1100", 60, 0), new("3000", 0, 60)], "owner");
        f.State.OpenItems.Add(new(f.Source.Id, "AR", f.Customer.Id, f.Source.InvoiceNumber, cutover, new(2026, 10, 31), 60, opening.Id));
        f.Payload.Payments[0].OccurredAtUtc = cutover.ToDateTime(TimeOnly.MinValue);
        f.Payload.Payments.Add(new() { Id = Guid.NewGuid(), IdempotencyKey = "new-receipt", Status = "succeeded", Kind = "payment", Amount = 20, OccurredAtUtc = new(2026, 10, 2) });
        await f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id);
        Assert.Equal(2, f.State.Journals.Count);
        Assert.DoesNotContain(f.State.Journals, j => j.SourceType == "invoice");
        Assert.Equal(40, FinanceLedger.Balance(f.State, Assert.Single(f.State.OpenItems), DateOnly.MaxValue));
        Assert.Single(f.State.Settlements); Assert.Empty(f.State.MigrationBatches);
    }

    [Fact]
    public async Task HistoricalUnmappedReceivableCannotDisappearBehindOpeningCutoff()
    {
        var f = new Fixture();
        FinanceLedger.Post(f.State, new(2026, 10, 1), "opening-import", "legacy-source", "Opening cash", [new("1000", 50, 0), new("3000", 0, 50)], "owner");
        f.Payload.Payments[0].OccurredAtUtc = new(2026, 10, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Dispatch.DispatchAsync(f.Tenant, f.Source.Id));
        Assert.True(f.Source.FinanceDispatchPending);
        Assert.Single(f.State.Journals);
        Assert.Empty(f.State.OpenItems);
    }

    private sealed class Fixture
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public Invoice Source { get; private set; }
        public Customer Customer { get; }
        public InvoiceWorkflowPayloadDto Payload { get; }
        public FinanceState State { get; set; } = new();
        public FinanceInvoiceDispatch Dispatch { get; }
        public bool FailAck { get; set; }
        public Fixture(IFinanceSourceCoordinator? coordinator=null)
        {
            var partition = RepositoryKeyHelper.ToTenantPartitionKey(Tenant);
            Customer = new() { Id = Guid.NewGuid(), PartitionKey = partition };
            Source = new() { Id = Guid.NewGuid(), PartitionKey = partition, CustomerId = Customer.Id, Status = InvoiceStatus.Sent,
                InvoiceNumber = "INV-101", IssueDate = new(2026, 10, 1), DueDate = new(2026, 10, 31), Subtotal = 100, Total = 100, FinanceDispatchPending = true };
            Source.RowKey = RepositoryKeyHelper.ToRowKey(Source.Id);
            Source.WorkflowPayloadBlobName = $"{Tenant:N}/{Source.Id:N}/source.json";
            Payload = new() { SentAtUtc = Source.IssueDate, Payments = [new() { Id = Guid.NewGuid(), Amount = 40, Kind = "payment", Status = "succeeded", IdempotencyKey = "provider:event1", OccurredAtUtc = new(2026, 10, 2), Method = "bank" }] };
            FinanceLedger.Initialize(State);
            State.Periods.Add(new() { Id = "2026-10", Start = new(2026, 10, 1), End = new(2026, 10, 31) });
            var invoices = new Mock<IInvoiceRepository>();
            invoices.Setup(r=>r.ListFinanceDispatchPendingAsync(It.IsAny<int>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>new[]{JobConfigurationService.Clone(Source)});
            invoices.Setup(r => r.GetAsync(partition, Source.RowKey, It.IsAny<CancellationToken>())).ReturnsAsync(() => JobConfigurationService.Clone(Source));
            invoices.Setup(r => r.SaveAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).ReturnsAsync((Invoice source, CancellationToken ct) =>
            { if (FailAck && !source.FinanceDispatchPending) throw new InvalidOperationException("simulated acknowledgement outage"); Source = JobConfigurationService.Clone(source); return source; });
            var customers = new Mock<ICustomerRepository>();
            customers.Setup(r => r.GetAsync(partition, RepositoryKeyHelper.ToRowKey(Customer.Id), It.IsAny<CancellationToken>())).ReturnsAsync(() => Customer);
            var payloads = new Mock<IInvoiceWorkflowPayloadStore>(); payloads.Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => JobConfigurationService.Clone(Payload));
            var finance = new Mock<IFinanceStore>(); finance.Setup(s => s.ReadAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(() => JobConfigurationService.Clone(State));
            finance.Setup(s => s.SaveAsync(Tenant, It.IsAny<FinanceState>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((Guid tenant, FinanceState state, string expected, CancellationToken ct) =>
            { FinanceIntegrity.ValidateTransition(State, state); State = JobConfigurationService.Clone(state); return Task.CompletedTask; });
            Dispatch = new(invoices.Object, payloads.Object, customers.Object, finance.Object, coordinator??new Coordinator());
        }
    }
    private sealed class Coordinator : IFinanceSourceCoordinator
    {
        private readonly SemaphoreSlim gate = new(1);
        public async Task<IAsyncDisposable> AcquireAsync(Guid tenant, CancellationToken ct = default)
        { await gate.WaitAsync(ct); return new Handle(gate); }
        private sealed class Handle(SemaphoreSlim gate) : IAsyncDisposable
        { public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; } }
    }
}
