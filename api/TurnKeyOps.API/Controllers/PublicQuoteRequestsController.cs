using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[AllowAnonymous]
[EnableRateLimiting("public-quote-intake")]
[Route("api/public/quote-requests")]
public sealed class PublicQuoteRequestsController : ApiControllerBase
{
    private const long MaxAttachmentRequestBytes = 50 * 1024 * 1024;
    private readonly IQuoteRequestService _service;
    private readonly IQuoteRequestAttachmentService _attachmentService;

    public PublicQuoteRequestsController(
        IQuoteRequestService service,
        IQuoteRequestAttachmentService attachmentService)
    {
        _service = service;
        _attachmentService = attachmentService;
    }

    [HttpPost("{tenantSlug}")]
    public async Task<IActionResult> Create(
        string tenantSlug,
        [FromBody] CreateQuoteRequestDto dto,
        CancellationToken ct)
    {
        var result = await _service.CreatePublicAsync(tenantSlug, dto, ct);
        return CreatedResponse(nameof(Create), new { tenantSlug }, result);
    }

    [HttpPost("{tenantSlug}/{quoteRequestId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAttachmentRequestBytes)]
    public async Task<IActionResult> UploadAttachments(
        string tenantSlug,
        Guid quoteRequestId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        var uploads = QuoteRequestAttachmentHttpMapper.Map(files);
        try
        {
            var result = await _attachmentService.UploadPublicAsync(tenantSlug, quoteRequestId, uploads, ct);
            return result is null ? NotFound() : OkResponse(result);
        }
        finally
        {
            QuoteRequestAttachmentHttpMapper.Dispose(uploads);
        }
    }
}
