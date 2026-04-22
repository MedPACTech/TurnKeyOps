<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildEstimateViews, getScaffoldBanner } from '$lib/mvp-display';
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	type EstimateView = ReturnType<typeof buildEstimateViews>[number];

	let { data }: PageProps = $props();

	const allEstimates = $derived(
		[...buildEstimateViews(data.estimates, data.customers)].sort((left, right) =>
			String(right.validUntilUtc ?? '').localeCompare(String(left.validUntilUtc ?? ''))
		)
	);
	const totalValue = $derived(allEstimates.reduce((sum, estimate) => sum + estimate.totalAmount, 0));

	let search = $state('');
	let statusFilter = $state('Open');
	let selectedEstimateId = $state('');

	const filterMatches = (estimate: EstimateView) => {
		const status = estimate.status.toLowerCase();
		const readiness = estimate.productionReadiness.toLowerCase();
		const signature = estimate.signatureStatus.toLowerCase();

		if (statusFilter === 'Open') {
			return !status.includes('closed') && !status.includes('won');
		}

		if (statusFilter === 'Ready') {
			return readiness.includes('ready') || signature.includes('signed');
		}

		if (statusFilter === 'Blocked') {
			return readiness.includes('awaiting') || readiness.includes('not ready') || signature.includes('pending');
		}

		if (statusFilter === 'Closed') {
			return status.includes('closed') || status.includes('won');
		}

		return true;
	};

	const filteredEstimates = $derived.by(() =>
		allEstimates.filter((estimate) => {
			const query = search.trim().toLowerCase();
			if (query) {
				const haystack = [
					estimate.estimateNumber,
					estimate.customer?.displayName ?? '',
					estimate.scopeSummary,
					estimate.status
				]
					.join(' ')
					.toLowerCase();

				if (!haystack.includes(query)) return false;
			}

			return filterMatches(estimate);
		})
	);

	const selectedEstimate = $derived.by(() => {
		const current = filteredEstimates.find((estimate) => estimate.id === selectedEstimateId);
		return current ?? filteredEstimates[0] ?? null;
	});

	const metrics = $derived([
		{ label: 'Estimate queue', value: String(allEstimates.length), detail: getScaffoldBanner(data.source) },
		{ label: 'Pipeline value', value: formatCurrency(totalValue), detail: 'Visible value across active and closing estimate work' },
		{
			label: 'Ready to hand off',
			value: String(allEstimates.filter((estimate) => estimate.productionReadiness.toLowerCase().includes('ready')).length),
			detail: 'Estimates closest to schedule lock'
		}
	]);

	$effect(() => {
		if (selectedEstimate && selectedEstimateId !== selectedEstimate.id) {
			selectedEstimateId = selectedEstimate.id;
		}
	});
</script>

<AdminWorkspace
	kicker="Estimates"
	title="Estimate queue with search, workflow filters, and full record detail"
	description="The estimate route now opens directly into the working queue. Operators can search, switch between open and blocked work, and inspect a complete estimate record without leaving the lane."
	{metrics}
	contextLabel="Queue context"
	focusLabel="Estimate list"
>
	{#snippet context()}
		<div class="space-y-3">
			<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
				<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Predefined filters</p>
				<div class="mt-3 grid gap-2">
					{#each ['Open', 'Ready', 'Blocked', 'Closed', 'All'] as option}
						<button
							type="button"
							class={`rounded-md border px-3 py-2 text-left text-sm transition ${statusFilter === option ? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel-strong)] text-[var(--text-base)] hover:bg-[var(--shell-panel)]'}`}
							onclick={() => (statusFilter = option)}
						>
							{option}
						</button>
					{/each}
				</div>
			</div>

			<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
				<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Queue posture</p>
				<div class="mt-3 space-y-2 text-sm text-[var(--text-base)]">
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2">
						{allEstimates.filter((estimate) => filterMatches(estimate)).length} estimate(s) match the active status filter
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2">
						Newest estimate stays at the top of the focus rail
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2">
						Customer packet and internal costing stay on the same record
					</div>
				</div>
			</div>
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-4">
			<label class="grid gap-1">
				<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Search estimates</span>
				<input
					bind:value={search}
					placeholder="Estimate number, customer, scope"
					class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none placeholder:text-[var(--muted)]"
				/>
			</label>

			<div class="space-y-2">
				{#each filteredEstimates as estimate}
					<button
						type="button"
						class={`w-full rounded-md border px-3 py-3 text-left transition ${selectedEstimate?.id === estimate.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
						onclick={() => (selectedEstimateId = estimate.id)}
					>
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-sm font-semibold text-[var(--text-strong)]">{estimate.estimateNumber}</p>
								<p class="mt-1 text-xs text-[var(--text-muted)]">{estimate.customer?.displayName ?? 'Unknown customer'}</p>
							</div>
							<p class="text-xs font-semibold text-[var(--text-base)]">{formatCurrency(estimate.totalAmount)}</p>
						</div>
						<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{estimate.status} · {estimate.productionReadiness}</p>
					</button>
				{/each}
			</div>
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedEstimate}
			<div class="space-y-4">
				<div class="flex flex-wrap items-start justify-between gap-3">
					<div>
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Estimate record</p>
						<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedEstimate.estimateNumber}</h4>
						<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedEstimate.customer?.displayName ?? 'Unknown customer'} · Valid until {formatDate(selectedEstimate.validUntilUtc)}</p>
					</div>
					<div class="text-right">
						<p class="text-xl font-semibold text-[var(--text-strong)]">{formatCurrency(selectedEstimate.totalAmount)}</p>
						<p class="mt-1 text-xs uppercase tracking-[0.18em] text-[var(--muted)]">{selectedEstimate.status}</p>
					</div>
				</div>

				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Scope summary</p>
					<p class="mt-3 text-sm leading-6 text-[var(--text-base)]">{selectedEstimate.scopeSummary}</p>
				</div>

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Contract</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedEstimate.contractStatus}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Signature</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedEstimate.signatureStatus}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Deposit</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedEstimate.depositStatus}</p>
					</div>
				</div>

				<div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]">
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Customer-facing packet</p>
						<div class="mt-3 flex flex-wrap gap-2">
							{#each selectedEstimate.customerFacingSections as section}
								<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-1.5 text-xs text-[var(--text-base)]">{section}</span>
							{/each}
						</div>
					</div>

					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next step</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedEstimate.nextStep}</p>
						<p class="mt-2 text-sm text-[var(--text-muted)]">{selectedEstimate.productionReadiness}</p>
					</div>
				</div>

				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
					<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Internal costing and prep</p>
					<div class="mt-3 grid gap-2 md:grid-cols-2">
						{#each selectedEstimate.internalCosting as detail}
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2 text-sm text-[var(--text-base)]">{detail}</div>
						{/each}
					</div>
				</div>
			</div>
		{:else}
			<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
				No estimates match the current filters.
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
