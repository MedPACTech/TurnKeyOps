using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[ApiController]
[Route("api/invoices/webhooks")]
[AllowAnonymous]
public sealed class InvoicePaymentWebhookController : ApiControllerBase
{
    private readonly IInvoiceWebhookService _service;

    public InvoicePaymentWebhookController(IInvoiceWebhookService service)
    {
        _service = service;
    }

    [HttpPost("stripe")]
    public Task<IActionResult> ReceiveStripe(CancellationToken ct) => Receive("Stripe", ct);

    [HttpPost("paypal")]
    public Task<IActionResult> ReceivePayPal(CancellationToken ct) => Receive("PayPal", ct);

    private async Task<IActionResult> Receive(string provider, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var headers = Request.Headers.ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var result = await _service.ReceiveAsync(provider, json, headers, ct);
        return OkResponse(result);
    }
}
