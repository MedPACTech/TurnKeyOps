<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import type { AdminSettingField, AdminSettingGroup } from '$lib/admin-settings';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	let selectedGroupId = $state('');

	type BobMove = {
		label: string;
		detail: string;
		href: string;
	};

	const focusRing =
		'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--accent-border)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--shell-panel)]';

	const findField = (id: string, fallback: AdminSettingField): AdminSettingField =>
		data.groups.flatMap((group) => group.fields).find((field) => field.id === id) ?? fallback;

	const rulesDeskGroups = $derived<AdminSettingGroup[]>([
		{
			id: 'estimate-defaults',
			label: 'Estimate defaults',
			description: 'Baseline pricing and quote assumptions before an operator steps in.',
			outcome: 'Sets the standard commercial starting point for new estimate work.',
			fields: [
				findField('default-overhead', {
					id: 'default-overhead',
					label: 'Default overhead rate',
					value: '12%',
					type: 'percent',
					help: 'Applied before final markup so internal costing starts from a visible baseline.'
				}),
				findField('default-profit', {
					id: 'default-profit',
					label: 'Default target margin',
					value: '18%',
					type: 'percent',
					help: 'Used by the office when building standard estimate packets.'
				}),
				findField('rounding-mode', {
					id: 'rounding-mode',
					label: 'Estimate rounding mode',
					value: 'Round final total only',
					type: 'select',
					help: 'Controls how customer-facing totals are normalized.',
					options: ['Round line items to nearest dollar', 'Round final total only', 'No rounding']
				}),
				findField('tax-handling', {
					id: 'tax-handling',
					label: 'Sales tax handling',
					value: 'Apply by taxable material class',
					type: 'select',
					help: 'Defines how mixed-material scopes handle tax.',
					options: ['Apply by taxable material class', 'Apply at estimate total', 'Manual review required']
				})
			]
		},
		{
			id: 'scheduling-operations',
			label: 'Scheduling / operations rules',
			description: 'Rules that gate release timing, signature readiness, and field coordination.',
			outcome: 'Keeps schedule locks and production handoff predictable.',
			fields: [
				findField('insurance-supplement-window', {
					id: 'insurance-supplement-window',
					label: 'Insurance supplement review window',
					value: '2 days',
					type: 'days',
					help: 'How long supplement review can stay pending before escalation.'
				}),
				findField('schedule-release', {
					id: 'schedule-release',
					label: 'Schedule release buffer',
					value: '24 hours',
					type: 'hours',
					help: 'Minimum time between readiness approval and hard schedule lock.'
				}),
				findField('signature-required', {
					id: 'signature-required',
					label: 'Require signature before production',
					value: 'Enabled',
					type: 'toggle',
					help: 'Prevents production handoff if the estimate packet is unsigned.'
				})
			]
		},
		{
			id: 'billing-rules',
			label: 'Billing rules',
			description: 'Deposit, payment hold, and exception defaults used by the office team.',
			outcome: 'Makes the payment posture visible before work gets released.',
			fields: [
				findField('minimum-deposit', {
					id: 'minimum-deposit',
					label: 'Minimum deposit request',
					value: '30%',
					type: 'percent',
					help: 'Shown when a job requires material commitment before schedule lock.'
				}),
				findField('check-hold', {
					id: 'check-hold',
					label: 'Check hold duration',
					value: '3 days',
					type: 'days',
					help: 'Jobs paid by check stay on hold for this period before release.'
				}),
				findField('deposit-exception', {
					id: 'deposit-exception',
					label: 'Deposit exception owner',
					value: 'Owner approval',
					type: 'text',
					help: 'Who can override the default deposit requirement.'
				})
			]
		},
		{
			id: 'team-operator',
			label: 'Team / operator settings',
			description: 'Ownership and response defaults that keep the queue operating consistently.',
			outcome: 'Makes office routing predictable before deeper permissions or automation exist.',
			fields: [
				findField('intake-owner', {
					id: 'intake-owner',
					label: 'Default quote intake owner',
					value: 'Office intake',
					type: 'text',
					help: 'New website requests start with this owner unless reassigned.'
				}),
				findField('follow-up-sla', {
					id: 'follow-up-sla',
					label: 'First-response SLA',
					value: '15 minutes',
					type: 'hours',
					help: 'Operational target for emergency or priority web requests.'
				}),
				findField('estimate-review', {
					id: 'estimate-review',
					label: 'Estimate internal review required',
					value: 'Enabled',
					type: 'toggle',
					help: 'Keeps quote packets in draft until an internal review pass is complete.'
				})
			]
		},
		{
			id: 'workspace-identity',
			label: 'Workspace identity',
			description: 'Shared business identity defaults for office communication and scheduling context.',
			outcome: 'Keeps the workspace calm and trustworthy across customer-facing handoff.',
			fields: [
				{
					id: 'workspace-name',
					label: 'Workspace display name',
					value: 'Blue Diamond Roofing',
					type: 'text',
					help: 'Visible business label used across the External Admin shell.'
				},
				{
					id: 'reply-email',
					label: 'Default reply email',
					value: 'dispatch@bdr-demo.local',
					type: 'text',
					help: 'Used for outbound office follow-up and schedule coordination.'
				},
				{
					id: 'default-timezone',
					label: 'Default dispatch time zone',
					value: 'America/New_York',
					type: 'select',
					help: 'Controls the default scheduling context for office staff.',
					options: ['America/New_York', 'America/Chicago', 'America/Denver', 'America/Los_Angeles']
				}
			]
		}
	]);

	const selectedGroup = $derived(rulesDeskGroups.find((group) => group.id === selectedGroupId) ?? rulesDeskGroups[0]);

	$effect(() => {
		if (!selectedGroupId && rulesDeskGroups[0]) {
			selectedGroupId = rulesDeskGroups[0].id;
		}
	});

	const metrics = $derived([
		{
			label: 'Rule groups',
			value: String(rulesDeskGroups.length),
			detail: 'Estimate, operations, billing, team, and identity controls'
		},
		{
			label: 'Visible controls',
			value: String(rulesDeskGroups.reduce((sum, group) => sum + group.fields.length, 0)),
			detail: 'Concise inputs grouped by operational meaning'
		},
		{ label: 'Persistence', value: 'Scaffolded', detail: 'Editing surfaces remain ready for future API-backed settings storage' }
	]);

	const typeTone = (type: string) => {
		if (type === 'toggle') return 'border-emerald-300 bg-emerald-50 text-emerald-700';
		if (type === 'percent' || type === 'currency') return 'border-sky-300 bg-sky-50 text-sky-700';
		if (type === 'days' || type === 'hours') return 'border-amber-300 bg-amber-50 text-amber-700';
		return 'border-slate-300 bg-slate-50 text-slate-700';
	};

	const bobMoves = $derived.by(() => {
		if (!selectedGroup) {
			return [
				{
					label: 'Review rules desk',
					detail: `${rulesDeskGroups.length} grouped panels available`,
					href: '#rules-desk'
				}
			] satisfies BobMove[];
		}

		switch (selectedGroup.id) {
			case 'estimate-defaults':
				return [
					{ label: 'Check margin baseline', detail: 'Overhead, target margin, and rounding should stay aligned', href: '#default-overhead' },
					{ label: 'Review customer-facing totals', detail: 'Confirm tax and rounding rules still read cleanly', href: '#rounding-mode' },
					{ label: 'Flag deposit drift', detail: 'Make sure deposits match the estimate posture', href: '#minimum-deposit' }
				] satisfies BobMove[];
			case 'scheduling-operations':
				return [
					{ label: 'Check release timing', detail: 'Schedule buffers and supplement windows should not conflict', href: '#insurance-supplement-window' },
					{ label: 'Review production gate', detail: 'Signature requirement is the main release control here', href: '#signature-required' },
					{ label: 'Audit field readiness', detail: 'Make sure schedule rules still match office expectations', href: '#schedule-release' }
				] satisfies BobMove[];
			case 'billing-rules':
				return [
					{ label: 'Review deposit policy', detail: 'Deposit defaults and override ownership should stay explicit', href: '#minimum-deposit' },
					{ label: 'Check payment holds', detail: 'Hold timing should match actual release behavior', href: '#check-hold' },
					{ label: 'Look for exception creep', detail: 'Owner-only overrides should remain visible', href: '#deposit-exception' }
				] satisfies BobMove[];
			case 'team-operator':
				return [
					{ label: 'Confirm intake ownership', detail: 'New request routing should still match office staffing', href: '#intake-owner' },
					{ label: 'Check response target', detail: 'SLA targets should stay realistic for the team', href: '#follow-up-sla' },
					{ label: 'Review internal review gate', detail: 'Quote packets should not bypass internal review', href: '#estimate-review' }
				] satisfies BobMove[];
			default:
				return [
					{ label: 'Check visible identity', detail: 'Workspace naming should match the approved shell baseline', href: '#workspace-name' },
					{ label: 'Review office reply path', detail: 'Outbound communication defaults should stay trustworthy', href: '#reply-email' },
					{ label: 'Confirm scheduling context', detail: 'Timezone defaults should match the active office region', href: '#default-timezone' }
				] satisfies BobMove[];
		}
	});
