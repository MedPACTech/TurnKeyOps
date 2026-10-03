using System.Text.Json;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using RepositoryKeyHelper = TurnKeyOps.Lib.Utils.RepositoryKeyHelper;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;

public sealed class CalendarTenantIsolationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task CalendarOperationsUseTheCurrentTenantPartition()
    {
        var id = Guid.NewGuid();
        var partitionKey = RepositoryKeyHelper.ToTenantPartitionKey(TenantId);
        var entity = new CalendarEvent
        {
            Id = id, PartitionKey = partitionKey, RowKey = RepositoryKeyHelper.ToRowKey(id),
            Title = "Site visit", StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddHours(1)
        };
        var repository = new Mock<ICalendarEventRepository>();
        repository.Setup(item => item.GetAsync(partitionKey, entity.RowKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        repository.Setup(item => item.ListAsync(partitionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entity });
        var service = CreateService(repository);

        Assert.NotNull(await service.GetAsync(id));
        Assert.Single(await service.GetByDateRangeAsync(entity.StartUtc.AddMinutes(-1), entity.EndUtc.AddMinutes(1)));
        await service.UpdateAsync(new CalendarEventDto
        {
            Id = id, Title = "Updated site visit", StartUtc = entity.StartUtc, EndUtc = entity.EndUtc
        });
        await service.DeleteAsync(id);

        repository.Verify(item => item.GetAsync(partitionKey, entity.RowKey, It.IsAny<CancellationToken>()), Times.Exactly(3));
        repository.Verify(item => item.ListAsync(partitionKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForeignCalendarEventReturnedByRepositoryCannotBeReadOrChanged()
    {
        var id = Guid.NewGuid();
        var foreign = new CalendarEvent
        {
            Id = id, PartitionKey = RepositoryKeyHelper.ToTenantPartitionKey(Guid.NewGuid()),
            RowKey = RepositoryKeyHelper.ToRowKey(id), Title = "Other tenant"
        };
        var repository = new Mock<ICalendarEventRepository>();
        repository.Setup(item => item.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreign);
        var service = CreateService(repository);

        Assert.Null(await service.GetAsync(id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new CalendarEventDto { Id = id }));
        await service.DeleteAsync(id);

        repository.Verify(item => item.SaveAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LocksmithSiteVisitRequiresAQualifiedAssignedTechnician()
    {
        var memberId = Guid.NewGuid();
        var repository = new Mock<ICalendarEventRepository>();
        repository.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var jobs = new Mock<IJobRepository>();
        jobs.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = CreateLocksmithService(repository, jobs, memberId, ["residential", "commercial"]);
        var visit = Visit(memberId, "commercial");

        await service.AddAsync(visit);
        repository.Verify(item => item.SaveAsync(It.Is<CalendarEvent>(eventItem =>
            eventItem.LocksmithJobType == "commercial" && eventItem.AssignedTechnicianMembershipId == memberId),
            It.IsAny<CancellationToken>()), Times.Once);

        visit.LocksmithJobType = null;
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(visit));
        visit.LocksmithJobType = "commercial";
        visit.AssignedTechnicianMembershipId = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(visit));
    }

    [Fact]
    public async Task LocksmithSiteVisitCannotOverlapAnotherVisitOrScheduledJob()
    {
        var memberId = Guid.NewGuid();
        var visit = Visit(memberId, "residential");
        var repository = new Mock<ICalendarEventRepository>();
        repository.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CalendarEvent
            {
                Id = Guid.NewGuid(), EventType = CalendarEventType.SiteVisit,
                AssignedTechnicianMembershipId = memberId,
                StartUtc = visit.StartUtc.AddMinutes(30), EndUtc = visit.EndUtc.AddMinutes(30)
            } });
        var jobs = new Mock<IJobRepository>();
        jobs.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = CreateLocksmithService(repository, jobs, memberId, ["residential"]);

        var visitError = await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(visit));
        Assert.Contains("overlapping", visitError.Message);

        repository.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        jobs.Setup(item => item.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Job
            {
                Id = Guid.NewGuid(), Status = JobStatus.Scheduled,
                AssignedTechnicianMembershipId = memberId,
                ScheduledStart = visit.StartUtc.AddMinutes(15), ScheduledEnd = visit.EndUtc.AddMinutes(15)
            } });
        var jobError = await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(visit));
        Assert.Contains("overlapping", jobError.Message);
        repository.Verify(item => item.SaveAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CalendarEventService CreateService(Mock<ICalendarEventRepository> repository) =>
        new(repository.Object, Mock.Of<IWeatherService>(), new User(), Mock.Of<ITenantMembershipRepository>(),
            Mock.Of<ITenantSettingsRepository>(), Mock.Of<IJobRepository>());

    private static CalendarEventService CreateLocksmithService(
        Mock<ICalendarEventRepository> repository, Mock<IJobRepository> jobs,
        Guid memberId, string[] capabilities)
    {
        var memberships = new Mock<ITenantMembershipRepository>();
        memberships.Setup(item => item.GetActiveAssignedByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TenantMembership
            {
                Id = memberId, TenantId = TenantId, UserId = Guid.NewGuid(),
                Role = "staff", MembershipStatus = "Active", SeatStatus = "Assigned"
            }]);
        var settings = new Mock<ITenantSettingsRepository>();
        settings.Setup(item => item.GetAsync(It.IsAny<string>(), "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new TenantSettingsDocument
            {
                ValuesJson = JsonSerializer.Serialize(new
                {
                    locksmith = new
                    {
                        techCapabilities = new Dictionary<string, string[]> { [memberId.ToString()] = capabilities }
                    }
                })
            });
        return new CalendarEventService(repository.Object, Mock.Of<IWeatherService>(), new User(),
            memberships.Object, settings.Object, jobs.Object);
    }

    private static CalendarEventDto Visit(Guid memberId, string jobType) => new()
    {
        EventType = CalendarEventType.SiteVisit,
        Title = "Measure opening", StartUtc = DateTime.UtcNow.AddDays(1),
        EndUtc = DateTime.UtcNow.AddDays(1).AddHours(1),
        LocksmithJobType = jobType, AssignedTechnicianMembershipId = memberId
    };

    private sealed class User : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => CalendarTenantIsolationTests.TenantId;
        public Guid UserId => Guid.NewGuid();
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
