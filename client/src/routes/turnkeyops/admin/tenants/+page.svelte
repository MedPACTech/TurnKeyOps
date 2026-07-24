<script lang="ts">
	import { tenants } from '$lib/config/tenants';

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
							<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-2 py-1 text-[0.68rem] uppercase tracking-[0.14em] text-[var(--muted)]">{tenant.tradeLabel}</span>
						</div>
						<p class="mt-1 text-sm capitalize text-[var(--text-muted)]">{tenant.status} · {tenant.services.length} configured services</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
						<p class="text-[0.62rem] uppercase tracking-[0.16em] text-[var(--muted)]">Trade profile</p>
						<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">{tenant.tradeProfile}</p>
					</div>
					<div class="space-y-2 text-sm">
						<p class="text-[var(--text-muted)]"><span class="font-semibold text-[var(--text-strong)]">Bob context:</span> {tenant.bobContext}</p>
						<div class="flex flex-wrap gap-2 pt-1">
							<a href={tenant.adminPath} class="rounded-md bg-[var(--text-strong)] px-3 py-2 text-xs font-semibold text-white">Open admin</a>
							<a href={tenant.publicPath} class="rounded-md border border-[var(--shell-border)] px-3 py-2 text-xs font-semibold text-[var(--text-strong)]">View public site</a>
						</div>
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