</script>

<AdminWorkspace
	kicker="Admin / Settings"
	title="Simple rules desk for defaults, billing, and operator controls"
	description="Adjust business defaults without digging through a settings wall. The workflow stays intact; the framing is calmer and more operational."
	{metrics}
	contextLabel="Rule groups"
	focusLabel="Visible rules"
>
	{#snippet context()}
		<div class="space-y-3">
			{#each rulesDeskGroups as group}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition hover:border-[var(--accent-border)] ${focusRing} ${selectedGroup?.id === group.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (selectedGroupId = group.id)}
				>
					<div class="flex items-center justify-between gap-3">
						<p class="text-sm font-semibold text-[var(--text-strong)]">{group.label}</p>
						<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-[var(--text-base)]">
							{group.fields.length}
						</span>
					</div>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{group.outcome}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet focus()}
		{#if selectedGroup}
			<div class="space-y-2">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
					{selectedGroup.fields.length} controls in {selectedGroup.label}
				</p>
				{#each selectedGroup.fields as field}
					<a
						href={`#${field.id}`}
						class={`block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3 text-left transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel-strong)] ${focusRing}`}
					>
						<div class="flex items-start justify-between gap-3">
							<p class="text-sm font-semibold text-[var(--text-strong)]">{field.label}</p>
							<span class={`rounded-full border px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] ${typeTone(field.type)}`}>{field.type}</span>
						</div>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{field.help}</p>
					</a>
				{/each}
			</div>
		{/if}
	{/snippet}

	{#snippet work()}
		{#if selectedGroup}
			<div class="space-y-4" id="rules-desk">
				<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<div class="grid gap-3 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)]">
						<div>
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Rule group</p>
							<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedGroup.label}</h4>
							<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{selectedGroup.description}</p>
							<div class="mt-4 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-sm text-[var(--text-base)]">
								{selectedGroup.outcome}
							</div>
						</div>

						<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-1">
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
								<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Controls</p>
								<p class="mt-2 text-lg font-semibold text-[var(--text-strong)]">{selectedGroup.fields.length}</p>
								<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Editable rules in this panel</p>
							</div>
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
								<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Control types</p>
								<p class="mt-2 text-lg font-semibold text-[var(--text-strong)]">{new Set(selectedGroup.fields.map((field) => field.type)).size}</p>
								<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Toggles, inputs, and segmented choices</p>
							</div>
						</div>
					</div>
				</div>

				<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<div class="flex items-start justify-between gap-3">
						<div>
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Bob rules assist</p>
							<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">{selectedGroup.label}</p>
						</div>
						<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg text-[var(--accent-text)] shadow-sm">
							✨
						</span>
					</div>
					<div class="mt-3 grid gap-2">
						{#each bobMoves as move}
							<a
								href={move.href}
								class={`block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2.5 transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)] ${focusRing}`}
							>
								<p class="text-sm font-semibold text-[var(--text-strong)]">{move.label}</p>
								<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{move.detail}</p>
							</a>
						{/each}
					</div>
				</div>

				<div class="grid gap-3">
					{#each selectedGroup.fields as field}
						<div id={field.id} class="scroll-mt-24 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<div class="flex flex-wrap items-start justify-between gap-3">
								<div>
									<p class="text-sm font-semibold text-[var(--text-strong)]">{field.label}</p>
									<p class="mt-1 max-w-3xl text-xs leading-5 text-[var(--text-muted)]">{field.help}</p>
								</div>
								<span class={`rounded-full border px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] ${typeTone(field.type)}`}>{field.type}</span>
							</div>

							<div class="mt-4 grid gap-3 lg:grid-cols-[minmax(0,0.65fr)_minmax(0,1.35fr)]">
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Current value</p>
									<p class="mt-2 text-lg font-semibold text-[var(--text-strong)]">{field.value}</p>
								</div>

								<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Edit rule</p>
									{#if field.options?.length}
										<select
											class={`mt-3 w-full rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none transition focus:border-[var(--accent-border)] ${focusRing}`}
										>
											{#each field.options as option}
												<option selected={option === field.value}>{option}</option>
											{/each}
										</select>
									{:else}
										<input
											value={field.value}
											class={`mt-3 w-full rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none transition focus:border-[var(--accent-border)] ${focusRing}`}
										/>
									{/if}
								</div>
							</div>
						</div>
					{/each}
				</div>
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
