# Bob intelligence and automation V1

## Delivery and scope

This implementation adds a permission-scoped operating intelligence surface to the existing Bob action platform. The V1 core is implemented and locally verified. The larger specification also describes follow-on capabilities; the limits below are explicit, and the Hubbsly parent remains in flight for that work. Nothing has been deployed or enabled against production.

Modules remain authoritative. There are no Bob-owned Lead, Job, Finance or Payroll copies. Bob stores policies, source references, attention conditions, execution claims, structured rules, user feedback and workflow checkpoints. Existing module providers perform business operations through their existing service contracts and business validation.

## Architecture and capability registry

`BobCapabilityRegistry` discovers the registered `IBobActionProvider` implementations. Each capability exposes its ID, module, read/write grants, module dependencies, risk, required inputs, draft semantics, deterministic provider and rationale. Inputs are bounded and source IDs/version requirements are validated. Provider services retain final business-schema validation.

Available providers cover conversation read/archive; Lead summary, assignment, stage, task, estimate handoff, draft/send and scheduling; Estimate summary, structuring, pricing, revisions, issue and reminders; Job summary, readiness, structured notes, recommendations, scheduling, tasks, changes, requirements, activity, transitions and notification; Supply availability, shortages, sources, purchasing, reservations, requests, draft orders and dispatch; Finance AR/AP, cash, health, close, construction/WIP/retainage/backlog, operational exceptions and journal drafts; Payroll readiness and reviewed batch preparation. Registry availability follows the actual DI provider set.

`BobActionGuard` is registered at the existing action execution boundary. It checks authenticated tenant/actor, explicit Bob read/write grants, target-module grants, dependent modules, role permission and current tenant/module policy. Checks run on proposal, approval, replay and execution. Payroll must also be enabled by its own authoritative configuration. Legacy broad Bob routes retain their aggregate permission gate; `BobIntelligenceController` is a separate narrow route whose service filters every module. This permits Finance-only, Payroll-only, Jobs-only, Leads-only and mixed installations without opening legacy broad context to restricted users.

The new tenant Bob module allow-list controls Bob discovery. Existing platform modules lack one universal entitlement catalog; Payroll's own enablement is additionally enforced. Central Bob configuration does not grant access to a module or override a legacy disabled/recommend/draft/read-only action policy. An explicit legacy approval policy remains a checkpoint. This is a conservative migration ceiling.

## Context, attention and next action

`IBobContextSource` is the module contract. `BobModuleContextSource` obtains deliberate projections through authoritative services. Finance and Supply expose focused intelligence methods that do not require unrelated module workspaces. Payroll uses its existing employee/crew/approval-scoped readiness projection, without compensation.

Context records tenant, actor, actual membership role, enabled authorized modules, trade, observation time, source ID/version, recorded condition, explanation and next step. Module/entity focus is checked before retrieval. No whole-tenant context is sent to a model. Per-module result windows are bounded at 60; source retrieval can still enumerate a module's existing workspace/list contract internally. Query-level pagination improvements are a follow-on for large tenants.

Initial deterministic conditions include overdue Lead response/follow-up and missing qualification; pricing blockers and sent proposals awaiting response; Job readiness blockers; upcoming Calendar appointments; inventory shortages; overdue purchase orders; unsettled overdue AR/AP and close blockers; Payroll time exceptions. Unknown or unavailable data is never replaced with invented values. Module errors and incomplete windows are reported explicitly.

`BobAttentionEngine` uses a stable actor/module/entity/condition identity. A repeated condition updates the active item. Dismissal persists until resolution and recurrence. A complete authorized check resolves missing conditions; failed, disabled, unauthorized or truncated checks cannot resolve them. Tenant escalation rules use condition, elapsed hours, team label and severity. Escalation is visible in the briefing; it does not send an external message or assign an unauthorized user.

Role ranking distinguishes executive/default, sales/estimator, operations/project manager, dispatch/scheduler, foreman/field, finance/bookkeeper and payroll. Membership role is authoritative; optional policy role-module preferences can narrow retrieval but never expand permission. Installations whose RBAC roles are only owner/admin/staff need functional-role configuration before fine-grained role tuning.

Next-action results include source, rationale, eligibility, blocking inputs, risk, policy and whether immediate execution is possible. Missing input or source state produces a review requirement. Deterministic module blockers take precedence over inference. A proposed review capability is not a promise that a business mutation is eligible.

## Briefing and command experience

