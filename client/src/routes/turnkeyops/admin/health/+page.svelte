<script lang="ts">
	const services = [
		{ name: 'Authentication + roles', status: 'Healthy', note: 'Permission shapes are stabilizing, but cross-tenant support roles still need refinement.' },
		{ name: 'Billing orchestration', status: 'Watch', note: 'Invoice exceptions remain too hard-coded around BDR-style workflows.' },
		{ name: 'Calendar + scheduling', status: 'Healthy', note: 'The shared model is strong, but other verticals need fewer job-site assumptions.' },
		{ name: 'Data migration QA', status: 'Risk', note: 'Validation tooling is lagging the pace of onboarding.' }
	];

	const incidents = [
		'One degraded sync is delaying clean reporting in pilot environments.',
		'Billing rule regressions surface late because edge-case coverage is thin.',
		'Duplicate customer records remain the biggest migration trust hit.'
	];
</script>

<div class="grid gap-3 xl:grid-cols-[1.08fr_0.92fr]">
	<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
		<div class="border-b border-[var(--shell-border)] px-4 py-3">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Service readout</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Shared capabilities that drive launch confidence</h3>
		</div>
		<div class="divide-y divide-[var(--shell-border)]">
			{#each services as service}
				<div class="grid gap-3 px-4 py-4 lg:grid-cols-[1fr_120px_1.2fr] lg:items-start">
					<p class="text-base font-semibold text-[var(--text-strong)]">{service.name}</p>
					<span class={`inline-flex w-fit items-center rounded-md border px-2.5 py-1 text-[0.68rem] uppercase tracking-[0.16em] ${service.status === 'Healthy' ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : service.status === 'Watch' ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-rose-200 bg-rose-50 text-rose-700'}`}>{service.status}</span>
					<p class="text-sm leading-6 text-[var(--text-muted)]">{service.note}</p>
				</div>
			{/each}
		</div>
	</section>

	<div class="grid gap-3">
		<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Incident themes</p>
			<ul class="mt-3 space-y-2.5">
				{#each incidents as incident}
					<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{incident}</li>
				{/each}
			</ul>
		</section>

		<section class="rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Operator translation</p>
			<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">This route is framed around operator trust: what is healthy enough to standardize, what is risky enough to slow rollout, and where technical debt becomes launch debt.</p>
		</section>
	</div>
</div>
