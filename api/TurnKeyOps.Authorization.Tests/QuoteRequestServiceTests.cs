using MedInsights.Lib.Utils;
using Moq;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace MedInsights.Authorization.Tests;

public sealed class QuoteRequestServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task PublicCreateResolvesTenantAndAuthorsWorkflowOnServer()
    {
        var repository = RepositoryThatSaves();
        var service = CreateService(repository.Object);

        var result = await service.CreatePublicAsync("bdr", ValidCreate());

        Assert.Equal(TenantId, result.TenantId);
        Assert.Equal("new", result.Status);
        Assert.Equal("public-site", result.Source);
        Assert.Equal("Office intake", result.AssignedTo);
        Assert.Single(result.Timeline);
        Assert.Equal("submitted", result.Timeline[0].Type);
        Assert.Equal("Customer", result.Timeline[0].Actor);
        repository.Verify(x => x.SaveAsync(
            It.Is<QuoteRequest>(entity =>
                entity.PartitionKey == TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId) &&
                entity.TenantId == TenantId &&
                entity.Status == "new"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublicCreateIsIdempotentForAnExistingTenantRequestId()
    {
        var id = Guid.NewGuid();
        var existing = Entity(id, TenantId, "new");
        existing.CompanyName = "Acme Construction";
        existing.ContactName = "Avery Customer";
        existing.ServiceAddress = "100 Main St, Columbus, OH";
        existing.ServiceType = "Concrete";
        existing.Need = "Replace loading pad";
        var repository = new Mock<IQuoteRequestRepository>();
        repository.Setup(x => x.GetAsync(Partition(TenantId), Row(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var service = CreateService(repository.Object);
        var input = ValidCreate();
        input.Id = id;

        var result = await service.CreatePublicAsync("bdr", input);

        Assert.Equal(id, result.Id);
        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublicCreateRejectsReusedRequestIdWithDifferentPayload()
    {
        var id = Guid.NewGuid();
        var existing = Entity(id, TenantId, "new");
        var repository = new Mock<IQuoteRequestRepository>();
        repository.Setup(x => x.GetAsync(Partition(TenantId), Row(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var service = CreateService(repository.Object);
        var input = ValidCreate();
        input.Id = id;
        input.CompanyName = "Different company";

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePublicAsync("bdr", input));

        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublicCreateRejectsMissingContactChannelBeforePersistence()
    {
        var repository = new Mock<IQuoteRequestRepository>();
        var service = CreateService(repository.Object);
        var input = ValidCreate();
        input.Email = string.Empty;
        input.Phone = string.Empty;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePublicAsync("bdr", input));

        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublicCreateRejectsFilledBotTrapBeforePersistence()
    {
        var repository = new Mock<IQuoteRequestRepository>();
        var service = CreateService(repository.Object);
        var input = ValidCreate();
        input.Website = "https://spam.invalid";

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePublicAsync("bdr", input));

        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublicCreateRejectsCallerSuppliedAttachmentMetadata()
    {
        var repository = new Mock<IQuoteRequestRepository>();
        var service = CreateService(repository.Object);
        var input = ValidCreate();
        input.Attachments.Add(new QuoteRequestAttachmentDto
        {
            Id = Guid.NewGuid(),
            FileName = "untrusted.pdf",
            BlobContainer = "caller-container",
            BlobName = "caller-blob"
        });

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePublicAsync("bdr", input));

        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUsesCurrentTenantPartitionInsteadOfGlobalIdLookup()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IQuoteRequestRepository>();
        repository.Setup(x => x.GetAsync(Partition(TenantId), Row(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Entity(id, TenantId, "new"));
        var service = CreateService(repository.Object);

        var result = await service.GetAsync(id);

        Assert.NotNull(result);
        Assert.Equal(TenantId, result!.TenantId);
        repository.Verify(x => x.GetAsync(Partition(TenantId), Row(id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRejectsCrossTenantBodyBeforePersistence()
    {
        var repository = new Mock<IQuoteRequestRepository>();
        var service = CreateService(repository.Object);
        var id = Guid.NewGuid();
        var update = QuoteRequestMapper.ToDto(Entity(id, OtherTenantId, "new"));
        update.AssignedTo = "Office intake";
        update.NextAction = "Review";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(id, update));

        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRejectsInvalidStatusTransitionBeforePersistence()
    {
        var id = Guid.NewGuid();
        var existing = Entity(id, TenantId, "new");
        var repository = new Mock<IQuoteRequestRepository>();
        repository.Setup(x => x.GetAsync(Partition(TenantId), Row(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var service = CreateService(repository.Object);
        var update = QuoteRequestMapper.ToDto(existing);
        update.Status = "won";
        update.AssignedTo = "Office intake";
        update.NextAction = "Review";

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(id, update));

        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAuthorsStatusTimelineAndReviewerOnServer()
    {
        var id = Guid.NewGuid();
        var existing = Entity(id, TenantId, "new");
        var repository = RepositoryThatSaves();
        repository.Setup(x => x.GetAsync(Partition(TenantId), Row(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var service = CreateService(repository.Object);
        var update = QuoteRequestMapper.ToDto(existing);
        update.Status = "in-review";
        update.AssignedTo = "Estimator queue";
        update.NextAction = "Call customer";
        update.Timeline =
        [
            new QuoteRequestTimelineEventDto { Id = Guid.NewGuid(), Actor = "Spoofed", Label = "Spoofed" }
        ];

        var result = await service.UpdateAsync(id, update);

        Assert.NotNull(result);
        Assert.Equal("in-review", result!.Status);
        Assert.Equal("Test User", result.Qualification.ReviewedBy);
        Assert.DoesNotContain(result.Timeline, item => item.Actor == "Spoofed");
        Assert.Contains(result.Timeline, item => item.Label.Contains("Status changed") && item.Actor == "Test User");
        Assert.Contains(result.Timeline, item => item.Label.Contains("Owner reassigned") && item.Actor == "Test User");
    }

    private static QuoteRequestService CreateService(IQuoteRequestRepository repository) =>
        new(repository, new TestUserContext(), new StubTenantResolver(new QuoteRequestTenantDefinition
        {
            TenantId = TenantId,
            DefaultAssignedTo = "Office intake",
            DefaultNextAction = "Review submission"
        }));

    private static Mock<IQuoteRequestRepository> RepositoryThatSaves()
    {
        var repository = new Mock<IQuoteRequestRepository>();
        repository.Setup(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuoteRequest entity, CancellationToken _) => entity);
        return repository;
    }

    private static CreateQuoteRequestDto ValidCreate() => new()
    {
        CompanyName = "Acme Construction",
        ContactName = "Avery Customer",
        Email = "avery@example.com",
        Phone = "555-0100",
        SiteName = "North lot",
        ServiceAddress = "100 Main St, Columbus, OH",
        ServiceType = "Concrete",
        PropertyType = "Commercial",
        RequestedTimeline = "Within 30 days",
        Priority = "standard",
        Need = "Replace loading pad"
    };

    private static QuoteRequest Entity(Guid id, Guid tenantId, string status)
    {
        var submitted = DateTime.UtcNow.AddMinutes(-5);
        return QuoteRequestMapper.ToEntity(new QuoteRequestDto
        {
            Id = id,
            TenantId = tenantId,
            SubmittedAtUtc = submitted,
            CompanyName = "Acme",
            ContactName = "Avery",
            Email = "avery@example.com",
            Phone = "555-0100",
            SiteName = "North lot",
            ServiceAddress = "100 Main St",
            ServiceType = "Concrete",
            PropertyType = "Commercial",
            RequestedTimeline = "30 days",
            Priority = "standard",
            Need = "Replace pad",
            Source = "public-site",
            Status = status,
            AssignedTo = "Office intake",
            NextAction = "Review",
            IntakeSummary = "Concrete",
            SubmittedPayload = new QuoteRequestSubmittedPayloadDto(),
            Timeline =
            [
                new QuoteRequestTimelineEventDto
                {
                    Id = Guid.NewGuid(),
                    OccurredAtUtc = submitted,
                    Type = "submitted",
                    Actor = "Customer",
                    Label = "Quote request submitted"
                }
            ],
            UpdatedAtUtc = submitted
        });
    }

    private static string Partition(Guid tenantId) =>
        TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private static string Row(Guid id) => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(id);

    private sealed class TestUserContext : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => QuoteRequestServiceTests.TenantId;
        public Guid UserId => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }

    private sealed class StubTenantResolver(QuoteRequestTenantDefinition tenant) : IQuoteRequestTenantResolver
    {
        public QuoteRequestTenantDefinition Resolve(string tenantSlug) => tenant;
    }
}
