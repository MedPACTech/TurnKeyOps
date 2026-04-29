<script lang="ts">
	const playbooks = [
		{ stage: 'Discovery', owner: 'Platform PM', artifact: 'Vertical fit memo + object map', standard: 'Confirm what belongs to shared product versus vertical-specific workflow before delivery starts.' },
		{ stage: 'Configuration', owner: 'Solutions architect', artifact: 'Tenant setup checklist', standard: 'Roles, workflow defaults, billing rules, and environment controls must be explicit.' },
		{ stage: 'Workflow model', owner: 'Product + engineering', artifact: 'Request → visit → estimate schema map', standard: 'The shared object model must define relationships, ownership, audit posture, and stage-specific editing rules before implementation spreads.' },
		{ stage: 'Migration', owner: 'Implementation lead', artifact: 'Data validation packet', standard: 'Every migrated object needs QA, signoff, and rollback posture.' },
		{ stage: 'Training', owner: 'Enablement', artifact: 'Admin walkthrough + SOP set', standard: 'Operator confidence is a launch gate, not optional polish.' },
		{ stage: 'Go-live', owner: 'Platform ops', artifact: 'Launch readiness board', standard: 'Data, access, billing, and support coverage are green before launch.' }
	];

	const workflowFlow = [
		{ label: 'Quote request', detail: 'Intake record created from public site, office entry, or referral capture.', state: 'Owns submission payload, qualification state, and intake attachments.' },
		{ label: 'Site visit', detail: 'Operational field record linked to one request and one site.', state: 'Owns scheduled window, assigned resource, field notes, measurements, and visit outcome.' },
		{ label: 'Estimate', detail: 'Commercial record seeded from the approved visit context.', state: 'Owns pricing, scope package, revision history, customer-facing packet, and approval state.' }
	];

	const entityRows = [
		{
			entity: 'Quote request',
			requiredFields: 'request id, source, status, submittedAtUtc, contact snapshot, service address, service type, project need, requested timeline, qualification state',
			relationships: 'Belongs to one customer profile and one primary site; can create many activity events and attachments; may create one canonical site visit record plus later re-visits.',
			sourceOfTruth: 'Intake + qualification lane',
			editableByStage: 'Editable during intake and qualification; customer snapshot remains immutable after submission while normalized customer/site records can be corrected by ops.',
			derivedFields: 'lane, missing-info reasons, next action, intake summary, workflow phase'
		},
		{
			entity: 'Site visit',
			requiredFields: 'visit id, request id, site id, scheduled window, assigned field resource, visit status, captured scope summary, measurements/observations, outcome timestamp',
			relationships: 'Belongs to one request and one site; feeds one or more estimate revisions; emits activity events and field attachments.',
			sourceOfTruth: 'Visit scheduling + field execution lane',
			editableByStage: 'Schedule details editable before dispatch; field notes editable during/after visit; locked outcome snapshot seeds estimate creation.',
			derivedFields: 'visit readiness, dispatch label, estimate seed completeness'
		},
		{
			entity: 'Estimate',
			requiredFields: 'estimate id, estimate number, request id, customer id, site id, status, pricing totals, scope package, valid-through date, approval posture',
			relationships: 'Belongs to one customer and one site; references the originating request and latest approved site visit snapshot; can emit invoice/contract follow-on records later.',
			sourceOfTruth: 'Estimate desk',
			editableByStage: 'Draft fields editable before send; approved customer packet stays versioned while internal costing and revision notes remain operator-controlled.',
			derivedFields: 'production readiness, signature status, deposit status, margin flags'
		},
		{
			entity: 'Customer',
			requiredFields: 'customer id, display name, primary contact methods, lifecycle stage, account status',
			relationships: 'Owns many requests, sites, estimates, invoices, and activity events.',
			sourceOfTruth: 'Shared CRM/customer record',
			editableByStage: 'Core identity editable by office/admin users with audit trail; request submission keeps its original contact snapshot even after customer record changes.',
			derivedFields: 'open request count, estimate count, receivables posture'
		},
		{
			entity: 'Site',
			requiredFields: 'site id, service address, site label, property type, access notes, geo/service metadata',
			relationships: 'Belongs to one customer; linked by many requests, visits, estimates, and job records over time.',
			sourceOfTruth: 'Shared site/property record',
			editableByStage: 'Address and access data editable as site facts improve; historical visits and estimates retain their own event snapshots.',
			derivedFields: 'latest visit date, active work count, readiness flags'
		},
		{
			entity: 'Attachment',
			requiredFields: 'attachment id, parent entity type/id, file name, content type, size, uploadedAtUtc, storage pointer',
			relationships: 'Can belong to quote request, site visit, estimate, or activity event; may be reused by reference but never mutated in place.',
			sourceOfTruth: 'Blob/file store plus parent-record metadata',
			editableByStage: 'Upload and association are editable; original file artifact is append-only.',
			derivedFields: 'preview availability, required-attachment completeness'
		},
		{
			entity: 'Activity event',
			requiredFields: 'event id, entity type/id, occurredAtUtc, actor, event type, label, payload snapshot',
			relationships: 'Belongs to any workflow record and provides the shared audit/history layer across request, visit, and estimate transitions.',
			sourceOfTruth: 'Append-only event timeline',
			editableByStage: 'System-generated events are immutable; operator notes append corrections instead of rewriting history.',
			derivedFields: 'timeline groupings, SLA timers, recent-activity badges'
		}
	];

	const stageRules = [
		{
			stage: 'Intake + qualification',
			operatorEditable: 'Request status, assignment, qualification checklist, normalized customer/site corrections, attachment associations',
			derivedOrLocked: 'Original submission payload, initial submitted timestamp, calculated missing-info reasons, workflow lane'
		},
		{
			stage: 'Site visit scheduling',
			operatorEditable: 'Visit window, assigned field resource, site contact, scheduling notes, dispatch context',
			derivedOrLocked: 'Dispatch readiness, current visit status label, linked request history'
		},
		{
			stage: 'Field execution + estimate seeding',
			operatorEditable: 'Visit findings, measurements, follow-up notes, estimate seed summary, attachment uploads',
			derivedOrLocked: 'Visit completed timestamp, event history, seed completeness score'
		},
		{
			stage: 'Estimate drafting + send',
			operatorEditable: 'Scope package, internal costing, customer-facing sections, validity window, revision notes, send status',
			derivedOrLocked: 'Linked request/visit references, approval history, deposit/schedule readiness indicators'
		}
	];

	const estimateSeedInputs = [
		'Site visit scope summary, measurements, photos, and notable field observations seed the first estimate draft.',
		'Customer and site identities are referenced from normalized records so later edits do not corrupt the original request snapshot.',
		'Estimate status remains separate from request status so the intake lane can show conversion history without becoming the pricing source of truth.'
	];

	const openQuestions = [
		'Should the first site visit be embedded on the request record for speed, or split into a dedicated visit table immediately so re-visits and no-show handling stay clean?',
		'Which estimate fields belong in a reusable versioned packet model versus the core estimate row?',
		'Do attachments need first-class document categories (photo, measurement, contract, insurance, invoice backup) before multi-vertical rollout expands?',
		'When a request spans multiple properties or buildings, should one request fan out to multiple site records before estimate drafting starts?'
	];

	const templateBacklog = [
		'Contractor template needs weather-aware scheduling and estimate-to-contract defaults.',
		'White-glove template needs appointment orchestration without crew/job-site assumptions.',
		'Admin shells should share the same structure even when module content changes by vertical.'
	];
