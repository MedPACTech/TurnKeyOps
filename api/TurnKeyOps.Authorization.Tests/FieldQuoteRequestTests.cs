using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace MedInsights.Authorization.Tests;
public sealed class FieldQuoteRequestTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    [Fact]
    public async Task CreatesQualifiedFieldSourceAndIdempotentRetryKeepsSameRecord()
    {
        var (service, repository) = Fixture();
        var input = Input();
        var first = await service.CreateFieldAsync("carlzipf", input);
        var second = await service.CreateFieldAsync("carlzipf", input);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("qualified", first.Status);
        Assert.Equal("office", first.Source);
        Assert.Equal(TenantId, first.TenantId);
        repository.Verify(value => value.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task RejectsWrongTenantAndUnpermittedJobTypeBeforeWriting()
    {
        var (service, repository) = Fixture();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateFieldAsync("other", Input()));
        var input = Input(); input.PropertyType = "commercial";
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateFieldAsync("carlzipf", input));
        repository.Verify(value => value.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SourceListDetailAndUpdateHonorTechCapabilityAndAssignment()
    {
        var residential = Source("residential");
        var commercial = Source("commercial");
        var records = new[] { residential, commercial };
        var repository = new Mock<IQuoteRequestRepository>();
        repository.Setup(value => value.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(records);
        repository.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string row, CancellationToken _) => records.SingleOrDefault(item => item.RowKey == row));
        var settings = new Mock<ITenantSettingsRepository>();
        settings.Setup(value => value.GetAsync(It.IsAny<string>(), "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false)).ReturnsAsync(LocksmithQuotePricingTests.Settings());
        var members = new Mock<ITenantMembershipRepository>();
        members.Setup(value => value.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(TenantId), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership { Id = LocksmithQuotePricingTests.MembershipId, TenantId = TenantId, UserId = UserId, MembershipStatus = "Active", Role = "staff" });
        var jobs = new Mock<IJobRepository>();
        IReadOnlyCollection<Job> linked = [];
        jobs.Setup(value => value.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => linked);
        var options = Options.Create(new QuoteRequestTenantOptions { Tenants = new() { ["carlzipf"] = new() { TenantId = TenantId } } });
        var service = new QuoteRequestService(repository.Object, new User(), new Resolver(), settings.Object, members.Object, jobs.Object, options);

        Assert.Equal([residential.Id], (await service.ListAsync()).Select(item => item.Id));
        Assert.Null(await service.GetAsync(commercial.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateAsync(commercial.Id, new QuoteRequestDto { Id = commercial.Id, TenantId = TenantId }));

        linked = [new Job { PartitionKey = EntityKeyPolicy.TenantPartition(TenantId), QuoteRequestId = residential.Id, LocksmithJobType = "residential", AssignedTechnicianMembershipId = Guid.NewGuid() }];
        Assert.Empty(await service.ListAsync());
        Assert.Null(await service.GetAsync(residential.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateAsync(residential.Id, new QuoteRequestDto { Id = residential.Id, TenantId = TenantId }));
    }

    private static QuoteRequest Source(string type)
    {
        var id = Guid.NewGuid();
        return new QuoteRequest { Id = id, TenantId = TenantId, PartitionKey = EntityKeyPolicy.TenantPartition(TenantId), RowKey = RepositoryKeyHelper.ToRowKey(id), PropertyType = type, ContactName = "Customer", SubmittedAtUtc = DateTime.UtcNow };
    }
    private static (QuoteRequestService, Mock<IQuoteRequestRepository>) Fixture()
    {
        var repository = new Mock<IQuoteRequestRepository>();
        QuoteRequest? persisted = null;
        repository.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => persisted);
        repository.Setup(value => value.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((QuoteRequest request, CancellationToken _) => persisted = request);
        var settings = new Mock<ITenantSettingsRepository>();
        settings.Setup(value => value.GetAsync(It.IsAny<string>(), "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false)).ReturnsAsync(LocksmithQuotePricingTests.Settings());
        var members = new Mock<ITenantMembershipRepository>();
        members.Setup(value => value.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(TenantId), UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new TenantMembership { Id = LocksmithQuotePricingTests.MembershipId, TenantId = TenantId, UserId = UserId, MembershipStatus = "Active", Role = "staff" });
        return (new QuoteRequestService(repository.Object, new User(), new Resolver(), settings.Object, members.Object), repository);
    }
    private static CreateQuoteRequestDto Input() => new() { Id = Guid.NewGuid(), ContactName = "Test", Email = "test@example.com", SiteName = "Front", ServiceAddress = "100 Main", ServiceType = "Rekeying", PropertyType = "residential", RequestedTimeline = "Next week", Priority = "standard", Need = "Rekey front lock" };
    private sealed class Resolver : IQuoteRequestTenantResolver { public QuoteRequestTenantDefinition Resolve(string slug) => new() { TenantId = slug == "carlzipf" ? TenantId : Guid.NewGuid(), DefaultAssignedTo = "Office", DefaultNextAction = "Review" }; }
    private sealed class User : TurnKeyOps.Lib.Utils.IUserContext { public bool IsAuthenticated => true; public Guid TenantId => FieldQuoteRequestTests.TenantId; public Guid UserId => FieldQuoteRequestTests.UserId; public AppTimeZone Timezone => AppTimeZone.Utc; public string FirstName => "Test"; public string LastName => "Tech"; }
}
