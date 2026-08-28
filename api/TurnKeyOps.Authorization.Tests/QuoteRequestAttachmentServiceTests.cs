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

public sealed class QuoteRequestAttachmentServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RequestId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task UploadRequiresExistingRequestInResolvedTenant()
    {
        var repository = new Mock<IQuoteRequestRepository>();
        var storage = new Mock<IAzureBlobStorageService>();
        var service = CreateService(repository.Object, storage.Object);

        var result = await service.UploadPublicAsync("bdr", RequestId, [PngUpload("site.png")]);

        Assert.Null(result);
        repository.Verify(x => x.GetAsync(Partition(TenantId), Row(RequestId), It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(x => x.UploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadRejectsExtensionThatDoesNotMatchFileSignature()
    {
        var repository = RepositoryWithRequest(Entity());
        var storage = new Mock<IAzureBlobStorageService>();
        var service = CreateService(repository.Object, storage.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadPublicAsync("bdr", RequestId, [PngUpload("site.pdf")]));

        storage.Verify(x => x.UploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadStoresPrivateTenantPathAndPersistsServerMetadata()
    {
        var repository = RepositoryWithRequest(Entity());
        QuoteRequest? saved = null;
        repository.Setup(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()))
            .Callback((QuoteRequest entity, CancellationToken _) => saved = entity)
            .ReturnsAsync((QuoteRequest entity, CancellationToken _) => entity);
        var storage = new Mock<IAzureBlobStorageService>();
        storage.Setup(x => x.UploadAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            "image/png",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = CreateService(repository.Object, storage.Object);

        var result = await service.UploadPublicAsync("bdr", RequestId, [PngUpload("site.png")]);

        var attachment = Assert.Single(Assert.IsAssignableFrom<IReadOnlyCollection<QuoteRequestAttachmentDto>>(result));
        Assert.Equal(TenantId, attachment.TenantId);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Null(attachment.BlobUrl);
        Assert.Equal($"{TenantId:N}/{RequestId:N}/{attachment.Id:N}", attachment.BlobName);
        Assert.NotNull(saved);
        var persisted = QuoteRequestMapper.ToDto(saved!);
        Assert.Equal(attachment.Id, Assert.Single(persisted.Attachments).Id);
        Assert.Contains(persisted.Timeline, item => item.Label == "Attachment uploaded" && item.Actor == "Customer");
        storage.Verify(x => x.UploadAsync(
            QuoteRequestAttachmentService.ContainerName,
            attachment.BlobName!,
            It.IsAny<Stream>(),
            "image/png",
            It.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata["tenantId"] == TenantId.ToString("N") &&
                metadata["quoteRequestId"] == RequestId.ToString("N") &&
                metadata["attachmentId"] == attachment.Id.ToString("N")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepeatedUploadOfSameFileIsIdempotent()
    {
        var current = Entity();
        var repository = RepositoryWithRequest(current);
        repository.Setup(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()))
            .Callback((QuoteRequest saved, CancellationToken _) =>
            {
                current = saved;
                repository.Setup(x => x.GetAsync(Partition(TenantId), Row(RequestId), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => current);
            })
            .ReturnsAsync((QuoteRequest saved, CancellationToken _) => saved);
        var storage = new Mock<IAzureBlobStorageService>();
        storage.Setup(x => x.UploadAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            "image/png",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = CreateService(repository.Object, storage.Object);

        var first = await service.UploadPublicAsync("bdr", RequestId, [PngUpload("site.png")]);
        var second = await service.UploadPublicAsync("bdr", RequestId, [PngUpload("site.png")]);

        Assert.Equal(Assert.Single(first!).Id, Assert.Single(second!).Id);
        storage.Verify(x => x.UploadAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            "image/png",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadRollsBackEarlierBlobsWhenLaterUploadFails()
    {
        var repository = RepositoryWithRequest(Entity());
        var storage = new Mock<IAzureBlobStorageService>();
        var sequence = storage.SetupSequence(x => x.UploadAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            "image/png",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()));
        sequence.Returns(Task.CompletedTask);
        sequence.ThrowsAsync(new InvalidOperationException("storage failure"));
        storage.Setup(x => x.DeleteIfExistsAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = CreateService(repository.Object, storage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadPublicAsync(
            "bdr", RequestId, [PngUpload("one.png"), PngUpload("two.png")]));

        storage.Verify(x => x.DeleteIfExistsAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadRollsBackBlobWhenMetadataSaveFails()
    {
        var repository = RepositoryWithRequest(Entity());
        repository.Setup(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("metadata failure"));
        var storage = new Mock<IAzureBlobStorageService>();
        storage.Setup(x => x.UploadAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            "image/png",
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        storage.Setup(x => x.DeleteIfExistsAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = CreateService(repository.Object, storage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadPublicAsync("bdr", RequestId, [PngUpload("site.png")]));

        storage.Verify(x => x.DeleteIfExistsAsync(
            QuoteRequestAttachmentService.ContainerName,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadUsesAuthenticatedTenantAndExpectedBlobIdentity()
    {
        var attachmentId = Guid.NewGuid();
        var blobName = $"{TenantId:N}/{RequestId:N}/{attachmentId:N}";
        var entity = Entity([
            new QuoteRequestAttachmentDto
            {
                Id = attachmentId,
                TenantId = TenantId,
                FileName = "site.png",
                ContentType = "image/png",
                SizeBytes = PngBytes.Length,
                UploadedAtUtc = DateTime.UtcNow,
                BlobContainer = QuoteRequestAttachmentService.ContainerName,
                BlobName = blobName
            }
        ]);
        var repository = RepositoryWithRequest(entity);
        var storage = new Mock<IAzureBlobStorageService>();
        storage.Setup(x => x.OpenReadAsync(
            QuoteRequestAttachmentService.ContainerName,
            blobName,
            It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream(PngBytes));
        var service = CreateService(repository.Object, storage.Object);

        var result = await service.DownloadAsync(RequestId, attachmentId);

        Assert.NotNull(result);
        Assert.Equal("site.png", result!.FileName);
        Assert.Equal("image/png", result.ContentType);
        repository.Verify(x => x.GetAsync(Partition(TenantId), Row(RequestId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRemovesMetadataBeforeDeletingBlob()
    {
        var attachmentId = Guid.NewGuid();
        var blobName = $"{TenantId:N}/{RequestId:N}/{attachmentId:N}";
        var entity = Entity([
            new QuoteRequestAttachmentDto
            {
                Id = attachmentId,
                TenantId = TenantId,
                FileName = "site.png",
                ContentType = "image/png",
                SizeBytes = PngBytes.Length,
                UploadedAtUtc = DateTime.UtcNow,
                BlobContainer = QuoteRequestAttachmentService.ContainerName,
                BlobName = blobName
            }
        ]);
        var repository = RepositoryWithRequest(entity);
        var order = new List<string>();
        repository.Setup(x => x.SaveAsync(It.IsAny<QuoteRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("metadata"))
            .ReturnsAsync((QuoteRequest saved, CancellationToken _) => saved);
        var storage = new Mock<IAzureBlobStorageService>();
        storage.Setup(x => x.DeleteIfExistsAsync(
            QuoteRequestAttachmentService.ContainerName,
            blobName,
            It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("blob"))
            .Returns(Task.CompletedTask);
        var service = CreateService(repository.Object, storage.Object);

        var result = await service.DeleteAsync(RequestId, attachmentId);

        Assert.True(result);
        Assert.Equal(["metadata", "blob"], order);
        repository.Verify(x => x.SaveAsync(
            It.Is<QuoteRequest>(saved => !QuoteRequestMapper.ToDto(saved).Attachments.Any()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static QuoteRequestAttachmentService CreateService(
        IQuoteRequestRepository repository,
        IAzureBlobStorageService storage) =>
        new(repository, storage, new TestUserContext(), new StubTenantResolver());

    private static Mock<IQuoteRequestRepository> RepositoryWithRequest(QuoteRequest entity)
    {
        var repository = new Mock<IQuoteRequestRepository>();
        repository.Setup(x => x.GetAsync(Partition(TenantId), Row(RequestId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        return repository;
    }

    private static QuoteRequest Entity(List<QuoteRequestAttachmentDto>? attachments = null)
    {
        var now = DateTime.UtcNow.AddMinutes(-5);
        return QuoteRequestMapper.ToEntity(new QuoteRequestDto
        {
            Id = RequestId,
            TenantId = TenantId,
            SubmittedAtUtc = now,
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
            Status = "new",
            AssignedTo = "Office intake",
            NextAction = "Review",
            IntakeSummary = "Concrete",
            Attachments = attachments ?? [],
            SubmittedPayload = new QuoteRequestSubmittedPayloadDto(),
            Timeline = [],
            UpdatedAtUtc = now
        });
    }

    private static QuoteRequestAttachmentUpload PngUpload(string fileName) =>
        new(fileName, "image/png", PngBytes.Length, new MemoryStream(PngBytes));

    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
        0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52
    ];

    private static string Partition(Guid tenantId) =>
        TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private static string Row(Guid id) => TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(id);

    private sealed class StubTenantResolver : IQuoteRequestTenantResolver
    {
        public QuoteRequestTenantDefinition Resolve(string tenantSlug) => new() { TenantId = TenantId };
    }

    private sealed class TestUserContext : TurnKeyOps.Lib.Utils.IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid TenantId => QuoteRequestAttachmentServiceTests.TenantId;
        public Guid UserId => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public AppTimeZone Timezone => AppTimeZone.Utc;
        public string FirstName => "Test";
        public string LastName => "User";
    }
}
