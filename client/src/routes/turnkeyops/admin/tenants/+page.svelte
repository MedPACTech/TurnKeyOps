<script lang="ts">
	const tenants = [
		{ name: 'BDR Construction', vertical: 'Contractor', stage: 'Live', owner: 'Christian + implementation', readiness: '92%', blockers: 'Keep reference learnings from hard-coding the platform shell.', milestone: 'Graduate stable launch checks into shared playbooks.' },
		{ name: 'Field Services Pilot', vertical: 'Field services', stage: 'Configuration', owner: 'Solutions architect', readiness: '68%', blockers: 'Permissions and billing policies are still too bespoke.', milestone: 'Finalize modular roles + invoice exception rules.' },
		{ name: 'White-Glove Services', vertical: 'White-glove', stage: 'Discovery', owner: 'Platform PM', readiness: '41%', blockers: 'Workflow assumptions still lean contractor-first.', milestone: 'Define vertical object model before environment setup.' }
	];

	const portfolioNotes = [
		'Every tenant should carry owner, stage, confidence, next milestone, and escalation path.',
		'Product blockers must stay separate from implementation-only cleanup.',
		'Readiness should blend data, training, workflow coverage, and stakeholder confidence.'
	];
</script>

<div class="grid gap-3 xl:grid-cols-[1.15fr_0.85fr]">
	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Tenant board</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Readiness, blockers, and next milestone</h3>
		</div>
		<div class="divide-y divide-[var(--shell-border)]">
			{#each tenants as tenant}
				<div class="grid gap-3 px-4 py-4 lg:grid-cols-[1.1fr_0.7fr_1.2fr]">
					<div>
						<div class="flex flex-wrap items-center gap-2">
							<p class="text-base font-semibold text-[var(--text-strong)]">{tenant.name}</p>
							<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-2 py-1 text-[0.68rem] uppercase tracking-[0.14em] text-[var(--muted)]">{tenant.vertical}</span>
						</div>
						<p class="mt-1 text-sm text-[var(--text-muted)]">{tenant.stage} · {tenant.owner}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
						<p class="text-[0.62rem] uppercase tracking-[0.16em] text-[var(--muted)]">Readiness</p>
						<p class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{tenant.readiness}</p>
					</div>
					<div class="space-y-2 text-sm">
						<p class="text-[var(--text-muted)]"><span class="font-semibold text-[var(--text-strong)]">Current block:</span> {tenant.blockers}</p>
						<p class="text-[var(--text-muted)]"><span class="font-semibold text-[var(--text-strong)]">Next milestone:</span> {tenant.milestone}</p>
					</div>
				</div>
			{/each}
		</div>
	</section>

	<div class="grid gap-3">
		<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Portfolio rules</p>
			<ul class="mt-3 space-y-2.5">
				{#each portfolioNotes as note}
					<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{note}</li>
				{/each}
			</ul>
		</section>

		<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Separation check</p>
			<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">BDR remains a tenant workspace. This route is for multi-tenant rollout governance, not contractor-office operations.</p>
		</section>
	</div>
</div>
