using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;

[ApiController]
[Route("billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }


    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateSubscriptionCheckoutRequestDto req, CancellationToken ct)
    {
        var session = await _billingService.CreateSubscriptionCheckoutAsync(req, ct);
        return Ok(new { url = session.Url, sessionId = session.SessionId, provider = session.Provider });
    }

    [HttpPost("portal")]
    public async Task<IActionResult> CreatePortal([FromBody] CreateCustomerPortalRequestDto req, CancellationToken ct)
    {
        var portal = await _billingService.CreateCustomerPortalAsync(req, ct);
        return Ok(new { url = portal.Url, provider = portal.Provider });
    }

    [HttpPost("subscriptions/seats")]
    public async Task<IActionResult> UpdateSeats([FromBody] UpdateSubscriptionSeatsRequestDto req, CancellationToken ct)
        => Ok(await _billingService.UpdateSubscriptionSeatsAsync(req, ct));

    [HttpPost("subscriptions/seats/reduce-at-renewal")]
    public async Task<IActionResult> ReduceSeatsAtRenewal([FromBody] ScheduleSeatReductionRequestDto req, CancellationToken ct)
        => Ok(await _billingService.ScheduleSeatReductionAsync(req, ct));

    [HttpPost("topups/checkout")]
    public async Task<IActionResult> PurchaseTopUp([FromBody] PurchaseCreditTopUpRequestDto req, CancellationToken ct)
    {
        var session = await _billingService.PurchaseCreditTopUpAsync(req, ct);
        return Ok(new { url = session.Url, sessionId = session.SessionId, provider = session.Provider });
    }

    [HttpPost("subscriptions/cancel")]
    public async Task<IActionResult> CancelAtTermEnd([FromBody] CancelSubscriptionRequestDto req, CancellationToken ct)
        => Ok(await _billingService.CancelAtTermEndAsync(req, ct));

    [HttpPost("subscriptions/reactivate")]
    public async Task<IActionResult> Reactivate([FromBody] ReactivateSubscriptionRequestDto req, CancellationToken ct)
        => Ok(await _billingService.ReactivateAsync(req, ct));

    // [HttpPost("webhooks/stripe")]
    // public async Task<IActionResult> HandleWebhook()
    // {
    //     var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
    //     var endpointSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");

    //     Event stripeEvent;
    //     try
    //     {
    //         stripeEvent = EventUtility.ConstructEvent(
    //             json,
    //             Request.Headers["Stripe-Signature"],
    //             endpointSecret
    //         );
    //     }
    //     catch (Exception)
    //     {
    //         return BadRequest();
    //     }

    //     switch (stripeEvent.Type)
    //     {
    //         case Events.CheckoutSessionCompleted:
    //         {
    //             var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
    //             // 1) Get customer & subscription
    //             var subs = new SubscriptionService();
    //             var sub = await subs.GetAsync(session.SubscriptionId);

    //             // 2) Upsert tenant and owner billing records
    //             //    - Store Stripe IDs: CustomerId, SubscriptionId, PriceId, Status
    //             // 3) Send magic-link email to session.CustomerDetails.Email (or Customer.Email)
    //             break;
    //         }

    //         case Events.CustomerSubscriptionUpdated:
    //         case Events.CustomerSubscriptionCreated:
    //         case Events.CustomerSubscriptionDeleted:
    //         {
    //             var sub = stripeEvent.Data.Object as Subscription;
    //             // Update status, plan, current_period_end, cancel_at_period_end, quantity (seats), etc.
    //             break;
    //         }

    //         case Events.InvoicePaymentFailed:
    //             // Mark account past_due, email user
    //             break;

    //         case Events.InvoicePaid:
    //             // Ensure active
    //             break;
    //     }

    //     return Ok();
    // }
}
