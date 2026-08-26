using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;

public sealed class InvoiceWebhookServiceTests
{
    [Fact]
    public async Task VerifiedStripeEventIsTenantScopedAndReconciledThroughInvoiceService()
    {
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var invoiceId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var json = JsonSerializer.Serialize(new
        {
            data = new
            {
                @object = new
                {
                    id = "pi_123",
                    amount_received = 2550,
                    metadata = new { tenant_id = tenantId.ToString("D"), invoice_id = invoiceId.ToString("D") }
                }
            }
        });
        var provider = new Mock<IPaymentProvider>();
        provider.SetupGet(item => item.ProviderName).Returns("Stripe");
        provider.Setup(item => item.ParseWebhookAsync(json, It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentWebhookEventDto
            {
                Provider = "Stripe", EventId = "evt_123", EventType = "payment_intent.succeeded",
                TenantId = tenantId, PayloadJson = json
            });
        var resolver = new Mock<IPaymentProviderResolver>();
        resolver.Setup(item => item.GetRequiredProvider("Stripe")).Returns(provider.Object);
        var invoices = new Mock<IInvoiceService>();
        invoices.Setup(item => item.ReconcileProviderEventAsync(
                tenantId, invoiceId, It.IsAny<InvoicePaymentInputDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceDto { Id = invoiceId });
        var service = new InvoiceWebhookService(resolver.Object, invoices.Object);

        var result = await service.ReceiveAsync("Stripe", json, new Dictionary<string, string>
        {
            ["Stripe-Signature"] = "verified-by-provider"
        });

        Assert.Equal("evt_123", result.EventId);
        provider.Verify(item => item.ParseWebhookAsync(json, It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        invoices.Verify(item => item.ReconcileProviderEventAsync(tenantId, invoiceId,
            It.Is<InvoicePaymentInputDto>(input => input.Kind == "payment" && input.Amount == 25.50m &&
                input.IdempotencyKey == "stripe:evt_123" && input.ExternalReference == "pi_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WebhookWithoutVerifiedTenantCorrelationIsRejected()
    {
        const string json = "{\"data\":{\"object\":{\"amount\":100,\"metadata\":{\"invoice_id\":\"dddddddd-dddd-dddd-dddd-dddddddddddd\"}}}}";
        var provider = new Mock<IPaymentProvider>();
        provider.Setup(item => item.ParseWebhookAsync(json, It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentWebhookEventDto
            {
                Provider = "Stripe", EventId = "evt", EventType = "payment_intent.succeeded", PayloadJson = json
            });
        var resolver = new Mock<IPaymentProviderResolver>();
        resolver.Setup(item => item.GetRequiredProvider("Stripe")).Returns(provider.Object);
        var invoices = new Mock<IInvoiceService>();
        var service = new InvoiceWebhookService(resolver.Object, invoices.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReceiveAsync("Stripe", json, new Dictionary<string, string>()));

        invoices.Verify(item => item.ReconcileProviderEventAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<InvoicePaymentInputDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
