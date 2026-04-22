<script lang="ts">
	const playbooks = [
		{ stage: 'Discovery', owner: 'Platform PM', artifact: 'Vertical fit memo + object map', standard: 'Confirm what belongs to shared product versus vertical-specific workflow before delivery starts.' },
		{ stage: 'Configuration', owner: 'Solutions architect', artifact: 'Tenant setup checklist', standard: 'Roles, workflow defaults, billing rules, and environment controls must be explicit.' },
		{ stage: 'Migration', owner: 'Implementation lead', artifact: 'Data validation packet', standard: 'Every migrated object needs QA, signoff, and rollback posture.' },
		{ stage: 'Training', owner: 'Enablement', artifact: 'Admin walkthrough + SOP set', standard: 'Operator confidence is a launch gate, not optional polish.' },
		{ stage: 'Go-live', owner: 'Platform ops', artifact: 'Launch readiness board', standard: 'Data, access, billing, and support coverage are green before launch.' }
	];

	const templateBacklog = [
		'Contractor template needs weather-aware scheduling and estimate-to-contract defaults.',
		'White-glove template needs appointment orchestration without crew/job-site assumptions.',
		'Admin shells should share the same structure even when module content changes by vertical.'
	];
</script>

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
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--muted)]">Template backlog</p>
			<ul class="mt-3 space-y-2.5">
				{#each templateBacklog as item}
					<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-sm leading-6 text-[var(--text-muted)]">{item}</li>
				{/each}
			</ul>
		</section>

		<section class="rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
			<p class="text-[0.64rem] uppercase tracking-[0.18em] text-[var(--accent-text)]">Shell principle</p>
			<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">What carries over from the concept is structure: route rail, queue pane, fixed context header, and a large working canvas. The content stays product-ops specific to TurnKeyOps.</p>
		</section>
	</div>
</div>
