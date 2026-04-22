<script lang="ts">
	const roleSets = [
		{ name: 'Platform operators', scope: 'Cross-tenant visibility', note: 'See rollout status, health, controls, and launch readiness across the portfolio.' },
		{ name: 'Implementation leads', scope: 'Tenant setup + migration', note: 'Can configure tenants and advance playbooks, but do not own platform-wide policy alone.' },
		{ name: 'Tenant admins', scope: 'Tenant-only operations', note: 'Operate their workspace without access to platform internals or other tenants.' }
	];

	const launchGates = [
		'Validated data signed off by implementation and tenant owners.',
		'Billing rules configured with exception paths reviewed.',
		'Admin training complete with named operators and escalation path.',
		'Support coverage and rollback posture agreed for launch window.'
	];
</script>

<div class="grid gap-3 xl:grid-cols-[1.04fr_0.96fr]">
	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Access model</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Role architecture across platform and tenant surfaces</h3>
		</div>
		<div class="divide-y divide-[var(--shell-border)]">
			{#each roleSets as role}
				<div class="grid gap-3 px-4 py-4 lg:grid-cols-[0.9fr_0.8fr_1.2fr]">
					<p class="text-base font-semibold text-[var(--text-strong)]">{role.name}</p>
					<p class="text-sm text-[var(--muted)]">{role.scope}</p>
					<p class="text-sm leading-6 text-[var(--text-muted)]">{role.note}</p>
				</div>
			{/each}
		</div>
	</section>

	<div class="grid gap-3">
		<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Launch gates</p>
			<ul class="mt-3 space-y-2.5">
				{#each launchGates as gate}
					<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{gate}</li>
				{/each}
			</ul>
		</section>

		<section class="rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Boundary reminder</p>
			<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">This is deliberately a platform control-plane route. BDR admin remains tenant operations, not a place for cross-tenant governance.</p>
		</section>
	</div>
</div>
