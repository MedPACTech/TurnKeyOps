using System.Text.Json;
using Azure;
using MedInsights.Lib.Utils;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;

public sealed class JobWorkflowServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid InvoiceId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task CreatePersistsTenantScopedWorkflowAndRejectsCrewConflict()
    {
        var fixture = CreateFixture(TenantA);
        var first = await fixture.Service.AddAsync(ScheduledJob(Guid.NewGuid(), "Crew A", 8, 12));

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.AddAsync(ScheduledJob(Guid.NewGuid(), "crew a", 11, 14, Guid.NewGuid())));

        Assert.Contains("overlapping assignment", error.Message);
        Assert.Equal(Partition(TenantA), fixture.State.Jobs.Values.Single().PartitionKey);
        Assert.Contains(first.Activity, item => item.Type == "scheduled" && item.Actor == "Test User");
    }

    [Fact]
    public async Task MutationsRequireCurrentVersionAndEnforceTransitions()
    {
        var fixture = CreateFixture(TenantA);
        var created = await fixture.Service.AddAsync(ScheduledJob(Guid.NewGuid(), "Crew A", 8, 12));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateStatusAsync(created.Id, new()
        {
            Status = JobStatus.Completed,
            ExpectedVersion = created.Version
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ScheduleAsync(created.Id, new()
        {
            ScheduledStart = DateTime.UtcNow.Date.AddDays(2).AddHours(8),
            ScheduledEnd = DateTime.UtcNow.Date.AddDays(2).AddHours(12),
            Crew = "Crew A",
            ExpectedVersion = "stale"
        }));
    }

    [Fact]
    public async Task StatusPlanningAndNotesRecordActorAndTimestamp()
    {
        var fixture = CreateFixture(TenantA);
        var created = await fixture.Service.AddAsync(ScheduledJob(Guid.NewGuid(), "Crew A", 8, 12));
        var active = await fixture.Service.UpdateStatusAsync(created.Id, new()
        {
            Status = JobStatus.InProgress,
            Note = "Crew arrived",
            ExpectedVersion = created.Version
        });
        var planned = await fixture.Service.UpdatePlanningAsync(created.Id, new()
        {
            ExpectedVersion = active.Version,
            Planning = new JobPlanningDto
            {
                CustomerConfirmationStatus = "confirmed",
                Materials = [new() { Kind = "concrete", Status = "ordered", Quantity = 8m, Unit = "yard" }]
            }
        });
        var noted = await fixture.Service.AddNoteAsync(created.Id, new()
        {
            ExpectedVersion = planned.Version,
            Note = "Pump truck confirmed"
        });

        Assert.Equal("ordered", Assert.Single(noted.Planning.Materials).Status);
        Assert.Contains(noted.Activity, item => item.Type == "status_updated" && item.Actor == "Test User");
        Assert.Contains(noted.Activity, item => item.Type == "planning_updated" && item.OccurredAtUtc != default);
        Assert.Contains(noted.Activity, item => item.Type == "note" && item.Note == "Pump truck confirmed");
    }

    [Fact]
    public async Task TenantPartitionPreventsCrossTenantRead()
    {
        var state = new State();
        var tenantA = CreateFixture(TenantA, state);
        var created = await tenantA.Service.AddAsync(ScheduledJob(Guid.NewGuid(), "Crew A", 8, 12));
        var tenantB = CreateFixture(TenantB, state);

        Assert.Null(await tenantB.Service.GetAsync(created.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => tenantB.Service.AddNoteAsync(created.Id, new()
        {
            ExpectedVersion = created.Version,
            Note = "Cross tenant"
        }));
    }

    [Fact]
    public async Task RepositoryFailureCleansNewWorkflowPayload()
    {
        var fixture = CreateFixture(TenantA, failSave: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.AddAsync(ScheduledJob(Guid.NewGuid(), "Crew A", 8, 12)));

        Assert.Empty(fixture.State.Payloads);
    }

    private static JobDto ScheduledJob(Guid id, string crew, int startHour, int endHour, Guid? invoiceId = null)
    {
        var day = DateTime.UtcNow.Date.AddDays(1);
        return new JobDto
        {
            Id = id,
            InvoiceId = invoiceId ?? InvoiceId,
            Name = "North lot",
            Status = JobStatus.Scheduled,
            Crew = crew,
            ScheduledStart = day.AddHours(startHour),
            ScheduledEnd = day.AddHours(endHour),
            Planning = new() { CustomerConfirmationStatus = "pending" }
        };
    }

    private static Fixture CreateFixture(Guid tenantId, State? state = null, bool failSave = false)
    {
        state ??= new State();
        var jobs = new Mock<IJobRepository>();
        jobs.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string partition, string row, CancellationToken _) => state.Jobs.GetValueOrDefault((partition, row)));
        jobs.Setup(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string partition, CancellationToken _) => state.Jobs.Values.Where(item => item.PartitionKey == partition && !item.IsDeleted).ToArray());
        if (failSave)
        {
            jobs.Setup(x => x.SaveAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("storage unavailable"));
        }
        else
        {
            jobs.Setup(x => x.SaveAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Job item, CancellationToken _) =>
                {
                    item.ETag = new ETag($"v{++state.Version}");
                    state.Jobs[(item.PartitionKey, item.RowKey)] = item;
                    return item;
                });
        }

        var estimates = new Mock<IEstimateWorkflowPayloadStore>();
        estimates.Setup(x => x.SaveJobEstimateSnapshotAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<EstimateCalculationSnapshotDto?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        estimates.Setup(x => x.LoadJobEstimateSnapshotAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EstimateCalculationSnapshotDto?)null);
        var invoices = new Mock<IInvoiceService>();
        invoices.Setup(x => x.GetJobReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceJobReleaseDto { IsEligible = true, Reason = "Deposit rule satisfied." });
        var payloads = new MemoryPayloadStore(state);
        var service = new JobService(jobs.Object, estimates.Object, payloads, invoices.Object, new User(tenantId));
        return new(service, state);
    }

    private static string Partition(Guid tenantId) => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private sealed record Fixture(JobService Service, State State);

    private sealed class State
    {
        public int Version { get; set; }
        public Dictionary<(string Partition, string Row), Job> Jobs { get; } = [];
        public Dictionary<string, JobWorkflowPayloadDto> Payloads { get; } = [];
    }

    private sealed class MemoryPayloadStore(State state) : IJobWorkflowPayloadStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public Task<string> SaveAsync(Guid tenantId, Guid jobId, JobWorkflowPayloadDto payload, CancellationToken ct = default)
        {
            var name = $"{tenantId:N}/{jobId:N}/{Guid.NewGuid():N}.json";
            state.Payloads[name] = Clone(payload);
            return Task.FromResult(name);
        }

        public Task<JobWorkflowPayloadDto> LoadAsync(string? blobName, CancellationToken ct = default) =>
            Task.FromResult(string.IsNullOrWhiteSpace(blobName) ? new JobWorkflowPayloadDto() : Clone(state.Payloads[blobName]));

        public Task DeleteIfExistsAsync(string? blobName, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(blobName)) state.Payloads.Remove(blobName);
            return Task.CompletedTask;
        }

        private static JobWorkflowPayloadDto Clone(JobWorkflowPayloadDto value) =>
            JsonSerializer.Deserialize<JobWorkflowPayloadDto>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
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
