<script lang="ts">
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

	const platformRoles = [
		{
			role: 'Platform operator',
			scope: 'Portfolio-wide control plane',
			can: ['See rollout health across tenants', 'Review launch gates and audit posture', 'Own platform-level policy decisions'],
			cannot: 'Should not act as a tenant office admin inside day-to-day production queues.'
		},
		{
			role: 'Implementation lead',
			scope: 'Tenant setup + launch execution',
			can: ['Configure tenants and playbooks', 'Drive migration and readiness work', 'Escalate reusable gaps into product backlog'],
			cannot: 'Should not unilaterally change platform policy or privileged billing controls.'
		},
		{
			role: 'Support + finance',
			scope: 'Operational exceptions and revenue controls',
			can: ['Handle billing exceptions and support escalation', 'Review audit trail context for high-risk actions', 'Confirm training and launch coverage'],
			cannot: 'Should not bypass invite verification, ledger history, or approval evidence.'
		}
	];

	const tenantRoles = [
		{
			role: 'Owner',
			assignable: 'No',
			coverage: 'Full tenant control',
			includes: 'Membership administration + billing authority',
			constraints: 'Reserved for the tenant owner.'
		},
		{
			role: 'Admin',
			assignable: 'Yes',
			coverage: 'Tenant administration',
			includes: 'Manage membership, invites, and tenant role assignments',
			constraints: 'Does not automatically become billing admin.'
		},
		{
			role: 'Billing Admin',
			assignable: 'Yes',
			coverage: 'Billing operations',
			includes: 'Manage billing settings and billing workflows',
			constraints: 'No verified-contact edits or invite-rule bypass.'
		},
		{
			role: 'Member',
			assignable: 'Yes',
			coverage: 'Standard tenant access',
			includes: 'Base application usage without tenant-admin or billing-admin authority',
			constraints: 'No membership or billing administration.'
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

	const modelNotes = [
		'Platform and tenant access are intentionally documented as separate layers so rollout work does not blur into day-to-day office operations.',
		'Current tenant role definitions are grounded in the system role catalog (`Owner`, `Admin`, `Billing Admin`, `Member`) while broader platform operator roles remain a documented operating model.',
		'Permission language here is meant to make launch decisions legible now, even while deeper cross-surface enforcement work continues in follow-up cards.'
	];
</script>

<div class="space-y-4">
	<div class="grid gap-3 xl:grid-cols-[1.18fr_0.82fr]">
		<section class="rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Role model</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Cross-surface permission boundaries</h3>
			</div>
			<div class="grid gap-3 p-4 md:grid-cols-2">
				{#each modelNotes as note}
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">
						{note}
					</div>
				{/each}
			</div>
		</section>

		<section class="rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Documented now</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">What this route clarifies</h3>
			<ul class="mt-3 space-y-2 text-sm leading-6 text-[var(--text-muted)]">
				<li>• which surfaces are platform-wide versus tenant-only</li>
				<li>• which roles are operating-model roles versus current tenant roles</li>
				<li>• which guardrails cannot be bypassed during invites, billing, and launch readiness</li>
			</ul>
		</section>
	</div>

	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Surface map</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Where each role model applies</h3>
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

	<div class="grid gap-3 xl:grid-cols-[0.96fr_1.04fr]">
		<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Platform operating roles</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Who owns cross-tenant decisions</h3>
			</div>
			<div class="divide-y divide-[var(--shell-border)]">
				{#each platformRoles as item}
					<div class="px-4 py-4">
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div>
								<p class="text-base font-semibold text-[var(--text-strong)]">{item.role}</p>
								<p class="mt-1 text-sm text-[var(--muted)]">{item.scope}</p>
							</div>
						</div>
						<div class="mt-3 grid gap-3 lg:grid-cols-[0.92fr_1.08fr]">
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
								<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--muted)]">Can do</p>
								<ul class="mt-2 space-y-1.5 text-sm leading-6 text-[var(--text-muted)]">
									{#each item.can as grant}
										<li>• {grant}</li>
									{/each}
								</ul>
							</div>
							<div class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-3">
								<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Boundary</p>
								<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{item.cannot}</p>
							</div>
						</div>
					</div>
				{/each}
			</div>
		</section>

		<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Tenant role catalog</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Current assignable tenant roles</h3>
			</div>
			<div class="overflow-x-auto">
				<table class="min-w-full text-left text-sm">
					<thead class="bg-[var(--shell-panel)] text-[0.72rem] uppercase tracking-[0.14em] text-[var(--muted)]">
						<tr>
							<th class="px-4 py-3 font-medium">Role</th>
							<th class="px-4 py-3 font-medium">Assignable</th>
							<th class="px-4 py-3 font-medium">Coverage</th>
							<th class="px-4 py-3 font-medium">Includes</th>
							<th class="px-4 py-3 font-medium">Constraint</th>
						</tr>
					</thead>
					<tbody>
						{#each tenantRoles as item}
							<tr class="border-t border-[var(--shell-border)] align-top text-[var(--text-base)]">
								<td class="px-4 py-3 font-semibold text-[var(--text-strong)]">{item.role}</td>
								<td class="px-4 py-3 text-[var(--text-muted)]">{item.assignable}</td>
								<td class="px-4 py-3 text-[var(--text-muted)]">{item.coverage}</td>
								<td class="px-4 py-3 text-[var(--text-muted)]">{item.includes}</td>
								<td class="px-4 py-3 text-[var(--text-muted)]">{item.constraints}</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		</section>
	</div>

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
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Launch gates</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Evidence required before go-live</h3>
			<ul class="mt-4 space-y-2.5">
				{#each launchGates as gate}
					<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{gate}</li>
				{/each}
			</ul>
		</section>
	</div>
</div>
