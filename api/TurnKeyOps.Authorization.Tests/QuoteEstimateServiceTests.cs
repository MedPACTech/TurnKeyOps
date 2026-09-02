using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using MedInsights.AzureServices.Interfaces;
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

public sealed class QuoteEstimateServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RequestId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task SaveDraftRequiresParentInCurrentTenant()
    {
        var fixture = CreateFixture(parent: null);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SaveDraftAsync(RequestId, Input()));

        fixture.Estimates.Verify(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Storage.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveDraftCalculatesTotalsOnServerAndUsesTenantBlobPath()
    {
        var fixture = CreateFixture(Quote());
        QuoteEstimateDto? written = null;
        fixture.Storage.Setup(x => x.UploadAsync(
                QuoteEstimateService.ContainerName, It.IsAny<string>(), It.IsAny<Stream>(), "application/json",
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, Stream stream, string _, IReadOnlyDictionary<string, string> _, CancellationToken _) =>
                written = JsonSerializer.Deserialize<QuoteEstimateDto>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .Returns(Task.CompletedTask);

        var result = await fixture.Service.SaveDraftAsync(RequestId, Input());

        Assert.NotNull(written);
        Assert.Equal(100, written!.Totals.SquareFeet);
        Assert.Equal(1.4, written.Totals.CubicYards);
        Assert.Equal(written.Totals.EstimatedTotal, result.Totals.EstimatedTotal);
        fixture.Storage.Verify(x => x.UploadAsync(
            QuoteEstimateService.ContainerName,
            It.Is<string>(name => name.StartsWith($"{TenantId:N}/{RequestId:N}/v1/", StringComparison.Ordinal)),
            It.IsAny<Stream>(), "application/json",
            It.Is<IReadOnlyDictionary<string, string>>(metadata => metadata["tenantId"] == TenantId.ToString("N")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveDraftDeletesNewBlobWhenMetadataSaveFails()
    {
        var fixture = CreateFixture(Quote());
        fixture.Estimates.Setup(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("table failure"));
        fixture.Storage.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SaveDraftAsync(RequestId, Input()));

        fixture.Storage.Verify(x => x.DeleteIfExistsAsync(
            QuoteEstimateService.ContainerName, It.IsAny<string>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CreateRevisionRequiresCurrentVersionAndRetainsImmutableSnapshot()
    {
        var entity = Entity("v1");
        var fixture = CreateFixture(Quote("estimate-sent"), entity, Packet("sent"));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateRevisionAsync(RequestId, "stale"));
        var revised = await fixture.Service.CreateRevisionAsync(RequestId, entity.ETag.ToString());

        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal("draft", revised.Status);
        var history = Assert.Single(revised.RevisionHistory);
        Assert.Equal(1, history.RevisionNumber);
        Assert.Equal("sent", history.Status);
    }

    [Fact]
    public async Task PublicAccessRequiresValidUnexpiredTokenAndApprovedDecisionIsIdempotent()
    {
        const string token = "customer-capability";
        var entity = Entity("v1");
        entity.CustomerAccessTokenHash = Hash(token);
        entity.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1);
        entity.DeliveryStatus = "approved";
        var fixture = CreateFixture(Quote(), entity, Packet("sent", "approved"));

        Assert.Null(await fixture.Service.GetPublicAsync("bdr", RequestId, "wrong"));
        var first = await fixture.Service.ApproveAsync("bdr", RequestId, new QuoteEstimateDecisionDto { AccessToken = token });
        var second = await fixture.Service.ApproveAsync("bdr", RequestId, new QuoteEstimateDecisionDto { AccessToken = token });

        Assert.Equal("approved", first!.Delivery!.Status);
        Assert.Equal("approved", second!.Delivery!.Status);
        fixture.Estimates.Verify(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveWithValidTokenPersistsDecisionAndWinsTenantQuote()
    {
        const string token = "customer-capability";
        var entity = Entity("v1");
        entity.CustomerAccessTokenHash = Hash(token);
        entity.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1);
        var fixture = CreateFixture(Quote("estimate-sent"), entity, Packet("sent"));

        var result = await fixture.Service.ApproveAsync("bdr", RequestId, new QuoteEstimateDecisionDto { AccessToken = token });

        Assert.Equal("approved", result!.Delivery!.Status);
        fixture.Quotes.Verify(x => x.SaveAsync(
            It.Is<QuoteRequest>(quote => QuoteRequestMapper.ToDto(quote).Status == "won"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Fixture CreateFixture(QuoteRequest? parent, QuoteEstimate? entity = null, QuoteEstimateDto? packet = null)
    {
        var estimates = new Mock<IQuoteEstimateRepository>();
        estimates.Setup(x => x.GetAsync(Partition(), Row(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        estimates.Setup(x => x.ListAsync(Partition(), It.IsAny<CancellationToken>())).ReturnsAsync(entity is null ? [] : [entity]);
        estimates.Setup(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuoteEstimate saved, CancellationToken _) => { saved.ETag = new ETag("v2"); return saved; });
        var quotes = new Mock<IQuoteRequestRepository>();
        quotes.Setup(x => x.GetAsync(Partition(), Row(), It.IsAny<CancellationToken>())).ReturnsAsync(parent);
        quotes.Setup(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuoteRequest saved, CancellationToken _) => saved);
        var storage = new Mock<IAzureBlobStorageService>();
        storage.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        storage.Setup(x => x.DeleteIfExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        if (packet is not null)
        {
            storage.Setup(x => x.OpenReadAsync(QuoteEstimateService.ContainerName, entity!.PayloadBlobName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(packet, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        }
        var defaults = new Mock<IEstimateDefaultsService>();
        defaults.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new EstimateDefaultsDto
        {
            ConcreteCostPerYard = 100, RebarCostPerFoot = 1, LaborRatePerHour = 50,
            PourHoursPer100SqFt = 4, FinishHoursPer100SqFt = 4
        });
        var service = new QuoteEstimateService(estimates.Object, quotes.Object, storage.Object, defaults.Object, new Resolver(), new User());
        return new(service, estimates, quotes, storage);
    }

    private static QuoteEstimateDraftInputDto Input() => new()
    {
        CustomerName = "Avery", SiteName = "North lot", ServiceSummary = "Concrete", Status = "draft",
        Locations = [new() { Id = "pad", Name = "Pad", LengthFeet = 10, WidthFeet = 10, DepthInches = 4, WastePercent = 10, NumberOfPours = 1 }]
    };
    private static QuoteEstimateDto Packet(string status, string delivery = "sent") => new()
    {
        Id = RequestId, QuoteRequestId = RequestId, RevisionNumber = 1, CustomerName = "Avery", SiteName = "North lot",
        Status = status, SavedAtUtc = DateTime.UtcNow, Locations = Input().Locations, Totals = new(),
        Delivery = new() { Status = delivery, ReviewUrl = "/review?token=x", SentAtUtc = DateTime.UtcNow }
    };
    private static QuoteEstimate Entity(string etag) => new()
    {
        Id = RequestId, QuoteRequestId = RequestId, PartitionKey = Partition(), RowKey = Row(), ETag = new ETag(etag),
        PayloadBlobName = $"{TenantId:N}/{RequestId:N}/v1/payload.json", RevisionNumber = 1, Status = "sent"
    };
    private static QuoteRequest Quote(string status = "qualified") => QuoteRequestMapper.ToEntity(new QuoteRequestDto
    {
        Id = RequestId, TenantId = TenantId, SubmittedAtUtc = DateTime.UtcNow.AddDays(-1), CompanyName = "Acme",
        ContactName = "Avery", Email = "avery@example.com", Phone = "555-0100", SiteName = "North lot",
        ServiceAddress = "100 Main", ServiceType = "Concrete", PropertyType = "Commercial", RequestedTimeline = "30 days",
        Priority = "standard", Need = "Pad", Status = status, AssignedTo = "Office", NextAction = "Estimate",
        SubmittedPayload = new(), Timeline = [], UpdatedAtUtc = DateTime.UtcNow
    });
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static string Partition() => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(TenantId);
    private static string Row() => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(RequestId);
    private sealed record Fixture(
        QuoteEstimateService Service,
        Mock<IQuoteEstimateRepository> Estimates,
        Mock<IQuoteRequestRepository> Quotes,
        Mock<IAzureBlobStorageService> Storage);
    private sealed class Resolver : IQuoteRequestTenantResolver { public QuoteRequestTenantDefinition Resolve(string tenantSlug) => new() { TenantId = TenantId }; }
    private sealed class User : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true; public Guid TenantId => QuoteEstimateServiceTests.TenantId;
        public Guid UserId => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"); public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test"; public string LastName => "User";
    }
}
