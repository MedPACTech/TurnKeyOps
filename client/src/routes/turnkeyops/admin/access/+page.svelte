<script lang="ts">
	type ActorId = 'internal-admin' | 'internal-client' | 'external-admin' | 'external-client';
	type PermissionState = 'primary' | 'conditional' | 'none';
	type PermissionCell = {
		state: PermissionState;
		note: string;
	};

	const actorOrder: ActorId[] = ['internal-admin', 'internal-client', 'external-admin', 'external-client'];

	const actorMeta: Record<
		ActorId,
		{
			label: string;
			surface: string;
			audience: string;
			summary: string;
			primaryUi: string[];
			hiddenUi: string[];
			handoff: string;
		}
	> = {
		'internal-admin': {
			label: 'Internal Admin',
			surface: 'TurnKeyOps operator console',
			audience: 'Platform operators, implementation leads, support escalation',
			summary:
				'Sees cross-tenant health, launch gates, audit posture, and exception workflows without becoming the day-to-day tenant office queue owner.',
			primaryUi: [
				'Portfolio rollout board and launch gates',
				'Audit summaries, billing exceptions, and policy controls',
				'Read-only tenant workflow context for escalation and QA'
			],
			hiddenUi: [
				'Tenant production inbox as the default home surface',
				'Field-execution-only controls',
				'Customer-facing public quote and estimate receipt views'
			],
			handoff: 'Reviews intake and estimate exceptions, then hands execution back to the tenant admin lane.'
		},
		'internal-client': {
			label: 'Internal Client',
			surface: 'TurnKeyOps public / sales narrative',
			audience: 'Prospects, buyers, partners, and internal storytelling',
			summary:
				'Explains the platform and rollout model, but never exposes live tenant work queues, operator controls, or privileged workflow actions.',
			primaryUi: [
				'Platform positioning and implementation narrative',
				'Contact / demo pathways',
				'High-level workflow explanation without operational data'
			],
			hiddenUi: [
				'Admin queues, customer records, and schedule boards',
				'Visit-completion and estimate-send controls',
				'Identity, billing, or tenant-membership settings'
			],
			handoff: 'Hands interested buyers into implementation or sales follow-up rather than operational workflow ownership.'
		},
		'external-admin': {
			label: 'External Admin',
			surface: 'Tenant admin workspace',
			audience: 'Owner, office admin, estimator, or tenant operations staff',
			summary:
				'Owns the request-to-visit-to-estimate workflow for one tenant, with the queue, scheduling, field handoff, and estimate controls visible in one operational shell.',
			primaryUi: [
				'Request inbox, scheduling desk, and estimate workspace',
				'Customer records, attachments, and workflow status controls',
				'Tenant-scoped membership or billing controls where role allows'
			],
			hiddenUi: [
				'Cross-tenant rollout telemetry and platform governance rails',
				'Operator-only audit exceptions and launch-gate controls',
				'TurnKeyOps public marketing-management concerns'
			],
			handoff: 'Owns intake and scheduling, then hands site-visit execution to field staff and estimate receipt to the external client.'
		},
		'external-client': {
			label: 'External Client',
			surface: 'Tenant public / customer-facing workflow',
			audience: 'Homeowners, customers, and request submitters',
			summary:
				'Can start a request and receive customer-facing outputs, but never sees internal queue logic, staff notes, policy controls, or privileged estimate editing.',
			primaryUi: [
				'Quote-request form and service information',
				'Appointment / estimate receipt touchpoints',
				'Customer-readable status or next-step messaging only'
			],
			hiddenUi: [
				'Internal workflow phases, admin notes, and billing exceptions',
				'Scheduling ownership controls and field-completion tools',
				'Platform admin, tenant membership, and audit trails'
			],
			handoff: 'Creates demand and receives outputs; all operational ownership stays with the tenant admin workflow.'
		}
	};

	const workflowActions: Array<{
		action: string;
		permissions: Record<ActorId, PermissionCell>;
	}> = [
		{
			action: 'Create intake / request',
			permissions: {
				'internal-admin': { state: 'conditional', note: 'Allowed for support or migration exceptions, not as the default intake path.' },
				'internal-client': { state: 'none', note: 'Public storytelling surface only.' },
				'external-admin': { state: 'primary', note: 'Office staff can capture phone, referral, or manual intake for their tenant.' },
				'external-client': { state: 'primary', note: 'Customer submits the public quote / request form.' }
			}
		},
		{
			action: 'View workflow records',
			permissions: {
				'internal-admin': { state: 'primary', note: 'Can review tenant records across the lifecycle for rollout, QA, and support.' },
				'internal-client': { state: 'none', note: 'No live operational record access.' },
				'external-admin': { state: 'primary', note: 'Owns day-to-day visibility into request, visit, and estimate state.' },
				'external-client': { state: 'conditional', note: 'Sees only their own customer-facing request / estimate outputs.' }
			}
		},
		{
			action: 'Edit workflow details',
			permissions: {
				'internal-admin': { state: 'conditional', note: 'Edits are limited to exception handling, configuration, or corrective intervention.' },
				'internal-client': { state: 'none', note: 'No workflow editing controls.' },
				'external-admin': { state: 'primary', note: 'Owns queue notes, scheduling prep, scope edits, and estimate drafting.' },
				'external-client': { state: 'none', note: 'Customer does not edit internal workflow state after submission.' }
			}
		},
		{
			action: 'Schedule site visit',
			permissions: {
				'internal-admin': { state: 'conditional', note: 'Can step in for escalation or launch support, but tenant ops should own the calendar.' },
				'internal-client': { state: 'none', note: 'No scheduling tools.' },
				'external-admin': { state: 'primary', note: 'Tenant office owns appointment selection, assignment, and customer coordination.' },
				'external-client': { state: 'conditional', note: 'May pick from offered slots or confirm availability, but does not own scheduling rules.' }
			}
		},
		{
			action: 'Complete visit / capture field outcome',
			permissions: {
				'internal-admin': { state: 'none', note: 'Platform operators should not be the default field-completion actor.' },
				'internal-client': { state: 'none', note: 'No field-work tools.' },
				'external-admin': { state: 'conditional', note: 'Tenant admin can finalize or reconcile field results, but field staff owns first capture.' },
				'external-client': { state: 'none', note: 'Customers confirm access or appointment completion, not internal visit data.' }
			}
		},
		{
			action: 'Send estimate',
			permissions: {
				'internal-admin': { state: 'conditional', note: 'Available only for support escalation or rollout intervention, not normal tenant selling motion.' },
				'internal-client': { state: 'none', note: 'No commercial workflow controls.' },
				'external-admin': { state: 'primary', note: 'Tenant office owns draft review, send, and follow-up.' },
				'external-client': { state: 'none', note: 'Receives estimates rather than sending them.' }
			}
		},
		{
			action: 'Receive estimate',
			permissions: {
				'internal-admin': { state: 'conditional', note: 'Can review customer packet for QA or support, not as the customer recipient.' },
				'internal-client': { state: 'none', note: 'No customer delivery role.' },
				'external-admin': { state: 'conditional', note: 'Sees sent-state confirmation and follow-up tasks, not the final recipient experience.' },
				'external-client': { state: 'primary', note: 'Customer receives and responds to the estimate packet.' }
			}
		}
	];

	const handoffRules = [
		{
			step: 'Intake',
			owner: 'External Client or External Admin',
			handoff: 'New requests enter the tenant admin queue. Internal Admin is read-only unless support or migration intervention is needed.',
			uiShift: 'External Client sees a simple public form; External Admin sees queue ownership, missing-info flags, and next-action controls.'
		},
		{
			step: 'Scheduling',
			owner: 'External Admin',
			handoff: 'Tenant office owns appointment selection and staff assignment, then hands an approved visit to field execution.',
			uiShift: 'Scheduling controls, calendar ownership, and resource assignment live in the tenant admin shell, not in the public or platform surfaces.'
		},
		{
			step: 'Visit completion',
			owner: 'Field staff with External Admin review',
			handoff: 'Field observations become the estimate seed. External Admin reconciles completeness before the estimate lane advances.',
			uiShift: 'Field-completion tools stay out of platform governance views and customer-facing surfaces; tenant admin sees readiness and follow-up prompts.'
		},
		{
			step: 'Estimate send',
			owner: 'External Admin',
			handoff: 'The tenant office sends the customer packet. External Client becomes the recipient, while Internal Admin remains exception-only.',
			uiShift: 'Tenant admin keeps send controls and revision context; the customer sees only the receipt / response experience.'
		}
	];

	const surfaceCoverage = [
		{
			surface: 'TurnKeyOps admin',
			audience: 'Platform operators + implementation leads',
			route: '/turnkeyops/admin',
			focus: 'Cross-tenant rollout health, controls, and launch readiness',
			guardrail: 'Never inherits tenant-specific office workflows or branding.'
		},
		{
			surface: 'BDR admin',
			audience: 'Tenant operators',
			route: '/bdr/admin',
			focus: 'Quote, scheduling, invoicing, content, and customer operations for one tenant',
			guardrail: 'No cross-tenant governance, portfolio health, or platform policy controls.'
		},
		{
			surface: 'TurnKeyOps public',
			audience: 'Prospects + implementation buyers',
			route: '/turnkeyops/public',
			focus: 'Platform story, rollout model, and product positioning',
			guardrail: 'Explains the product; does not expose operator controls or tenant data.'
		},
		{
			surface: 'BDR public',
			audience: 'Tenant customers',
			route: '/bdr/public',
			focus: 'Lead capture, trust proof, and service marketing for one tenant brand',
			guardrail: 'Customer-facing only; no internal admin controls or platform oversight.'
		}
	];

	const controlRules = [
		{
			title: 'Invite redemption stays exact-match',
			detail: 'Users must redeem against the invited email or phone. Alternate verified contacts cannot claim the invite.'
		},
		{
			title: 'Verified contact changes require OTP',
			detail: 'Primary contact changes happen through verify-new-contact flow, not through direct profile edits.'
		},
		{
			title: 'Admin reassignment uses release + re-invite',
			detail: 'Admins cannot overwrite another user’s verified login contact. Seat changes require a new invite flow.'
		},
		{
			title: 'Billing admins stay inside billing bounds',
			detail: 'Billing admins can manage seats, subscriptions, and top-up settings, but not ledger history or verified identity state.'
		}
	];

	const launchGates = [
		'Validated data is signed off by implementation and tenant owners before go-live.',
		'Billing configuration and exception handling are reviewed with named owners.',
		'Admin training is complete with support escalation coverage documented.',
		'Privileged actions, audit posture, and rollback path are confirmed before launch approval.'
	];

	const openQuestions = [
		'Scheduling ownership is tenant-admin primary; Internal Admin can intervene only as an exception path, not as the default operating lane.',
		'Field-entry responsibility stays with tenant field staff plus tenant-admin reconciliation; the platform console should expose readiness, not replace field execution.',
		'Customer-facing surfaces should show confirmations and estimate receipt states without leaking internal workflow phases, notes, or privileged policy controls.'
	];

	let selectedActor = $state<ActorId>('external-admin');

	const selectedActorMeta = $derived(actorMeta[selectedActor]);

	function badgeClass(state: PermissionState) {
		switch (state) {
			case 'primary':
				return 'border-emerald-300 bg-emerald-50 text-emerald-700';
			case 'conditional':
				return 'border-amber-300 bg-amber-50 text-amber-700';
			default:
				return 'border-slate-300 bg-slate-100 text-slate-600';
		}
	}

	function badgeLabel(state: PermissionState) {
		switch (state) {
			case 'primary':
				return 'Primary';
			case 'conditional':
				return 'Conditional';
			default:
				return 'No';
		}
	}
