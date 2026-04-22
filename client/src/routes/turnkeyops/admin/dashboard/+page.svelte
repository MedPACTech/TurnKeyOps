<script lang="ts">
	const launchBoard = [
		{ label: 'Launches in motion', value: '3', detail: 'BDR live, one tenant in configuration, one in discovery close.' },
		{ label: 'Critical blockers', value: '4', detail: 'Data mapping, billing policy, permissions cleanup, training assets.' },
		{ label: 'Release confidence', value: '84%', detail: 'Expansion is viable, but rollout debt is still visible.' },
		{ label: 'Exec watchlist', value: '5', detail: 'Items most likely to delay repeatable tenant launches.' }
	];

	const rolloutRows = [
		{ tenant: 'BDR Construction', stage: 'Live', owner: 'Implementation + Product', risk: 'Reference tenant drift', next: 'Promote stable launch checks into shared defaults', status: 'Stable' },
		{ tenant: 'Field Services Pilot', stage: 'Configuration', owner: 'Solutions Architect', risk: 'Role and billing edge cases', next: 'Lock modular permission + invoicing rules', status: 'At risk' },
		{ tenant: 'White-Glove Services', stage: 'Discovery', owner: 'Platform PM', risk: 'Contractor assumptions in workflow model', next: 'Define vertical object model before setup', status: 'Scoping' }
	];

	const decisionQueue = [
		{ lane: 'Navigation model', owner: 'Product design', due: 'Today', note: 'Freeze platform-vs-tenant shell conventions before more admin routes ship.' },
		{ lane: 'Role model', owner: 'Platform ops', due: 'Tomorrow', note: 'Split implementation, support, finance, and tenant admin access cleanly.' },
		{ lane: 'Template promotion', owner: 'Implementation lead', due: 'Thu', note: 'Decide which BDR launch workarounds deserve product-level playbooks.' },
		{ lane: 'Migration QA', owner: 'Engineering', due: 'Fri', note: 'Reduce duplicate-record risk before the next pilot imports data.' }
	];
</script>

<div class="space-y-4">
	<div class="grid gap-3 xl:grid-cols-[1.32fr_0.68fr]">
		<section class="rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Portfolio board</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Launch pressure across the portfolio</h3>
			</div>
			<div class="grid gap-3 p-4 md:grid-cols-2 xl:grid-cols-4">
				{#each launchBoard as item}
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
						<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--muted)]">{item.label}</p>
						<p class="mt-1.5 text-2xl font-semibold text-[var(--text-strong)]">{item.value}</p>
						<p class="mt-1.5 text-sm leading-5 text-[var(--text-muted)]">{item.detail}</p>
					</div>
				{/each}
			</div>
		</section>

		<section class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Operator note</p>
			<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Why this shell changed</h3>
			<p class="mt-3 text-sm leading-6 text-[var(--text-muted)]">TurnKeyOps admin now reads like a platform operations surface instead of a generic hero page: fixed header, persistent route rail, route queue, and a dense workspace canvas built for multi-tenant oversight.</p>
		</section>
	</div>

	<div class="grid gap-3 xl:grid-cols-[1.08fr_0.92fr]">
		<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Rollout matrix</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Tenant momentum and current constraints</h3>
			</div>
			<div class="overflow-x-auto">
				<table class="min-w-full text-left text-sm">
					<thead class="bg-[var(--shell-panel)] text-[0.72rem] uppercase tracking-[0.14em] text-[var(--muted)]">
						<tr>
							<th class="px-4 py-3 font-medium">Tenant</th>
							<th class="px-4 py-3 font-medium">Stage</th>
							<th class="px-4 py-3 font-medium">Owner</th>
							<th class="px-4 py-3 font-medium">Primary risk</th>
							<th class="px-4 py-3 font-medium">Next move</th>
						</tr>
					</thead>
					<tbody>
						{#each rolloutRows as row}
							<tr class="border-t border-[var(--shell-border)] align-top text-[var(--text-base)]">
								<td class="px-4 py-3">
									<p class="font-semibold text-[var(--text-strong)]">{row.tenant}</p>
									<p class="mt-1 text-xs uppercase tracking-[0.12em] text-[var(--muted)]">{row.status}</p>
								</td>
								<td class="px-4 py-3">{row.stage}</td>
								<td class="px-4 py-3">{row.owner}</td>
								<td class="px-4 py-3 text-[var(--text-muted)]">{row.risk}</td>
								<td class="px-4 py-3 text-[var(--text-muted)]">{row.next}</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		</section>

		<section class="overflow-hidden rounded-lg border border-[var(--shell-border)] bg-white">
			<div class="border-b border-[var(--shell-border)] px-4 py-3">
				<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Decision queue</p>
				<h3 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">This week’s platform actions</h3>
			</div>
			<div class="divide-y divide-[var(--shell-border)]">
				{#each decisionQueue as item}
					<div class="px-4 py-3">
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-sm font-semibold text-[var(--text-strong)]">{item.lane}</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">{item.note}</p>
							</div>
							<div class="text-right text-xs uppercase tracking-[0.14em] text-[var(--muted)]">
								<p>{item.owner}</p>
								<p class="mt-1">{item.due}</p>
							</div>
						</div>
					</div>
				{/each}
			</div>
		</section>
	</div>
</div>
