# Local Estimates walkthrough

Seeded on October 6, 2026 at the user's explicit request, in the existing **Carl Zipf local tenant** only. Open `http://127.0.0.1:5189/carlzipf/admin/estimates` while signed in to that tenant. The corresponding six opportunities are under Leads.

| Demo | What to try |
| --- | --- |
| 01 — Confirm site measurements | Confirm opening count and labor hours, then calculate. Lead notes are carried forward but do not count as verified measurements. |
| 02 — Ready to send | Preview the scope, base pricing and optional closer upgrade; issue a customer review link. |
| 03 — Discount needs approval | Review the 15% discount exception against the demo 10% authority, then approve it as an owner. |
| 04 — Customer chooses repair or replacement | Open the issued revision; choose one of the two alternatives. The required-base total is zero because the price depends on that choice. |
| 05 — Signed scope linked to Job | Review simulated signature evidence, accepted base plus closer upgrade, Won Lead and linked Job. |
| 06 — Revision after customer feedback | Review revision 1 history and the customer feedback, then edit/reprice draft revision 2. |

Five **DEMO ONLY** catalog items provide illustrative service, labor, lockset, closer and replacement-opening rates. An explicit illustrative 8% tax, 10% discount authority and 20% deposit were added to the previously empty local estimate configuration. These are walkthrough values, not business pricing advice. Demo contacts use `example.invalid`, and no email/SMS delivery endpoint was called. Delivery origin was not enabled.

`../scripts/seed-estimates-demo.py` is an explicit opt-in CLI, never an application startup seed. Supply an existing authorized local API bearer using `TKO_DEMO_ACCESS_TOKEN`, confirm that the local API uses local Azurite, and run:

```sh
python3 scripts/seed-estimates-demo.py --tenant-slug carlzipf
python3 scripts/seed-estimates-demo.py --tenant-slug carlzipf --apply
```

The first command previews the plan. The script rejects non-loopback API URLs and tenant mismatches. Stable IDs preserve already-created estimate scenarios on reruns; unrelated records/catalog items are retained. No access grants or persistent credentials are created. The seed was run against verified emulator storage; it must never be used against a local API configured for production storage.

The walkthrough uncovered IBeam 2.0.32 envelope reads losing the physical Azure ETag. Leads and QuoteEstimates now use a compatible envelope adapter that reads payload/metadata together, returns the write-response ETag, and rejects stale/wildcard updates. The table and JSON formats are unchanged. Two real-Azurite storage tests verify the behavior separately from the ordinary unit suite (the older profile integration test changes a global key-format setting).

Manifest metadata for Leads and Estimates is public, static GET/HEAD data; app pages and APIs remain authenticated. Manifest requests no longer enter login failure/session cleanup, and manifest links explicitly request credentials when available. Regression coverage checks all six tenant/module combinations, icon responses, absence of cookie clearing, and protected-page rejection.

## Residential/commercial sample refresh

At the user's request, the local Leads walkthrough now has **10 opportunities: 5 residential and 5 commercial**. The six linked Estimate scenarios have more natural Lead names (Maple Ridge, Willow Court, Cedar Grove Dental, Briarwood Offices, Oakview Business Centre and Juniper Lane). Four additional Leads cover move-in rekeying, patio-lock qualification, retail exit-hardware discovery and warehouse-office estimating readiness.

Titles retain a `Sample` suffix. Names/businesses/addresses are fictional, email uses `example.invalid`, and phones use the reserved 555-01xx range. Each Lead includes relevant scope, qualification, source, next action, approximate value and follow-up timing. Existing issued/signed proposal snapshots remain their original walkthrough records. No communications were sent.

The checked-in dataset is `scripts/fixtures/leads-demo.json`; `scripts/refresh-lead-demo.py` updates only explicitly tagged local demo Leads through the API and preserves linked records/lifecycle history. It uses the same authorized local bearer environment variable as the original seed. Run without `--apply` to preview.

## Associate walkthrough

Run `scripts/seed-associates-demo.py --apply` with the same local `TKO_DEMO_ACCESS_TOKEN` to add six fictional employee profiles and assign only the ten tagged sample Leads. It is restricted to the local Carl Zipf tenant and reuses profiles on repeat runs. It creates no memberships, invitations, or outbound communications. Profiles are marked by their Sample company/team and reserved `example.invalid` addresses.

Residential associates: Jordan Ellis, Avery Patel, Riley Torres. Commercial associates: Morgan Chen, Casey Brooks, Cameron Reed. Each has a consistent colored initials avatar in People & Access and on assigned Lead cards. Open a Lead and use Assigned associate → Save assignment to reassign it or choose Unassigned.

Assignments use `ownerProfileId` for employee business profiles independently of sign-in access. Existing membership assignments and automatic membership rules remain supported. Both use Leads write authorization, tenant eligibility checks, optimistic concurrency, and assignment history. Archived employee assignments remain visible as unavailable until reassigned; editing unrelated Lead fields retains them. New job handoffs retain the assigned profile in the accepted-estimate context.