</script>

<div class="space-y-4">
	<div class="grid gap-3 xl:grid-cols-[1.18fr_0.82fr]">
		<section class="rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Permission-driven UI</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Which surface changes for each actor</h3>
			</div>
			<div class="space-y-4 p-4">
				<div class="flex flex-wrap gap-2">
					{#each actorOrder as actorId}
						<button
							type="button"
							onclick={() => (selectedActor = actorId)}
							class={`rounded-md border px-3 py-2 text-left text-sm transition ${selectedActor === actorId ? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--text-strong)] shadow-sm' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] text-[var(--text-muted)] hover:bg-white'}`}
						>
							<p class="font-semibold">{actorMeta[actorId].label}</p>
							<p class="mt-0.5 text-xs uppercase tracking-[0.12em]">{actorMeta[actorId].surface}</p>
						</button>
					{/each}
				</div>

				<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Active role</p>
					<h4 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">{selectedActorMeta.label}</h4>
					<p class="mt-1 text-sm text-[var(--muted)]">{selectedActorMeta.audience}</p>
					<p class="mt-3 text-sm leading-6 text-[var(--text-muted)]">{selectedActorMeta.summary}</p>
				</div>

				<div class="grid gap-3 lg:grid-cols-[1fr_1fr]">
					<div class="rounded-md border border-[var(--shell-border)] bg-white p-4">
						<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--muted)]">UI shown first</p>
						<ul class="mt-3 space-y-2 text-sm leading-6 text-[var(--text-muted)]">
							{#each selectedActorMeta.primaryUi as item}
								<li>• {item}</li>
							{/each}
						</ul>
					</div>
					<div class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
						<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">UI withheld</p>
						<ul class="mt-3 space-y-2 text-sm leading-6 text-[var(--text-muted)]">
							{#each selectedActorMeta.hiddenUi as item}
								<li>• {item}</li>
							{/each}
						</ul>
					</div>
				</div>
			</div>
		</section>

		<section class="rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Why this matters</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">UI differences now stay explicit</h3>
			<ul class="mt-3 space-y-2 text-sm leading-6 text-[var(--text-muted)]">
				<li>• platform governance stays out of tenant office queues</li>
				<li>• tenant admin keeps the request / visit / estimate operating surface</li>
				<li>• customer views stay simple and customer-readable</li>
				<li>• open ownership questions are flagged instead of hidden in copy</li>
			</ul>
			<div class="mt-4 rounded-md border border-[var(--shell-border)] bg-white/70 px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">
				{selectedActorMeta.handoff}
			</div>
		</section>
	</div>

	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Workflow permission matrix</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Who can do what across the MVP flow</h3>
		</div>
		<div class="overflow-x-auto">
			<table class="min-w-full text-left text-sm">
				<thead class="bg-[var(--shell-panel)] text-[0.72rem] uppercase tracking-[0.14em] text-[var(--muted)]">
					<tr>
						<th class="px-4 py-3 font-medium">Action</th>
						{#each actorOrder as actorId}
							<th class="px-4 py-3 font-medium">{actorMeta[actorId].label}</th>
						{/each}
					</tr>
				</thead>
				<tbody>
					{#each workflowActions as row}
						<tr class="border-t border-[var(--shell-border)] align-top text-[var(--text-base)]">
							<td class="px-4 py-3 font-semibold text-[var(--text-strong)]">{row.action}</td>
							{#each actorOrder as actorId}
								{@const permission = row.permissions[actorId]}
								<td class="px-4 py-3 text-[var(--text-muted)]">
									<span class={`inline-flex rounded-full border px-2 py-0.5 text-[0.68rem] font-semibold uppercase tracking-[0.12em] ${badgeClass(permission.state)}`}>
										{badgeLabel(permission.state)}
									</span>
									<p class="mt-2 leading-6">{permission.note}</p>
								</td>
							{/each}
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	</section>

	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Ownership and handoffs</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Where workflow control changes hands</h3>
		</div>
		<div class="grid gap-3 p-4 xl:grid-cols-2">
			{#each handoffRules as item}
				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<div class="flex flex-wrap items-start justify-between gap-3">
						<div>
							<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--muted)]">{item.step}</p>
							<h4 class="mt-1 text-base font-semibold text-[var(--text-strong)]">{item.owner}</h4>
						</div>
					</div>
					<p class="mt-3 text-sm leading-6 text-[var(--text-muted)]">{item.handoff}</p>
					<div class="mt-3 rounded-md border border-[var(--accent-border)] bg-white px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">
						{item.uiShift}
					</div>
				</div>
			{/each}
		</div>
	</section>

	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Surface map</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Where each permission model applies</h3>
		</div>
		<div class="overflow-x-auto">
			<table class="min-w-full text-left text-sm">
				<thead class="bg-[var(--shell-panel)] text-[0.72rem] uppercase tracking-[0.14em] text-[var(--muted)]">
					<tr>
						<th class="px-4 py-3 font-medium">Surface</th>
						<th class="px-4 py-3 font-medium">Audience</th>
						<th class="px-4 py-3 font-medium">Route</th>
						<th class="px-4 py-3 font-medium">Primary focus</th>
						<th class="px-4 py-3 font-medium">Guardrail</th>
					</tr>
				</thead>
				<tbody>
					{#each surfaceCoverage as item}
						<tr class="border-t border-[var(--shell-border)] align-top text-[var(--text-base)]">
							<td class="px-4 py-3 font-semibold text-[var(--text-strong)]">{item.surface}</td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{item.audience}</td>
							<td class="px-4 py-3"><span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-2 py-1 text-xs font-medium text-[var(--text-muted)]">{item.route}</span></td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{item.focus}</td>
							<td class="px-4 py-3 text-[var(--text-muted)]">{item.guardrail}</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	</section>

	<div class="grid gap-3 xl:grid-cols-[1fr_1fr]">
		<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Control rules</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Non-bypassable identity and billing rules</h3>
			<div class="mt-4 grid gap-3">
				{#each controlRules as rule}
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
						<p class="text-sm font-semibold text-[var(--text-strong)]">{rule.title}</p>
						<p class="mt-1.5 text-sm leading-6 text-[var(--text-muted)]">{rule.detail}</p>
					</div>
				{/each}
			</div>
		</section>

		<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Launch gates + open questions</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">What still must stay explicit</h3>
			<ul class="mt-4 space-y-2.5">
				{#each launchGates as gate}
					<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{gate}</li>
				{/each}
			</ul>
			<div class="mt-4 rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
				<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Explicitly flagged now</p>
				<ul class="mt-3 space-y-2 text-sm leading-6 text-[var(--text-muted)]">
					{#each openQuestions as item}
						<li>• {item}</li>
					{/each}
				</ul>
			</div>
		</section>
	</div>
</div>
