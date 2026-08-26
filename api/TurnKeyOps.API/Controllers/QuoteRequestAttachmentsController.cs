using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
[Route("api/quote-requests/{quoteRequestId:guid}/attachments")]
public sealed class QuoteRequestAttachmentsController : ApiControllerBase
{
    private const long MaxRequestBytes = 50 * 1024 * 1024;
    private readonly IQuoteRequestAttachmentService _service;

    public QuoteRequestAttachmentsController(IQuoteRequestAttachmentService service) => _service = service;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Upload(
        Guid quoteRequestId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        var uploads = QuoteRequestAttachmentHttpMapper.Map(files);
        try
        {
            var result = await _service.UploadAsync(quoteRequestId, uploads, ct);
            return result is null ? NotFound() : OkResponse(result);
        }
        finally
        {
            QuoteRequestAttachmentHttpMapper.Dispose(uploads);
        }
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> Download(
        Guid quoteRequestId,
        Guid attachmentId,
        CancellationToken ct)
    {
        var result = await _service.DownloadAsync(quoteRequestId, attachmentId, ct);
        return result is null
            ? NotFound()
            : File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid quoteRequestId,
        Guid attachmentId,
        CancellationToken ct) =>
        await _service.DeleteAsync(quoteRequestId, attachmentId, ct) ? NoContentResponse() : NotFound();
}

internal static class QuoteRequestAttachmentHttpMapper
{
    public static IReadOnlyCollection<QuoteRequestAttachmentUpload> Map(IEnumerable<IFormFile> files) =>
        files.Select(file => new QuoteRequestAttachmentUpload(
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream())).ToArray();

    public static void Dispose(IEnumerable<QuoteRequestAttachmentUpload> uploads)
    {
        foreach (var upload in uploads) upload.Content.Dispose();
    }
}
