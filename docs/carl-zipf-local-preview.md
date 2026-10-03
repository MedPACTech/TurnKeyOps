# Carl Zipf local preview

The Carl Zipf surfaces run in the shared TurnKeyOps client and API. In the current workspace, the client uses `http://127.0.0.1:5189` and the API uses `http://127.0.0.1:5188`. The routes are `/carlzipf/public`, `/carlzipf/tech`, and `/carlzipf/admin` (including `/estimates` and `/invoices`).

Use Azurite-backed local storage. For a Development or Local API preview, set `TurnKeyOps__DisableOutboundCommunications=true`. This prevents Azure Communications email/SMS provider registration and service-bus workers even if developer secrets contain provider connections. The switch is ignored outside Development and Local environments. Keep the API bound to loopback for a local review. The client and API processes must both remain running.

The public form records an office-confirmed appointment preference, not a guaranteed slot. Issuing a quote creates a review link, not an email or text. Activating an invoice changes its workflow status, not delivery or payment state. The field completion signature requires a linked job and an eligible technician. Sample catalog items are not authoritative quote prices until an admin configures live catalog and tax policy.

Local verification completed on 2026-10-02: the .NET authorization suite passed 165 tests; Svelte check and production build passed; focused client tests passed. A full persisted issued-quote/signature round trip still needs an explicitly approved local sample fixture. Concurrent quote decisions and calendar bookings do not yet have storage-level atomic conflict protection.