All three tenants have `/admin/intelligence`, with Today, Activity and owner-only Automation views. Today shows priorities, decisions, risk, handled workflows and watches, with source links, dismissal and lightweight feedback. The approved Ask Bob conversation layout is retained and gains a compact daily priority section when intelligence is enabled. A restricted user can open focused Today without the broad legacy conversation permissions.

Cmd/Ctrl+K in the admin shell opens the focused command surface. Command resolution uses bounded deterministic module/intent routing, authorized context and explicit entity focus. Multi-record action requests require clarification. Language never constructs a database mutation or grants a tool. “Focus this record” narrows context; unsupported business creation, general multi-entity natural-language extraction and fuzzy entity-name resolution still require module UI or a reviewed structured request.

Operational read questions from the existing general Ask Bob conversation use the same intelligence command endpoint. Existing voice transcription submits the same question form and therefore uses this path for those questions. Existing estimate interviews and field draft guidance remain in their established paths. No additional audio transport, speech model, or voice-only execution path was introduced.

The UI uses the existing shell/theme variables, text severity labels, 44px controls, visible focus, semantic forms and responsive layouts. Feedback is collapsed until needed. Approval views show registered step IDs, rationale, current state and supported input fields. Declining a checkpoint stops remaining work and preserves completed module actions.

## Policies, risk and execution safety

Canonical authority modes are DISABLED, RECOMMEND_ONLY, DRAFT, ACT_WITH_APPROVAL and AUTO_ACT_BY_POLICY. Legacy strings are normalized. Read-only authority cannot execute a mutation; draft authority cannot send. Unknown modes fail closed. Read/draft operations are low risk, ordinary internal operations are medium risk, and communication/scheduling/financial outcomes are high risk. Restricted operations such as conversation destruction have explicit human risk. Sensitive financial close, compensation alteration and payment posting capabilities are not registered for autonomous execution. Internal policy fields do not replace proper module entitlements.

High-risk actions retain explicit human approval in V1 even if tenant configuration selects auto. External communication also requires the tenant's external-communication allowance. Existing `BobOperations.Enabled` and `WriteActionsEnabled` kill switches remain authoritative, and remain disabled by default in source configuration.

Each non-read execution claims a stable actor/idempotency key through a conditional durable Bob-state commit before invoking the provider. Competing requests or API instances cannot both claim the same write. Action replay additionally binds the original input. Completed actions return their recorded result. An uncertain write is not automatically retried, including after a crash; source reconciliation and a deliberately reviewed new action are required. This favors duplicate prevention over automatic recovery.

## Workflows and cross-module coordination

Workflows contain explicit capability steps, reviewed inputs, source workspace IDs, concise rationale, durable step/action IDs and current status. Steps are validated before creation; caller-supplied completion state or approvals are ignored. A stable request fingerprint binds workflow replay. A conditional tenant commit claims workflow advancement before side effects. The workflow stops at every human checkpoint; approval of one checkpoint cannot approve future steps. Permissions and policy are rechecked as it advances.

Module actions retain optimistic source versions. A successfully returned authoritative version may update later steps on the same source; Bob does not fetch a fresh unrelated version to bypass a human's concurrent edit. Provider results stay in the existing BobActions store. Workflow inputs are minimized before storage. The Activity endpoint omits raw workflow input.

Attention can prepare an explicit source review; Lead assignment followed by a module task and editable response draft; a Job readiness task; or Job readiness coordination with authorized Inventory/Purchasing reads and an internal Job task. Disabled/unauthorized optional modules are omitted. The generic workflow endpoint supports reviewed steps across the registered provider set, including approval-gated actions. It does not invent valid scheduling windows, stock availability, financial postings or payroll inputs.

A partial failure reports completed step count and stops in `needs_review`; it never implies rollback or full completion. V1 does not advertise a generic undo. Compensation must use a module's proper cancellation/reassignment/reversal flow. Automatic recovery after an uncertain provider result and a reconciliation UI are follow-on work.

## Proactive events, watching and automation

Briefing refresh is the current check cadence. `BobAutomationEngine` converts authoritative condition observations/changes into tenant/actor-scoped structured events. Rule matching is deterministic; event/rule IDs make processing idempotent. Conditions are reconciled through the Attention Engine. Identical refreshes do not flood Activity.

V1 rule configuration is deliberately limited to module + canonical condition + severity/effective time + enabled/priority + `attention` action. Rules cannot contain arbitrary executable code, choose unrestricted actors or enable authority. Rule ownership is recorded and applies in that owner's current access context. A general no-code action builder and autonomous domain-event worker are not implemented.

