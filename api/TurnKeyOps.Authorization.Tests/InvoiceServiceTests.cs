using System.Text.Json;
using Azure;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using AppTimeZone = MedInsights.Lib.Utils.AppTimeZone;

namespace MedInsights.Authorization.Tests;

public sealed class InvoiceServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task CreateCalculatesTotalsOnServerAndUsesTenantPartitions()
    {
        var fixture = CreateFixture(TenantA);
        var input = DraftInvoice();
        input.Subtotal = 999m;
        input.TaxAmount = 999m;
        input.Total = 999m;
        input.AmountPaid = 999m;

        var result = await fixture.Service.AddAsync(input);

        Assert.Equal(50m, result.Subtotal);
        Assert.Equal(5m, result.TaxAmount);
        Assert.Equal(55m, result.Total);
        Assert.Equal(0m, result.AmountPaid);
        Assert.Equal(55m, result.BalanceDue);
        fixture.Invoices.Verify(x => x.SaveAsync(
            It.Is<Invoice>(item => item.PartitionKey == Partition(TenantA)), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Lines.Verify(x => x.SaveAsync(
            It.Is<InvoiceLineItem>(item => item.PartitionKey == RepositoryKeyHelper.ToTenantInvoicePartitionKey(TenantA, result.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentLifecycleIsIdempotentAndRefundRevokesRelease()
    {
        var fixture = CreateFixture(TenantA);
        var created = await fixture.Service.AddAsync(DraftInvoice(taxRate: 0m));
        await fixture.Service.SendAsync(created.Id, created.Version);

        var partial = await fixture.Service.RecordPaymentAsync(created.Id, Payment(20m, "evt-partial"));
        var duplicate = await fixture.Service.RecordPaymentAsync(created.Id, Payment(20m, "evt-partial"));
        var paid = await fixture.Service.RecordPaymentAsync(created.Id, Payment(30m, "evt-full"));
        var refunded = await fixture.Service.RecordRefundAsync(created.Id, Payment(30m, "evt-refund"));

        Assert.Equal(InvoiceStatus.PartiallyPaid, partial.Status);
        Assert.False(partial.JobRelease.IsEligible);
        Assert.Single(duplicate.Payments, item => item.IdempotencyKey == "evt-partial");
        Assert.Equal(InvoiceStatus.Paid, paid.Status);
        Assert.True(paid.JobRelease.IsEligible);
        Assert.Equal(20m, refunded.AmountPaid);
        Assert.Equal(InvoiceStatus.PartiallyPaid, refunded.Status);
        Assert.False(refunded.JobRelease.IsEligible);
        Assert.Contains(refunded.AuditEvents, item => item.Type == "refund_reconciled");
    }

    [Fact]
    public async Task OutOfOrderRefundReconcilesWhenPaymentArrivesLater()
    {
        var fixture = CreateFixture(TenantA);
        var created = await fixture.Service.AddAsync(DraftInvoice(taxRate: 0m));
        await fixture.Service.SendAsync(created.Id, null);

        var refundFirst = await fixture.Service.ReconcileProviderEventAsync(TenantA, created.Id, new InvoicePaymentInputDto
        {
            Kind = "refund", Amount = 20m, Provider = "Stripe", Method = "Stripe",
            IdempotencyKey = "stripe:refund", ExternalReference = "re_1", Status = "succeeded"
        });
        var paymentLater = await fixture.Service.ReconcileProviderEventAsync(TenantA, created.Id, new InvoicePaymentInputDto
        {
            Kind = "payment", Amount = 70m, Provider = "Stripe", Method = "Stripe",
            IdempotencyKey = "stripe:payment", ExternalReference = "pi_1", Status = "succeeded"
        });

        Assert.Equal(0m, refundFirst.AmountPaid);
        Assert.Equal(50m, paymentLater.AmountPaid);
        Assert.True(paymentLater.JobRelease.IsEligible);
    }

    [Fact]
    public async Task ReminderPolicyRequiresSentBalanceAndEnforcesCooldown()
    {
        var fixture = CreateFixture(TenantA);
        var created = await fixture.Service.AddAsync(DraftInvoice(taxRate: 0m));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.RecordReminderAsync(created.Id, new() { Channel = "email" }));
        await fixture.Service.SendAsync(created.Id, null);

        var first = await fixture.Service.RecordReminderAsync(created.Id, new() { Channel = "email", IdempotencyKey = "rem-1" });
        var duplicate = await fixture.Service.RecordReminderAsync(created.Id, new() { Channel = "email", IdempotencyKey = "rem-1" });

        Assert.Single(first.Reminders);
        Assert.Single(duplicate.Reminders);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.RecordReminderAsync(created.Id, new()
        {
            Channel = "email", IdempotencyKey = "rem-2"
        }));
    }

    [Fact]
    public async Task TenantPartitionPreventsCrossTenantReadsAndMutations()
    {
        var shared = new State();
        var tenantA = CreateFixture(TenantA, shared);
        var created = await tenantA.Service.AddAsync(DraftInvoice(taxRate: 0m));
        await tenantA.Service.SendAsync(created.Id, null);
        var tenantB = CreateFixture(TenantB, shared);

        Assert.Null(await tenantB.Service.GetAsync(created.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => tenantB.Service.RecordPaymentAsync(created.Id, Payment(50m, "cross-tenant")));
    }

    [Fact]
    public async Task ApprovedEstimateSyncIsDurableAndIdempotent()
    {
        var quoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var fixture = CreateFixture(TenantA, approved:
        [
            new QuoteEstimateDto
            {
                Id = quoteId, QuoteRequestId = quoteId, RevisionNumber = 2, CustomerName = "Avery", SiteName = "North lot",
                ServiceSummary = "Concrete pad", SavedAtUtc = DateTime.UtcNow,
                Totals = new() { MaterialCost = 60m, LaborCost = 40m, EstimatedTotal = 100m },
                ScopeLineItems = ["Approved scope"],
                Delivery = new() { Status = "approved", Email = "avery@example.com", ApprovedAtUtc = DateTime.UtcNow }
            }
        ]);

        var first = await fixture.Service.SyncApprovedEstimatesAsync();
        var second = await fixture.Service.SyncApprovedEstimatesAsync();

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(quoteId, first.Single().Id);
        Assert.Equal(100m, first.Single().Total);
        Assert.Equal(2, first.Single().EstimateRevisionNumber);
        fixture.Invoices.Verify(x => x.SaveAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static InvoiceDto DraftInvoice(decimal taxRate = 0.10m) => new()
    {
        CustomerName = "Avery",
        CustomerEmail = "avery@example.com",
        RequiredDepositPercent = 50m,
        TaxRate = taxRate,
        IssueDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(30),
        LineItems = [new() { Description = "Concrete", Quantity = 2m, UnitPrice = 25m }]
    };

    private static InvoicePaymentInputDto Payment(decimal amount, string key) => new()
    {
        Amount = amount, Method = "ACH", IdempotencyKey = key, Status = "succeeded"
    };

    private static Fixture CreateFixture(Guid tenantId, State? state = null, IReadOnlyCollection<QuoteEstimateDto>? approved = null)
    {
        state ??= new State();
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string partition, string row, CancellationToken _) => state.Invoices.GetValueOrDefault((partition, row)));
        invoices.Setup(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string partition, CancellationToken _) => state.Invoices.Values.Where(item => item.PartitionKey == partition && !item.IsDeleted).ToArray());
        invoices.Setup(x => x.SaveAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice item, CancellationToken _) =>
            {
                item.ETag = new ETag($"v{++state.Version}");
                state.Invoices[(item.PartitionKey, item.RowKey)] = item;
                return item;
            });

        var lines = new Mock<IInvoiceLineItemRepository>();
        lines.Setup(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string partition, CancellationToken _) => state.Lines.Values.Where(item => item.PartitionKey == partition && !item.IsDeleted).ToArray());
        lines.Setup(x => x.SaveAsync(It.IsAny<InvoiceLineItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvoiceLineItem item, CancellationToken _) =>
            {
                state.Lines[(item.PartitionKey, item.RowKey)] = item;
                return item;
            });
        var estimates = new Mock<IEstimateRepository>();
        var estimateLines = new Mock<IEstimateLineItemRepository>();
        var quoteEstimates = new Mock<IQuoteEstimateService>();
        quoteEstimates.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(approved ?? []);
        var payloads = new MemoryPayloadStore(state);
        var service = new InvoiceService(invoices.Object, lines.Object, estimates.Object, estimateLines.Object,
            quoteEstimates.Object, payloads, new User(tenantId));
        return new(service, invoices, lines);
    }

    private static string Partition(Guid tenantId) => RepositoryKeyHelper.ToTenantPartitionKey(tenantId);

    private sealed record Fixture(InvoiceService Service, Mock<IInvoiceRepository> Invoices, Mock<IInvoiceLineItemRepository> Lines);

    private sealed class State
    {
        public int Version { get; set; }
        public Dictionary<(string Partition, string Row), Invoice> Invoices { get; } = [];
        public Dictionary<(string Partition, string Row), InvoiceLineItem> Lines { get; } = [];
        public Dictionary<string, InvoiceWorkflowPayloadDto> Payloads { get; } = [];
    }

    private sealed class MemoryPayloadStore(State state) : IInvoiceWorkflowPayloadStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public Task<string> SaveAsync(Guid tenantId, Guid invoiceId, InvoiceWorkflowPayloadDto payload, CancellationToken ct = default)
        {
            var name = $"{tenantId:N}/{invoiceId:N}/{Guid.NewGuid():N}.json";
            state.Payloads[name] = Clone(payload);
            return Task.FromResult(name);
        }

        public Task<InvoiceWorkflowPayloadDto> LoadAsync(string? blobName, CancellationToken ct = default) =>
            Task.FromResult(string.IsNullOrWhiteSpace(blobName) ? new InvoiceWorkflowPayloadDto() : Clone(state.Payloads[blobName]));

        public Task DeleteIfExistsAsync(string? blobName, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(blobName)) state.Payloads.Remove(blobName);
            return Task.CompletedTask;
        }

        private static InvoiceWorkflowPayloadDto Clone(InvoiceWorkflowPayloadDto value) =>
            JsonSerializer.Deserialize<InvoiceWorkflowPayloadDto>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    }

    private sealed class User(Guid tenantId) : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => tenantId;
        public Guid UserId => Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