</script>

<div class="space-y-4">
	<div class="grid gap-3 xl:grid-cols-[1.04fr_0.96fr]">
		<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Implementation lifecycle</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Stages, artifacts, and exit standards</h3>
			</div>
			<div class="divide-y divide-[var(--shell-border)]">
				{#each playbooks as playbook}
					<div class="grid gap-3 px-4 py-4 lg:grid-cols-[0.7fr_0.9fr_1.35fr]">
						<div>
							<p class="text-base font-semibold text-[var(--text-strong)]">{playbook.stage}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{playbook.owner}</p>
						</div>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm text-[var(--text-base)]">
							{playbook.artifact}
						</div>
						<p class="text-sm leading-6 text-[var(--text-muted)]">{playbook.standard}</p>
					</div>
				{/each}
			</div>
		</section>

		<div class="grid gap-3">
			<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Workflow spine</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Shared object handoff from intake to estimate</h3>
				<div class="mt-4 space-y-3">
					{#each workflowFlow as item, index}
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
							<div class="flex items-center gap-2 text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">
								<span>0{index + 1}</span>
								<span>{item.label}</span>
							</div>
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{item.detail}</p>
							<p class="mt-1.5 text-sm leading-6 text-[var(--text-muted)]">{item.state}</p>
						</div>
					{/each}
				</div>
			</section>

			<section class="rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Shell principle</p>
				<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">What carries over from the concept is structure: route rail, queue pane, fixed context header, and a large working canvas. The content stays product-ops specific to TurnKeyOps, including this shared workflow model instead of tenant-office screens.</p>
			</section>
		</div>
	</div>

	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Shared entity model</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Required records, ownership, and edit rules</h3>
		</div>
		<div class="overflow-x-auto">
			<table class="min-w-full text-left text-sm">
				<thead class="bg-[var(--shell-panel)] text-[0.72rem] uppercase tracking-[0.14em] text-[var(--muted)]">
					<tr>
						<th class="px-4 py-3 font-medium">Entity</th>
						<th class="px-4 py-3 font-medium">Required fields</th>
						<th class="px-4 py-3 font-medium">Relationships</th>
						<th class="px-4 py-3 font-medium">Source of truth</th>
						<th class="px-4 py-3 font-medium">Editable by stage</th>
						<th class="px-4 py-3 font-medium">Derived / computed</th>
					</tr>
				</thead>
				<tbody>
					{#each entityRows as row}
						<tr class="border-t border-[var(--shell-border)] align-top text-[var(--text-base)]">
							<td class="px-4 py-3 font-semibold text-[var(--text-strong)]">{row.entity}</td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{row.requiredFields}</td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{row.relationships}</td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{row.sourceOfTruth}</td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{row.editableByStage}</td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{row.derivedFields}</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	</section>

	<div class="grid gap-3 xl:grid-cols-[1.03fr_0.97fr]">
		<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Stage editability</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Which fields stay editable versus derived</h3>
			</div>
			<div class="divide-y divide-[var(--shell-border)]">
				{#each stageRules as rule}
					<div class="grid gap-3 px-4 py-4 lg:grid-cols-[0.72fr_1fr_1fr]">
						<div>
							<p class="text-base font-semibold text-[var(--text-strong)]">{rule.stage}</p>
						</div>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
							<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--muted)]">Operator editable</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{rule.operatorEditable}</p>
						</div>
						<div class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-3">
							<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Derived or locked</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{rule.derivedOrLocked}</p>
						</div>
					</div>
				{/each}
			</div>
		</section>

		<div class="grid gap-3">
			<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Estimate seeding</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">How visit data becomes estimate data</h3>
				<ul class="mt-4 space-y-2.5">
					{#each estimateSeedInputs as item}
						<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{item}</li>
					{/each}
				</ul>
			</section>

			<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Template backlog</p>
				<ul class="mt-3 space-y-2.5">
					{#each templateBacklog as item}
						<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{item}</li>
					{/each}
				</ul>
			</section>
		</div>
	</div>

	<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
		<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Open schema questions</p>
		<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Follow-up decisions for implementation review</h3>
		<div class="mt-4 grid gap-3 xl:grid-cols-2">
			{#each openQuestions as item}
				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{item}</div>
			{/each}
		</div>
	</section>
</div>
