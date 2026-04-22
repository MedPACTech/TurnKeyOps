using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [ApiController]
    [Route("billing/webhooks")]
    [AllowAnonymous]
    public class BillingWebhookController : ApiControllerBase
    {
        private readonly IPaymentWebhookService _paymentWebhookService;

        public BillingWebhookController(IPaymentWebhookService paymentWebhookService) : base()
        {
            _paymentWebhookService = paymentWebhookService;
        }

        [HttpPost("stripe")]
        public async Task<IActionResult> ReceiveStripe(CancellationToken ct)
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync(ct);
            var signatureHeader = Request.Headers["Stripe-Signature"].ToString();

            var result = await _paymentWebhookService.ReceiveStripeWebhookAsync(json, signatureHeader, ct);
            return OkResponse(result);
        }

        [HttpPost("paypal")]
        public async Task<IActionResult> ReceivePayPal(CancellationToken ct)
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync(ct);
            var headers = Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            var result = await _paymentWebhookService.ReceiveWebhookAsync("PayPal", json, headers, ct);
            return OkResponse(result);
        }
    }
}