A user can watch an active source condition. The watch records its real source reference and completes only after a complete successful authorized refresh shows resolution. Disabled/inaccessible/failed/incomplete sources keep it watching. No UI claims continuous monitoring. Background schedules, domain-commit outboxes, real-time invalidation, quiet-hour delivery and external push/email/SMS briefings remain follow-on work.

## Automation Center, feedback, audit and metrics

Owners configure Bob/proactive availability, module discovery, each action's authority, external communication, attention rules and escalation thresholds. Stored role-module preferences are supported by API. Policy updates use exact optimistic versions, preserve unrelated module policy and cannot silently enable Customer Bob. Suggested automation guidance requires explicit review; no inference enables rules.

Activity lists workflow status and concise operational events. Existing action audit now identifies executed provider, target workspace and permission/policy evaluation. New control-state changes use `bob_intelligence` audit entries. No chain-of-thought is stored. Metrics currently count completed, awaiting-approval and failed/review workflows, resolved attention and recorded feedback. Helpful/not-helpful, wrong recommendation, wrong data and should-not-run feedback does not alter policy.

Outcome metrics such as time saved, acceptance rates, false-positive rates, provider latency, long-term retention and behavior-derived suggestions are not yet instrumented. No usefulness claim is inferred from activity volume.

## Customer Bob boundary

Internal intelligence endpoints require tenant staff authorization and explicit Bob grants. They never accept a caller-supplied tenant/actor. Customer Bob continues through `PortalService.ExplainAsync`, which reads the customer's entitlement-scoped public projection; it has no internal context or employee action tools. Internal policy rejects enabling a customer path through this service. Existing customer-visible status, proposal and next-step explanation remains separate and covered by portal isolation tests.

## Persistence, rollout and compatibility

Bob control state uses private `tenant-bob-intelligence` blobs with a conditional `BobIntelligenceVersions` Azure Table pointer. Blob paths are tenant-prefixed; pointer updates require exact ETags. Losing concurrent snapshots are removed; ambiguous commits retain their blob. Execution claims survive process restart. Existing Chats, ChatMessages and BobActions remain the action/conversation records.

Deployments must provision access to the new container/table, or enable the existing create-if-not-exists development behavior. Keep the global write switch off for initial verification. Review module allow-lists, legacy policy ceilings and explicit per-user grants before enabling actions. No production policy or module setting was changed by this work.

Existing module routes, service contracts, pricing, accounting, payroll and customer portal remain authoritative. Existing uncommitted Payroll & Time work was preserved. The legacy aggregate Bob permission gate was intentionally not relaxed. The new focused route is separately mapped and checked.

## Verification

Recorded on 2026-10-14 in the local workspace:

- Full API suite with `TKO_STORAGE_TESTS=1`: **670 passed, zero failed/skipped**.
- Bob-focused tests cover policy ceilings, real module grant checks, external communication, module combinations, disabled/failed/incomplete context, deduplication, recurrence/dismissal, escalation, command ambiguity and bypass attempts, approvals, partial failure, replay binding, caller-state injection, watches, event scope/idempotency and execution claims.
- Storage tests exercise real local Azurite Tables, including conditional pointer races and tenant isolation. The Bob blob transport is mocked; this is not a live cloud deployment test.
- Client session/module/routing suites plus Bob link/access tests: **47 passed**, including the legacy Carl Zipf client suite.
- `npm run check`: zero errors/warnings. `npm run build`: passed.
- Playwright: **14 passed**, desktop and Pixel 7, all three tenant routes, command/dismissal, policy persistence, workflow approval preview and completion, keyboard, responsive overflow, light/dark axe checks. Browser checks use a fixture API; business authorization is covered separately in API tests.
- `git diff --check`: passed.

## Next work

The highest-value next delivery is an authorized actor-aware durable domain outbox/check worker with source reconciliation and recovery UI. Then extend structured automation actions/templates, entity-name resolution and command extraction; add validated scheduling-hold/compensation contracts; finish functional-role controls and outcome metrics; and run deployed multi-tenant provider acceptance with tenant policy and iBeam transport configured. These are tracked as remaining work rather than represented as shipped capabilities.

Hubbsly: parent `f4d387df-16ff-4ad9-ab8f-a6d12a8820ee`; implemented core cards TKO-0147–TKO-0151; remaining work TKO-0152–TKO-0154. Parent stays in flight.
