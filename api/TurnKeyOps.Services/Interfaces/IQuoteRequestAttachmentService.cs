using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public sealed record QuoteRequestAttachmentUpload(
    string FileName,
    string ContentType,
    long Length,
    Stream Content);

public sealed record QuoteRequestAttachmentDownload(
    string FileName,
    string ContentType,
    long Length,
    Stream Content);

public interface IQuoteRequestAttachmentService
{
    Task<IReadOnlyCollection<QuoteRequestAttachmentDto>?> UploadPublicAsync(
        string tenantSlug,
        Guid quoteRequestId,
        IReadOnlyCollection<QuoteRequestAttachmentUpload> uploads,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<QuoteRequestAttachmentDto>?> UploadAsync(
        Guid quoteRequestId,
        IReadOnlyCollection<QuoteRequestAttachmentUpload> uploads,
        CancellationToken ct = default);

    Task<QuoteRequestAttachmentDownload?> DownloadAsync(
        Guid quoteRequestId,
        Guid attachmentId,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid quoteRequestId,
        Guid attachmentId,
        CancellationToken ct = default);
}
