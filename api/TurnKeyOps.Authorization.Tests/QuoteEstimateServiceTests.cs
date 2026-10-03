using MedInsights.Repositories.Interfaces;
using MedInsights.Lib.Entities;
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

        var document = (await fixture.Service.GetPublicAsync("bdr", RequestId, token))!;
        var result = await fixture.Service.ApproveAsync("bdr", RequestId, SignedDecision(token, document));

        Assert.Equal("approved", result!.Delivery!.Status);
        Assert.Equal("Avery Customer", result.ApprovalSignature!.SignerPrintedName);
        Assert.Equal(document.DocumentHash, result.ApprovalSignature.DocumentHash);
        Assert.Equal(document.RevisionNumber, result.ApprovalSignature.RevisionNumber);
        Assert.Equal(document.Totals.EstimatedTotal, result.ApprovalSignature.Total);
        Assert.Equal(QuoteApprovalConsent.Text, result.ApprovalSignature.ConsentText);
        Assert.Equal(QuoteApprovalConsent.Version, result.ApprovalSignature.ConsentVersion);
        Assert.Equal(DateTimeKind.Utc, result.ApprovalSignature.SignedAtUtc.Kind);
        fixture.Quotes.Verify(x => x.SaveAsync(
            It.Is<QuoteRequest>(quote => QuoteRequestMapper.ToDto(quote).Status == "won"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LocksmithDraftUsesSharedPacketAndCannotBypassApprovalWithReadyStatus()
    {
        var quote = Quote(); quote.PropertyType = "residential";
        var fixture = CreateFixture(quote, operational: LocksmithQuotePricingTests.Settings(approval: true));
        var input = Input(); input.Locksmith = LocksmithQuotePricingTests.Input(); input.Status = "ready-to-send";
        var result = await fixture.Service.SaveDraftAsync(RequestId, input);
        Assert.Equal("draft", result.Status);
        Assert.Empty(result.Locations);
        Assert.NotNull(result.LocksmithPricing);
        Assert.Equal(286m, result.Totals.EstimatedTotal);
        Assert.Equal("policy-v1", result.LocksmithPricing.PolicyVersion);
    }

    [Fact]
    public async Task LocksmithTenantCannotUseConcreteDraftToBypassPricing()
    {
        var quote = Quote(); quote.PropertyType = "residential";
        var fixture = CreateFixture(quote, operational: LocksmithQuotePricingTests.Settings());
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SaveDraftAsync(RequestId, Input()));
        fixture.Estimates.Verify(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DraftRequiresCurrentTechJobTypeCapability()
    {
        var quote = Quote(); quote.PropertyType = "residential";
        var fixture = CreateFixture(quote, operational: LocksmithQuotePricingTests.Settings(capabilities: ["commercial"]));
        var input = Input(); input.Locksmith = LocksmithQuotePricingTests.Input();
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => fixture.Service.SaveDraftAsync(RequestId, input));
        fixture.Estimates.Verify(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendRejectsMissingOfficeApprovalAndChangedPolicy()
    {
        var quote = Quote("estimate-drafted"); quote.PropertyType = "residential";
        var packet = Packet("ready-to-send");
        packet.LocksmithPricing = LocksmithQuotePricing.Calculate(LocksmithQuotePricingTests.Settings(approval: true), LocksmithQuotePricingTests.Input());
        var fixture = CreateFixture(quote, Entity("v1"), packet, LocksmithQuotePricingTests.Settings());
        var error = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SendAsync(RequestId, "v1", "/carlzipf/estimate/test"));
        Assert.Contains("Office pricing approval", error.Message);
        packet.LocksmithPricing.PolicyVersion = "outdated";
        fixture = CreateFixture(quote, Entity("v1"), packet, LocksmithQuotePricingTests.Settings());
        error = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SendAsync(RequestId, "v1", "/carlzipf/estimate/test"));
        Assert.Contains("Pricing policy changed", error.Message);
    }

    [Fact]
    public async Task OfficeApprovalRequiresAdminAndMarksExactDraftReady()
    {
        var packet = Packet("draft"); packet.Delivery = null;
        packet.LocksmithPricing = LocksmithQuotePricing.Calculate(LocksmithQuotePricingTests.Settings(approval: true), LocksmithQuotePricingTests.Input());
        var fixture = CreateFixture(Quote(), Entity("v1"), packet, LocksmithQuotePricingTests.Settings());
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => fixture.Service.ApproveLocksmithPricingAsync(RequestId, "v1"));
        fixture = CreateFixture(Quote(), Entity("v1"), packet, LocksmithQuotePricingTests.Settings(), officeAdmin: true);
        var result = await fixture.Service.ApproveLocksmithPricingAsync(RequestId, "v1");
        Assert.NotNull(result.LocksmithPricing!.OfficeApprovedAtUtc);
        Assert.Equal("ready-to-send", result.Status);
        Assert.Equal("v2", result.Version);
    }

    [Fact]
    public async Task SharedPacketRequiresRevisionBeforeEditing()
    {
        var fixture = CreateFixture(Quote("estimate-sent"), Entity("v1"), Packet("sent"));
        var input = Input(); input.ExpectedVersion = "v1";
        var error = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SaveDraftAsync(RequestId, input));
        Assert.Contains("new revision", error.Message);
    }

    [Fact]
    public async Task EstimateVersionFallsBackWhenRepositoryOmitsEtag()
    {
        var entity = Entity(""); entity.DateUpdated = DateTime.UtcNow;
        var fixture = CreateFixture(Quote(), entity, Packet("draft"));
        var result = await fixture.Service.GetAsync(RequestId);
        Assert.Equal(entity.DateUpdated.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), result!.Version);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("intent")]
    [InlineData("consent")]
    [InlineData("revision")]
    [InlineData("hash")]
    [InlineData("control")]
    public async Task SignatureRejectsMissingIntentIdentityOrChangedDocument(string invalidField)
    {
        const string token = "customer-capability";
        var entity = Entity("v1"); entity.CustomerAccessTokenHash = Hash(token); entity.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1);
        var fixture = CreateFixture(Quote("estimate-sent"), entity, Packet("sent"));
        var document = (await fixture.Service.GetPublicAsync("carlzipf", RequestId, token))!;
        var decision = SignedDecision(token, document);
        switch (invalidField)
        {
            case "name": decision.SignerPrintedName = " "; break;
            case "intent": decision.IntentToSign = false; break;
            case "consent": decision.ConsentVersion = "outdated"; break;
            case "revision": decision.RevisionNumber++; break;
            case "hash": decision.DocumentHash = new string('a', 64); break;
            case "control": decision.SignerPrintedName = "Name\nInjected"; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ApproveAsync("carlzipf", RequestId, decision));
        fixture.Estimates.Verify(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Storage.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SignatureSurvivesReloadAndDuplicateDecisionDoesNotReplaceEvidence()
    {
        const string token = "customer-capability";
        var entity = Entity("v1"); entity.CustomerAccessTokenHash = Hash(token); entity.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1);
        var packet = Packet("sent"); packet.Totals.EstimatedTotal = 123.45m;
        var fixture = CreateFixture(Quote("estimate-sent"), entity, packet);
        fixture.Estimates.Setup(x => x.GetAsync(Partition(), Row(), It.IsAny<CancellationToken>())).ReturnsAsync(() => entity);
        fixture.Estimates.Setup(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>())).ReturnsAsync((QuoteEstimate saved, CancellationToken _) => { entity = saved; entity.ETag = new ETag("v2"); return entity; });
        fixture.Storage.Setup(x => x.OpenReadAsync(QuoteEstimateService.ContainerName, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(packet, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        fixture.Storage.Setup(x => x.UploadAsync(QuoteEstimateService.ContainerName, It.IsAny<string>(), It.IsAny<Stream>(), "application/json", It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, Stream stream, string _, IReadOnlyDictionary<string, string> _, CancellationToken _) => packet = JsonSerializer.Deserialize<QuoteEstimateDto>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .Returns(Task.CompletedTask);
        var document = (await fixture.Service.GetPublicAsync("carlzipf", RequestId, token))!;
        var decision = SignedDecision(token, document);
        var first = await fixture.Service.ApproveAsync("carlzipf", RequestId, decision);
        decision.SignerPrintedName = "Different person";
        var retry = await fixture.Service.ApproveAsync("carlzipf", RequestId, decision);
        Assert.Equal("Avery Customer", retry!.ApprovalSignature!.SignerPrintedName);
        Assert.Equal(123.45m, retry.ApprovalSignature.Total);
        Assert.Equal(first!.ApprovalSignature!.SignedAtUtc, retry.ApprovalSignature.SignedAtUtc);
        Assert.Equal(first.DocumentHash, retry.DocumentHash);
        fixture.Estimates.Verify(x => x.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await fixture.Service.ApproveAsync("carlzipf", RequestId, new() { AccessToken = "wrong" }));
    }

    [Fact]
    public async Task DocumentHashDetectsChangedPriceAndChangeRequestNeedsNoSignature()
    {
        const string token = "customer-capability";
        var entity = Entity("v1"); entity.CustomerAccessTokenHash = Hash(token); entity.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1);
        var packet = Packet("sent");
        var fixture = CreateFixture(Quote("estimate-sent"), entity, packet);
        var document = (await fixture.Service.GetPublicAsync("bdr", RequestId, token))!;
        packet.DocumentHash = document.DocumentHash;
        packet.Totals.EstimatedTotal = 99m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.GetPublicAsync("bdr", RequestId, token));
        packet.DocumentHash = string.Empty;
        var result = await fixture.Service.RequestChangesAsync("bdr", RequestId, new() { AccessToken = token, ResponseNote = "Please revise the scope." });
        Assert.Equal("changes-requested", result!.Delivery!.Status);
        Assert.Null(result.ApprovalSignature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuoteListDetailAndRevisionDenyOtherCapabilitiesOrAssignedTechnician(bool assignedElsewhere)
    {
        var quote = Quote("estimate-sent"); quote.PropertyType = "residential";
        var settings = LocksmithQuotePricingTests.Settings(capabilities: assignedElsewhere ? ["residential"] : ["commercial"]);
        var fixture = CreateFixture(quote, Entity("v1"), Packet("sent"), settings, assignedMember: assignedElsewhere ? Guid.NewGuid() : null);
        Assert.Empty(await fixture.Service.ListAsync());
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => fixture.Service.GetAsync(RequestId));
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => fixture.Service.CreateRevisionAsync(RequestId, "v1"));
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => fixture.Service.SendAsync(RequestId, "v1", "/carlzipf/estimate/test"));
        fixture.Estimates.Verify(value => value.SaveAsync(It.IsAny<QuoteEstimate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OfficeAdminCanReviewBothQuoteTypesWithoutTechCapability()
    {
        var quote = Quote("estimate-sent"); quote.PropertyType = "commercial";
        var fixture = CreateFixture(quote, Entity("v1"), Packet("sent"), LocksmithQuotePricingTests.Settings(capabilities: []), officeAdmin: true, assignedMember: Guid.NewGuid());
        Assert.Single(await fixture.Service.ListAsync());
        Assert.NotNull(await fixture.Service.GetAsync(RequestId));
        Assert.Equal(2, (await fixture.Service.CreateRevisionAsync(RequestId, "v1")).RevisionNumber);
    }

    private static QuoteEstimateDecisionDto SignedDecision(string token, QuoteEstimateDto document) => new()
    {
        AccessToken = token, SignerPrintedName = "Avery Customer", IntentToSign = true,
        ConsentVersion = document.ApprovalConsentVersion, RevisionNumber = document.RevisionNumber, DocumentHash = document.DocumentHash
    };

    private static Fixture CreateFixture(QuoteRequest? parent, QuoteEstimate? entity = null, QuoteEstimateDto? packet = null, TenantSettingsDocument? operational = null, bool officeAdmin = false, Guid? assignedMember = null)
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
        var settings = new Mock<ITenantSettingsRepository>();
        settings.Setup(x => x.GetAsync(Partition(), "SETTINGS|OPERATIONAL", It.IsAny<CancellationToken>(), false)).ReturnsAsync(operational);
        var members = new Mock<ITenantMembershipRepository>();
        members.Setup(x => x.GetByUserIdAsync(MedInsights.Lib.EntityKeyPolicy.TenantPartition(TenantId), new User().UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership { Id = LocksmithQuotePricingTests.MembershipId, TenantId = TenantId, UserId = new User().UserId, MembershipStatus = "Active", Role = officeAdmin ? "admin" : "staff" });
        var jobs = new Mock<IJobRepository>();
        jobs.Setup(value => value.ListAsync(Partition(), It.IsAny<CancellationToken>())).ReturnsAsync(assignedMember.HasValue
            ? new[] { new Job { Id = Guid.NewGuid(), PartitionKey = Partition(), QuoteRequestId = RequestId, LocksmithJobType = parent?.PropertyType, AssignedTechnicianMembershipId = assignedMember } } : Array.Empty<Job>());
        var service = new QuoteEstimateService(estimates.Object, quotes.Object, storage.Object, defaults.Object, new Resolver(), new User(), settings.Object, members.Object, jobs.Object);
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
