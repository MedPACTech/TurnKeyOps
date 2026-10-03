using System.Text.Json;
using Azure;
using MedInsights.Lib.Utils;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
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
    public async Task LocksmithJobsRequireJobTypeAndEligibleActiveTechnician()
    {
        var tenantId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var member = new TenantMembership
        {
            Id = memberId, TenantId = tenantId, UserId = Guid.NewGuid(), Role = "staff",
            MembershipStatus = "Active", SeatStatus = "Assigned"
        };
        var settingsJson = JsonSerializer.Serialize(new
        {
            locksmith = new
            {
                techCapabilities = new Dictionary<string, string[]> { [memberId.ToString()] = ["residential"] }
            }
        });
        var fixture = CreateFixture(tenantId, members: [member], settingsJson: settingsJson);
        var untyped = ScheduledJob(Guid.NewGuid(), "Crew A", 8, 12);
        var tradeError = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AddAsync(untyped));
        Assert.Contains("doors and locksmith trade", tradeError.Message);
        var job = ScheduledJob(Guid.NewGuid(), string.Empty, 8, 12);
        job.TradeType = TradeType.DoorsLocksmith;
        job.LocksmithJobType = "residential";
        job.AssignedTechnicianMembershipId = memberId;

        var saved = await fixture.Service.AddAsync(job);

        Assert.Equal(memberId, saved.AssignedTechnicianMembershipId);
        Assert.Equal("residential", saved.LocksmithJobType);
        Assert.StartsWith("Technician ", saved.Crew);

        var ineligible = ScheduledJob(Guid.NewGuid(), string.Empty, 13, 16, Guid.NewGuid());
        ineligible.TradeType = TradeType.DoorsLocksmith;
        ineligible.LocksmithJobType = "commercial";
        ineligible.AssignedTechnicianMembershipId = memberId;
        var capabilityError = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AddAsync(ineligible));
        Assert.Contains("not eligible", capabilityError.Message);

        ineligible.LocksmithJobType = "residential";
        ineligible.AssignedTechnicianMembershipId = Guid.NewGuid();
        var membershipError = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AddAsync(ineligible));
        Assert.Contains("not an active assigned member", membershipError.Message);

        ineligible.AssignedTechnicianMembershipId = memberId;
        ineligible.LocksmithJobType = null;
        var typeError = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AddAsync(ineligible));
        Assert.Contains("job type is required", typeError.Message);
    }

    [Fact]
    public async Task LocksmithScheduleRejectsOverlappingJobsForTheSameTechnician()
    {
        var tenantId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var member = new TenantMembership
        {
            Id = memberId, TenantId = tenantId, UserId = Guid.NewGuid(), Role = "staff",
            MembershipStatus = "Active", SeatStatus = "Assigned"
        };
        var settingsJson = JsonSerializer.Serialize(new
        {
            locksmith = new
            {
                techCapabilities = new Dictionary<string, string[]> { [memberId.ToString()] = ["residential", "commercial"] }
            }
        });
        var fixture = CreateFixture(tenantId, members: [member], settingsJson: settingsJson);
        var first = ScheduledJob(Guid.NewGuid(), "Alex", 8, 12);
        first.TradeType = TradeType.DoorsLocksmith;
        first.LocksmithJobType = "residential";
        first.AssignedTechnicianMembershipId = memberId;
        await fixture.Service.AddAsync(first);

        var second = ScheduledJob(Guid.NewGuid(), "Different label", 11, 14, Guid.NewGuid());
        second.TradeType = TradeType.DoorsLocksmith;
        second.LocksmithJobType = "commercial";
        second.AssignedTechnicianMembershipId = memberId;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AddAsync(second));

        Assert.Contains("overlapping assignment", error.Message);
    }

    [Fact]
    public async Task LocksmithScheduleRejectsOverlappingSiteVisitForTheSameTechnician()
    {
        var tenantId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var member = new TenantMembership
        {
            Id = memberId, TenantId = tenantId, UserId = Guid.NewGuid(), Role = "staff",
            MembershipStatus = "Active", SeatStatus = "Assigned"
        };
        var settingsJson = JsonSerializer.Serialize(new
        {
            locksmith = new
            {
                techCapabilities = new Dictionary<string, string[]> { [memberId.ToString()] = ["residential"] }
            }
        });
        var job = ScheduledJob(Guid.NewGuid(), "Carl", 8, 12);
        job.TradeType = TradeType.DoorsLocksmith;
        job.LocksmithJobType = "residential";
        job.AssignedTechnicianMembershipId = memberId;
        var visit = new CalendarEvent
        {
            Id = Guid.NewGuid(), EventType = CalendarEventType.SiteVisit,
            AssignedTechnicianMembershipId = memberId,
            StartUtc = job.ScheduledStart!.Value.AddHours(1),
            EndUtc = job.ScheduledStart.Value.AddHours(2)
        };
        var fixture = CreateFixture(tenantId, members: [member], settingsJson: settingsJson,
            calendarEvents: [visit]);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AddAsync(job));

        Assert.Contains("overlapping calendar assignment", error.Message);
    }

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

    [Fact]
    public async Task JobCanBeRescheduledWhenTableAdapterReturnsNoETag()
    {
        var fixture = CreateFixture(TenantA, noETag: true);
        var created = await fixture.Service.AddAsync(ScheduledJob(Guid.NewGuid(), "Crew A", 8, 12));
        Assert.False(string.IsNullOrWhiteSpace(created.Version));

        var rescheduled = await fixture.Service.ScheduleAsync(created.Id, new JobScheduleInputDto
        {
            ScheduledStart = DateTime.UtcNow.Date.AddDays(2).AddHours(8),
            ScheduledEnd = DateTime.UtcNow.Date.AddDays(2).AddHours(12),
            Crew = "Crew A",
            ExpectedVersion = created.Version
        });

        Assert.NotEqual(created.Version, rescheduled.Version);
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

    private static Fixture CreateFixture(Guid tenantId, State? state = null, bool failSave = false, bool noETag = false,
        IReadOnlyList<TenantMembership>? members = null, string? settingsJson = null,
        IReadOnlyCollection<CalendarEvent>? calendarEvents = null)
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
                    if (!noETag) item.ETag = new ETag($"v{++state.Version}");
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
        var memberships = new Mock<ITenantMembershipRepository>();
        memberships.Setup(x => x.GetActiveAssignedByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members ?? []);
        var settings = new Mock<ITenantSettingsRepository>();
        settings.Setup(x => x.GetAsync(It.IsAny<string>(), "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(settingsJson is null ? null : new TenantSettingsDocument { ValuesJson = settingsJson });
        var calendar = new Mock<ICalendarEventRepository>();
        calendar.Setup(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendarEvents ?? []);
        var service = new JobService(jobs.Object, estimates.Object, payloads, invoices.Object, new User(tenantId),
            memberships.Object, settings.Object, calendar.Object);
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
