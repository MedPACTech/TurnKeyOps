using MedInsights.AzureServices.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public sealed class QuoteRequestAttachmentService : IQuoteRequestAttachmentService
{
    internal const string ContainerName = "quote-request-attachments";
    internal const long MaxFileBytes = 10 * 1024 * 1024;
    internal const int MaxFilesPerUpload = 10;
    internal const int MaxFilesPerRequest = 25;
    internal const long MaxUploadBytes = 50 * 1024 * 1024;

    private readonly IQuoteRequestRepository _repository;
    private readonly IAzureBlobStorageService _blobStorage;
    private readonly IUserContext _userContext;
    private readonly IQuoteRequestTenantResolver _tenantResolver;

    public QuoteRequestAttachmentService(
        IQuoteRequestRepository repository,
        IAzureBlobStorageService blobStorage,
        IUserContext userContext,
        IQuoteRequestTenantResolver tenantResolver)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _userContext = userContext;
        _tenantResolver = tenantResolver;
    }

    public Task<IReadOnlyCollection<QuoteRequestAttachmentDto>?> UploadPublicAsync(
        string tenantSlug,
        Guid quoteRequestId,
        IReadOnlyCollection<QuoteRequestAttachmentUpload> uploads,
        CancellationToken ct = default)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        return UploadForTenantAsync(tenant.TenantId, quoteRequestId, uploads, "Customer", ct);
    }

    public Task<IReadOnlyCollection<QuoteRequestAttachmentDto>?> UploadAsync(
        Guid quoteRequestId,
        IReadOnlyCollection<QuoteRequestAttachmentUpload> uploads,
        CancellationToken ct = default) =>
        UploadForTenantAsync(_userContext.TenantId, quoteRequestId, uploads, Actor(), ct);

    public async Task<QuoteRequestAttachmentDownload?> DownloadAsync(
        Guid quoteRequestId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        ValidateIds(quoteRequestId, attachmentId);
        var tenantId = _userContext.TenantId;
        var entity = await GetRequestAsync(tenantId, quoteRequestId, ct);
        if (entity is null) return null;

        var attachment = QuoteRequestMapper.ToDto(entity).Attachments
            .SingleOrDefault(item => item.Id == attachmentId);
        if (attachment is null || !IsExpectedBlob(attachment, tenantId, quoteRequestId)) return null;

        var content = await _blobStorage.OpenReadAsync(ContainerName, attachment.BlobName!, ct);
        return new QuoteRequestAttachmentDownload(
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            content);
    }

    public async Task<bool> DeleteAsync(
        Guid quoteRequestId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        ValidateIds(quoteRequestId, attachmentId);
        var tenantId = _userContext.TenantId;
        var entity = await GetRequestAsync(tenantId, quoteRequestId, ct);
        if (entity is null) return false;

        var request = QuoteRequestMapper.ToDto(entity);
        var attachment = request.Attachments.SingleOrDefault(item => item.Id == attachmentId);
        if (attachment is null || !IsExpectedBlob(attachment, tenantId, quoteRequestId)) return false;

        request.Attachments = request.Attachments.Where(item => item.Id != attachmentId).ToList();
        request.UpdatedAtUtc = DateTime.UtcNow;
        request.Timeline.Add(NewTimelineEvent(Actor(), $"Attachment removed · {attachment.FileName}"));
        await SaveRequestAsync(entity, request, ct);
        await _blobStorage.DeleteIfExistsAsync(ContainerName, attachment.BlobName!, ct);
        return true;
    }

    private async Task<IReadOnlyCollection<QuoteRequestAttachmentDto>?> UploadForTenantAsync(
        Guid tenantId,
        Guid quoteRequestId,
        IReadOnlyCollection<QuoteRequestAttachmentUpload> uploads,
        string actor,
        CancellationToken ct)
    {
        if (quoteRequestId == Guid.Empty)
            throw new ArgumentException("A quote request id is required.", nameof(quoteRequestId));
        if (uploads.Count == 0)
            throw new ArgumentException("At least one attachment is required.", nameof(uploads));
        if (uploads.Count > MaxFilesPerUpload)
            throw new ArgumentException($"No more than {MaxFilesPerUpload} files may be uploaded at once.", nameof(uploads));
        if (uploads.Sum(item => item.Length) > MaxUploadBytes)
            throw new ArgumentException($"An attachment upload cannot exceed {MaxUploadBytes} bytes.", nameof(uploads));

        var entity = await GetRequestAsync(tenantId, quoteRequestId, ct);
        if (entity is null) return null;
        var request = QuoteRequestMapper.ToDto(entity);
        if (request.Attachments.Count + uploads.Count > MaxFilesPerRequest)
            throw new ArgumentException($"A quote request cannot contain more than {MaxFilesPerRequest} files.", nameof(uploads));

        var prepared = new List<PreparedUpload>(uploads.Count);
        var uploaded = new List<PreparedUpload>(uploads.Count);
        try
        {
            foreach (var upload in uploads)
                prepared.Add(await PrepareAsync(upload, tenantId, quoteRequestId, ct));

            foreach (var item in prepared)
            {
                item.Content.Position = 0;
                await _blobStorage.UploadAsync(
                    ContainerName,
                    item.Attachment.BlobName!,
                    item.Content,
                    item.Attachment.ContentType,
                    new Dictionary<string, string>
                    {
                        ["tenantId"] = tenantId.ToString("N"),
                        ["quoteRequestId"] = quoteRequestId.ToString("N"),
                        ["attachmentId"] = item.Attachment.Id.ToString("N")
                    },
                    ct);
                uploaded.Add(item);
            }

            request.Attachments.AddRange(prepared.Select(item => item.Attachment));
            request.UpdatedAtUtc = DateTime.UtcNow;
            request.Timeline.Add(NewTimelineEvent(
                actor,
                prepared.Count == 1 ? "Attachment uploaded" : $"{prepared.Count} attachments uploaded"));
            await SaveRequestAsync(entity, request, ct);
            return prepared.Select(item => item.Attachment).ToArray();
        }
        catch
        {
            foreach (var item in uploaded)
            {
                try
                {
                    await _blobStorage.DeleteIfExistsAsync(ContainerName, item.Attachment.BlobName!, CancellationToken.None);
                }
                catch
                {
                    // Preserve the original failure. Orphan cleanup is safe to retry from storage inventory.
                }
            }
            throw;
        }
        finally
        {
            foreach (var item in prepared) await item.Content.DisposeAsync();
        }
    }

    private async Task<PreparedUpload> PrepareAsync(
        QuoteRequestAttachmentUpload upload,
        Guid tenantId,
        Guid quoteRequestId,
        CancellationToken ct)
    {
        var fileName = Path.GetFileName(upload.FileName?.Trim());
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 || fileName.Any(char.IsControl))
            throw new ArgumentException("Attachment file name is invalid.", nameof(upload.FileName));
        if (upload.Length <= 0 || upload.Length > MaxFileBytes)
            throw new ArgumentException($"Each attachment must be between 1 byte and {MaxFileBytes} bytes.", nameof(upload.Length));

        var content = await ReadBoundedAsync(upload.Content, MaxFileBytes, ct);
        var detected = DetectContent(content.GetBuffer().AsSpan(0, checked((int)content.Length)));
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (detected is null || !detected.Extensions.Contains(extension))
        {
            await content.DisposeAsync();
            throw new ArgumentException(
                "Attachments must be JPG, PNG, WebP, HEIC/HEIF, or PDF files whose contents match the extension.",
                nameof(upload.ContentType));
        }

        var attachmentId = Guid.NewGuid();
        return new PreparedUpload(new QuoteRequestAttachmentDto
        {
            Id = attachmentId,
            FileName = fileName,
            ContentType = detected.ContentType,
            SizeBytes = content.Length,
            UploadedAtUtc = DateTime.UtcNow,
            TenantId = tenantId,
            BlobContainer = ContainerName,
            BlobName = BuildBlobName(tenantId, quoteRequestId, attachmentId),
            BlobUrl = null
        }, content);
    }

    private static async Task<MemoryStream> ReadBoundedAsync(Stream source, long maxBytes, CancellationToken ct)
    {
        var result = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (result.Length + read > maxBytes)
            {
                await result.DisposeAsync();
                throw new ArgumentException($"Attachment content exceeds the {maxBytes}-byte limit.", nameof(source));
            }
            await result.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        result.Position = 0;
        return result;
    }

    private async Task<QuoteRequest?> GetRequestAsync(Guid tenantId, Guid quoteRequestId, CancellationToken ct) =>
        await _repository.GetAsync(PartitionKey(tenantId), RowKey(quoteRequestId), ct) is { IsDeleted: false } entity
            ? entity
            : null;

    private async Task SaveRequestAsync(QuoteRequest existing, QuoteRequestDto request, CancellationToken ct)
    {
        var updated = QuoteRequestMapper.ToEntity(request);
        updated.DateCreated = existing.DateCreated;
        updated.ETag = existing.ETag;
        await _repository.SaveAsync(updated, ct);
    }

    private static DetectedContent? DetectContent(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8))
            return new("application/pdf", [".pdf"]);
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return new("image/jpeg", [".jpg", ".jpeg"]);
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return new("image/png", [".png"]);
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return new("image/webp", [".webp"]);
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(bytes.Slice(8, 4));
            if (brand is "heic" or "heix" or "hevc" or "hevx" or "mif1" or "msf1")
                return new("image/heic", [".heic", ".heif"]);
        }
        return null;
    }

    private bool IsExpectedBlob(
        QuoteRequestAttachmentDto attachment,
        Guid tenantId,
        Guid quoteRequestId) =>
        attachment.TenantId == tenantId &&
        attachment.BlobContainer == ContainerName &&
        attachment.BlobName == BuildBlobName(tenantId, quoteRequestId, attachment.Id);

    private string Actor()
    {
        var name = $"{_userContext.FirstName} {_userContext.LastName}".Trim();
        return name.Length == 0 ? "Tenant Admin" : name;
    }

    private static QuoteRequestTimelineEventDto NewTimelineEvent(string actor, string label) => new()
    {
        Id = Guid.NewGuid(),
        OccurredAtUtc = DateTime.UtcNow,
        Type = "operator-updated",
        Actor = actor,
        Label = label
    };

    private static void ValidateIds(Guid quoteRequestId, Guid attachmentId)
    {
        if (quoteRequestId == Guid.Empty) throw new ArgumentException("A quote request id is required.", nameof(quoteRequestId));
        if (attachmentId == Guid.Empty) throw new ArgumentException("An attachment id is required.", nameof(attachmentId));
    }

    private static string BuildBlobName(Guid tenantId, Guid quoteRequestId, Guid attachmentId) =>
        $"{tenantId:N}/{quoteRequestId:N}/{attachmentId:N}";
    private static string PartitionKey(Guid tenantId) => RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
    private static string RowKey(Guid id) => RepositoryKeyHelper.ToRowKey(id);

    private sealed record DetectedContent(string ContentType, HashSet<string> Extensions)
    {
        public DetectedContent(string contentType, IEnumerable<string> extensions)
            : this(contentType, new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase))
        {
        }
    }

    private sealed record PreparedUpload(QuoteRequestAttachmentDto Attachment, MemoryStream Content);
}
