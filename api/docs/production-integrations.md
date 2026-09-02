# Production identity, communications, and billing

This runbook is the configuration contract for TKO-0011. For the initial TurnKeyOps release, secret values are stored only in Azure App Service application settings. Key Vault references remain a supported future secret source. Never paste secret values into Hubbsly, source control, pipeline logs, or deployment evidence.

## Required production settings

Identity is issued and validated by iBeam Identity. Configure the exact deployed values for:

- `IBeam__Identity__Jwt__Issuer`
- `IBeam__Identity__Jwt__Audience`
- `IBeam__Identity__Jwt__SigningKey`
- `IBeam__Identity__AzureTable__StorageConnectionString`
- `IBeam__Identity__Otp__HashSalt`
- `IBeam__Identity__Otp__VerificationTokenSecret`

Azure Communication Services email and SMS are registered behind `IEmailService` and `ISmsService`. Set `ProductionIntegrations__Communications__Enabled=true`. The initial release uses the already configured shared TurnKeyOps platform sender, selected with `ProductionIntegrations__Communications__UseSharedPlatformSender=true`. Per-tenant profiles under `ProductionIntegrations__Communications__Tenants__{brand}` remain an optional future mode; when shared mode is false, both profiles are mandatory and tenant resolution fails closed for an unknown tenant.

Billing is opt-in and explicitly disabled for the initial release with `BillingIntegrations__Enabled=false`, an empty `EnabledProviders` array, and an empty `DefaultProvider`. Billing workers and provider clients are not registered in that posture, and runtime provider resolution fails closed. When billing is later approved, set `Enabled=true`, `EnabledProviders__{index}`, and `DefaultProvider`; only `Stripe` and `PayPal` are accepted. Startup fails in Production when enablement is missing, disabled billing retains active provider configuration, the default is not enabled, required credentials/webhook verification settings are absent, a catalog contains placeholders, PayPal points at sandbox, required communication settings are incomplete, or `ProductionIntegrations__SecretsSource` is neither `AppServiceSettings` nor `KeyVault`.

Stripe requires `StripeSettings__SecretKey`, `StripeSettings__WebhookSecret`, and production catalog mappings. PayPal requires `PayPalSettings__ClientId`, `PayPalSettings__ClientSecret`, `PayPalSettings__WebhookId`, `PayPalSettings__BaseUrl=https://api-m.paypal.com`, and production plan mappings.

## Reliability and replay safety

Checkout and credit top-up requests require a stable `Idempotency-Key` header (or the equivalent DTO property). Stripe receives it as the SDK request idempotency key and PayPal receives it as `PayPal-Request-Id`. Provider HTTP retries are bounded and apply only to safe or explicitly idempotent requests. Timeouts, retry count, and circuit-breaker thresholds are controlled by `BillingIntegrations` settings. Provider response bodies are not copied into raised HTTP errors.

Webhook verification happens before tenant correlation or business processing. Durable webhook receipts remain keyed by provider and event id, so replay returns the prior result instead of applying the event twice.

## Rotation, disable, and rollback

1. Add the replacement value to the approved secret source (`AppServiceSettings` for the initial release, or a new Key Vault secret version after Key Vault adoption).
2. Confirm App Service reports the setting or resolved Key Vault reference without displaying its value.
3. Restart the staging slot and run identity, communication, and webhook failure-mode checks.
4. Swap or deploy only after staging succeeds; retain the previous secret version through the rollback window.
5. To disable a billing provider, remove it from `EnabledProviders` only after another enabled provider is the default and no tenant account still references the provider.
6. Roll back by restoring the previous App Service artifact and prior setting/reference; never put a secret in source control or deployment evidence.

## Evidence checklist

- Both brand hosts authenticate against the exact issuer/audience and reject wrong issuer, wrong audience, expired, and wrong-role tokens.
- Shared-sender mode uses the approved TurnKeyOps platform sender for both brands without allowing caller-selected sender configuration. If per-tenant mode is later enabled, BDR and Think Pink test sends use their own profiles and an unknown/cross-tenant profile fails closed.
- Stripe or PayPal webhook signatures reject tampered payloads and duplicate event ids remain idempotent.
- Repeated checkout requests with one idempotency key return one provider operation.
- Timeout, 429, 5xx, open-circuit, and disabled-provider paths return safe errors without secrets or provider payloads.
- Internal Admin can create Customer Admin invites only for configured BDR Construction and Pink Axe tenant keys; tenant admins can manage only users in their JWT tenant.
- Invite redemption requires an exact verified iBeam email or phone match and remains auditable.
- App Service secret-valued settings are present in the approved source and never included in evidence. HTTPS-only, managed identity, and Key Vault adoption are tracked as infrastructure hardening when provisioned.

No customer communication smoke test may run without a designated safe recipient. Billing provider dashboards, live catalog/webhook identifiers, and payment/refund smoke-test authorization must be recorded in Hubbsly before billing is enabled in a later release.
