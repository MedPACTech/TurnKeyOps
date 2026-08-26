using System.Globalization;
using System.Text.Json;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class InvoiceWebhookService : IInvoiceWebhookService
{
    private readonly IPaymentProviderResolver _providers;
    private readonly IInvoiceService _invoices;

    public InvoiceWebhookService(IPaymentProviderResolver providers, IInvoiceService invoices)
    {
        _providers = providers;
        _invoices = invoices;
    }

    public async Task<InvoiceProviderWebhookResultDto> ReceiveAsync(
        string provider,
        string json,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Webhook payload is required.", nameof(json));
        var paymentProvider = _providers.GetRequiredProvider(provider);
        var verified = await paymentProvider.ParseWebhookAsync(json, headers, ct);
        if (string.IsNullOrWhiteSpace(verified.EventId))
            throw new ArgumentException("Verified webhook is missing a provider event identifier.");
        var tenantId = verified.TenantId
            ?? throw new ArgumentException("Verified webhook is missing tenant_id metadata.");
        using var document = JsonDocument.Parse(verified.PayloadJson);
        var invoiceIdValue = ReadProviderMetadata(document.RootElement, verified.Provider, "invoice_id");
        if (!Guid.TryParse(invoiceIdValue, out var invoiceId) || invoiceId == Guid.Empty)
            throw new ArgumentException("Verified webhook is missing a valid invoice_id metadata value.");

        var eventType = verified.EventType.Trim();
        var kind = eventType.Contains("refund", StringComparison.OrdinalIgnoreCase) ||
                   eventType.Contains("reversed", StringComparison.OrdinalIgnoreCase)
            ? "refund"
            : "payment";
        var status = eventType.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                     eventType.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                     eventType.Contains("cancel", StringComparison.OrdinalIgnoreCase)
            ? "failed"
            : eventType.Contains("pending", StringComparison.OrdinalIgnoreCase)
                ? "pending"
                : "succeeded";
        var amount = ReadAmount(document.RootElement, verified.Provider, kind);
        if (amount <= 0m) throw new ArgumentException("Verified webhook does not contain a positive payment amount.");
        var idempotencyKey = $"{verified.Provider.Trim().ToLowerInvariant()}:{verified.EventId}";
        var result = await _invoices.ReconcileProviderEventAsync(tenantId, invoiceId, new InvoicePaymentInputDto
        {
            Kind = kind,
            Amount = amount,
            Method = verified.Provider,
            Provider = verified.Provider,
            ExternalReference = ReadExternalReference(document.RootElement, verified.Provider),
            IdempotencyKey = idempotencyKey,
            Status = status
        }, ct);

        return new InvoiceProviderWebhookResultDto
        {
            Provider = verified.Provider,
            EventId = verified.EventId,
            Invoice = result
        };
    }

    private static string? ReadProviderMetadata(JsonElement root, string provider, string key)
    {
        if (provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase) &&
            TryGet(root, out var metadata, "data", "object", "metadata") &&
            metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty(key, out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        if (provider.Equals("PayPal", StringComparison.OrdinalIgnoreCase) &&
            TryGet(root, out var customId, "resource", "custom_id") && customId.ValueKind == JsonValueKind.String)
        {
            foreach (var segment in (customId.GetString() ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var split = segment.IndexOf('=');
                if (split > 0 && segment[..split].Equals(key, StringComparison.OrdinalIgnoreCase)) return segment[(split + 1)..];
            }
        }
        return null;
    }

    private static decimal ReadAmount(JsonElement root, string provider, string kind)
    {
        if (provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
        {
            var fields = kind == "refund"
                ? new[] { "amount", "amount_refunded" }
                : new[] { "amount_received", "amount_paid", "amount" };
            foreach (var field in fields)
            {
                if (TryGet(root, out var amount, "data", "object", field) && amount.TryGetInt64(out var cents))
                    return Math.Round(cents / 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        if (provider.Equals("PayPal", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGet(root, out var value, "resource", "amount", "value") &&
                decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) return amount;
            if (TryGet(root, out value, "resource", "seller_receivable_breakdown", "gross_amount", "value") &&
                decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) return amount;
        }
        return 0m;
    }

    private static string? ReadExternalReference(JsonElement root, string provider)
    {
        var path = provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase)
            ? new[] { "data", "object", "id" }
            : new[] { "resource", "id" };
        return TryGet(root, out var value, path) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool TryGet(JsonElement root, out JsonElement value, params string[] path)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value)) return false;
        }
        return true;
    }
}
