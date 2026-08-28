# Production identity, communications, and billing

This runbook is the configuration contract for TKO-0011. Secret values belong in Azure Key Vault and App Service must consume them through Key Vault references. Never paste secret values into Hubbsly, source control, pipeline logs, or deployment evidence.

## Required production settings

Identity is issued and validated by iBeam Identity. Configure the exact deployed values for:

- `IBeam__Identity__Jwt__Issuer`
- `IBeam__Identity__Jwt__Audience`
- `IBeam__Identity__Jwt__SigningKey`
- `IBeam__Identity__AzureTable__StorageConnectionString`
- `IBeam__Identity__Otp__HashSalt`
- `IBeam__Identity__Otp__VerificationTokenSecret`

Azure Communication Services email and SMS are registered behind `IEmailService` and `ISmsService`. Configure the provider connection strings and defaults, plus an explicit BDR and Think Pink profile under `ProductionIntegrations__Communications__Tenants__{brand}`. Tenant communication resolution is keyed by the service-owned tenant GUID and fails closed for an unknown tenant; callers must never choose another tenant's sender.

Billing is opt-in. Set `BillingIntegrations__EnabledProviders__{index}` and `BillingIntegrations__DefaultProvider`. Only `Stripe` and `PayPal` are accepted. Startup fails in Production when the default is disabled, required credentials/webhook verification settings are absent, a catalog contains placeholders, PayPal points at sandbox, tenant communication profiles are incomplete, or `ProductionIntegrations__SecretsSource` is not `KeyVault`.

Stripe requires `StripeSettings__SecretKey`, `StripeSettings__WebhookSecret`, and production catalog mappings. PayPal requires `PayPalSettings__ClientId`, `PayPalSettings__ClientSecret`, `PayPalSettings__WebhookId`, `PayPalSettings__BaseUrl=https://api-m.paypal.com`, and production plan mappings.

## Reliability and replay safety

Checkout and credit top-up requests require a stable `Idempotency-Key` header (or the equivalent DTO property). Stripe receives it as the SDK request idempotency key and PayPal receives it as `PayPal-Request-Id`. Provider HTTP retries are bounded and apply only to safe or explicitly idempotent requests. Timeouts, retry count, and circuit-breaker thresholds are controlled by `BillingIntegrations` settings. Provider response bodies are not copied into raised HTTP errors.

Webhook verification happens before tenant correlation or business processing. Durable webhook receipts remain keyed by provider and event id, so replay returns the prior result instead of applying the event twice.

## Rotation, disable, and rollback

1. Add the replacement secret as a new Key Vault version.
2. Confirm App Service reports a resolved Key Vault reference without displaying its value.
3. Restart the staging slot and run identity, communication, and webhook failure-mode checks.
4. Swap or deploy only after staging succeeds; retain the previous secret version through the rollback window.
5. To disable a billing provider, remove it from `EnabledProviders` only after another enabled provider is the default and no tenant account still references the provider.
6. Roll back by restoring the previous App Service artifact and Key Vault secret version; never replace a production reference with an inline secret.

## Evidence checklist

- Both brand hosts authenticate against the exact issuer/audience and reject wrong issuer, wrong audience, expired, and wrong-role tokens.
- BDR and Think Pink test sends use their own configured profiles and recipients; an unknown/cross-tenant profile fails closed.
- Stripe or PayPal webhook signatures reject tampered payloads and duplicate event ids remain idempotent.
- Repeated checkout requests with one idempotency key return one provider operation.
- Timeout, 429, 5xx, open-circuit, and disabled-provider paths return safe errors without secrets or provider payloads.
- App Service uses HTTPS-only, managed identity, and resolved Key Vault references; secret values are never included in evidence.

Provider dashboard ownership, live catalog/webhook identifiers, approved recipients, and payment/refund smoke-test authorization must be recorded in Hubbsly before production smoke tests run.
