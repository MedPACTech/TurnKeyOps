<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	let selectedGroupId = $state('');

	const selectedGroup = $derived(data.groups.find((group) => group.id === selectedGroupId) ?? data.groups[0]);

	$effect(() => {
		if (!selectedGroupId && data.groups[0]) {
			selectedGroupId = data.groups[0].id;
		}
	});

	const metrics = $derived([
		{ label: 'Config groups', value: String(data.groups.length), detail: 'Grouped by how operators reason about the admin system' },
		{
			label: 'Visible rules',
			value: String(data.groups.reduce((sum, group) => sum + group.fields.length, 0)),
			detail: 'Estimate defaults, calculation handling, business rules, and operator settings'
		},
		{ label: 'Persistence', value: 'Scaffolded', detail: 'Field structure is ready for future API-backed settings storage' }
	]);

	const typeTone = (type: string) => {
		if (type === 'toggle') return 'border-emerald-300 bg-emerald-50 text-emerald-700';
		if (type === 'percent' || type === 'currency') return 'border-sky-300 bg-sky-50 text-sky-700';
		if (type === 'days' || type === 'hours') return 'border-amber-300 bg-amber-50 text-amber-700';
		return 'border-slate-300 bg-slate-50 text-slate-700';
	};
</script>

<AdminWorkspace
	kicker="Admin / Settings"
	title="Operational rules and calculation controls for the BDR admin system"
	description="This route is intentionally configuration-heavy. Operators can inspect the domains that govern estimates, pricing logic, payment holds, and workflow defaults, then work inside a rule surface that reads like a real admin console."
	{metrics}
	contextLabel="Config summary"
	focusLabel="Config domains"
>
	{#snippet context()}
		<div class="space-y-3">
			{#each data.groups as group}
				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
					<p class="text-sm font-semibold text-[var(--text-strong)]">{group.label}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{group.outcome}</p>
					<p class="mt-2 text-xs uppercase tracking-[0.16em] text-[var(--muted)]">{group.fields.length} rules visible</p>
				</div>
			{/each}
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-2">
			{#each data.groups as group}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${selectedGroup?.id === group.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (selectedGroupId = group.id)}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{group.label}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{group.description}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedGroup}
			<div class="space-y-4">
				<div class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Configuration domain</p>
					<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedGroup.label}</h4>
					<p class="mt-3 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{selectedGroup.description}</p>
					<div class="mt-4 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-sm text-[var(--text-base)]">
						{selectedGroup.outcome}
					</div>
				</div>

				<div class="grid gap-3">
					{#each selectedGroup.fields as field}
						<div class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
							<div class="flex flex-wrap items-start justify-between gap-3">
								<div>
									<p class="text-sm font-semibold text-[var(--text-strong)]">{field.label}</p>
									<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{field.help}</p>
								</div>
								<span class={`rounded-full border px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] ${typeTone(field.type)}`}>{field.type}</span>
							</div>

							<div class="mt-4 grid gap-3 lg:grid-cols-[minmax(0,0.7fr)_minmax(0,1.3fr)]">
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Current value</p>
									<p class="mt-2 text-lg font-semibold text-[var(--text-strong)]">{field.value}</p>
								</div>

								<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Editing surface</p>
									{#if field.options?.length}
										<select class="mt-3 w-full rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none">
											{#each field.options as option}
												<option selected={option === field.value}>{option}</option>
											{/each}
										</select>
									{:else}
										<input
											value={field.value}
											class="mt-3 w-full rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none"
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
