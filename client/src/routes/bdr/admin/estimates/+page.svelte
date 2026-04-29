<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildEstimateViews, getScaffoldBanner } from '$lib/mvp-display';
	import type { QuoteRequest } from '$lib/quote-requests';
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

	type EstimateView = ReturnType<typeof buildEstimateViews>[number];
	type EstimateDraftRecord = {
		requestId: string;
		revisionNumber: number;
		customerName: string;
		siteName: string;
		serviceSummary: string;
		visitFindings: string;
		scopeLineItems: string[];
		notes: string;
		assumptions: string[];
		status: 'draft' | 'ready-to-send' | 'sent';
		commercialSummary: string;
		savedAtUtc: string;
		sentAtUtc?: string;
		sentBy?: string;
		revisionHistory: EstimateRevisionRecord[];
	};
	type EstimateRevisionRecord = {
		revisionNumber: number;
		customerName: string;
		siteName: string;
		serviceSummary: string;
		visitFindings: string;
		scopeLineItems: string[];
		notes: string;
		assumptions: string[];
		status: EstimateDraftRecord['status'];
		commercialSummary: string;
		savedAtUtc: string;
		sentAtUtc?: string;
		sentBy?: string;
	};
	type EstimateDraftPageData = PageProps['data'] & {
		quoteRequests?: QuoteRequest[];
		estimateDrafts?: Record<string, EstimateDraftRecord>;
	};
	type RevenueDeskState = 'Pending review' | 'Sent' | 'Revision needed' | 'Approved / Deposit';
	type BobMove = {
		label: string;
		detail: string;
		href: string;
	};

	let { data, form }: PageProps = $props();
	const estimatePageData = $derived(data as EstimateDraftPageData);

	const allEstimates = $derived(
		[...buildEstimateViews(estimatePageData.estimates, estimatePageData.customers)].sort((left, right) =>
			String(right.validUntilUtc ?? '').localeCompare(String(left.validUntilUtc ?? ''))
		)
	);
	const quoteRequests = $derived((estimatePageData.quoteRequests ?? []) as QuoteRequest[]);
	const estimateDrafts = $derived((estimatePageData.estimateDrafts ?? {}) as Record<string, EstimateDraftRecord>);
	const totalValue = $derived(allEstimates.reduce((sum, estimate) => sum + estimate.totalAmount, 0));

	let search = $state('');
	let statusFilter = $state('Open');
	let selectedEstimateId = $state('');
	let selectedDraftRequestId = $state('');
	let draftCustomerName = $state('');
	let draftSiteName = $state('');
	let draftServiceSummary = $state('');
	let draftVisitFindings = $state('');
	let draftScopeLineItems = $state('');
	let draftNotes = $state('');
	let draftAssumptions = $state('');
	let draftStatus = $state<'draft' | 'ready-to-send' | 'sent'>('draft');

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
	const draftSourceRequests = $derived(
		quoteRequests.filter(
			(request) =>
				Boolean(request.siteVisitSchedule) ||
				request.status === 'inspection-scheduled' ||
				request.status === 'estimate-drafted' ||
				request.status === 'estimate-sent'
		)
	);
	const selectedDraftRequest = $derived(
		draftSourceRequests.find((request) => request.id === selectedDraftRequestId) ?? draftSourceRequests[0] ?? null
	);

	const metrics = $derived([
		{ label: 'Estimate queue', value: String(allEstimates.length), detail: getScaffoldBanner(estimatePageData.source) },
		{ label: 'Pipeline value', value: formatCurrency(totalValue), detail: 'Visible value across active and closing estimate work' },
		{
			label: 'Ready to hand off',
			value: String(allEstimates.filter((estimate) => estimate.productionReadiness.toLowerCase().includes('ready')).length),
			detail: 'Estimates closest to schedule lock'
		}
	]);
	const getRevenueDeskState = (estimate: EstimateView): RevenueDeskState => {
		const signature = estimate.signatureStatus.toLowerCase();
		const deposit = estimate.depositStatus.toLowerCase();
		const contract = estimate.contractStatus.toLowerCase();
		const readiness = estimate.productionReadiness.toLowerCase();

		if (signature.includes('pending revised') || contract.includes('revision')) return 'Revision needed';
		if (deposit.includes('received') || deposit.includes('expected')) return 'Approved / Deposit';
		if (signature.includes('sent')) return 'Sent';
		if (readiness.includes('awaiting') || signature.includes('pending')) return 'Pending review';
		return 'Pending review';
	};
	const revenueDeskStateTone = (state: RevenueDeskState) => {
		switch (state) {
			case 'Sent':
				return 'border-sky-400/35 bg-sky-400/10 text-sky-300';
			case 'Revision needed':
				return 'border-amber-400/35 bg-amber-400/10 text-amber-700';
			case 'Approved / Deposit':
				return 'border-emerald-400/35 bg-emerald-400/10 text-emerald-300';
			default:
				return 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]';
		}
	};
	const revenueDeskSummary = $derived([
		{
			label: 'Drafts',
			value: String(Object.values(estimateDrafts).filter((draft) => draft.status !== 'sent').length),
			detail: 'Draft and send-ready work'
		},
		{
			label: 'Pending review',
			value: String(allEstimates.filter((estimate) => getRevenueDeskState(estimate) === 'Pending review').length),
			detail: 'Needs office action'
		},
		{
			label: 'Revision needed',
			value: String(allEstimates.filter((estimate) => getRevenueDeskState(estimate) === 'Revision needed').length),
			detail: 'Blocked on update'
		},
		{
			label: 'Approved / Deposit',
			value: String(allEstimates.filter((estimate) => getRevenueDeskState(estimate) === 'Approved / Deposit').length),
			detail: 'Closest to handoff'
		}
	]);

	$effect(() => {
		if (selectedEstimate && selectedEstimateId !== selectedEstimate.id) {
			selectedEstimateId = selectedEstimate.id;
		}
	});

	const buildVisitFindingSeed = (request: QuoteRequest | null) => {
		if (!request) return '';
		const visitEvent = [...request.timeline]
			.reverse()
			.find((event) => event.type.startsWith('site-visit') && event.note?.trim());
		return visitEvent?.note?.trim() ?? request.siteVisitSchedule?.notes ?? '';
	};

	const buildServiceSummarySeed = (request: QuoteRequest | null) =>
		request
			? [request.serviceType, request.need || request.message]
					.filter(Boolean)
					.join(' — ')
			: '';

	const buildDraftTrace = (request: QuoteRequest | null) => {
		if (!request) return [] as { label: string; value: string; source: string }[];
		return [
			{
				label: 'Customer',
				value: request.customerName,
				source: 'Request contact snapshot'
			},
			{
				label: 'Site',
				value: request.siteName,
				source: 'Request site/address fields'
			},
			{
				label: 'Service summary',
				value: request.serviceType,
				source: 'Request service + need'
			},
			{
				label: 'Visit findings',
				value: buildVisitFindingSeed(request) || 'No visit notes captured yet',
				source: request.siteVisitSchedule ? 'Visit schedule note / timeline' : 'No visit record yet'
			}
		];
	};

	const selectedDraftTrace = $derived(buildDraftTrace(selectedDraftRequest));
	const selectedSavedDraft = $derived(
		selectedDraftRequest ? estimateDrafts[selectedDraftRequest.id] ?? null : null
	);
	const selectedRevisionHistory = $derived(
		(selectedSavedDraft?.revisionHistory ?? []).toSorted((left, right) => right.revisionNumber - left.revisionNumber)
	);
	const draftCommercialSummary = $derived.by(() => {
		const scopeCount = draftScopeLineItems
			.split('\n')
			.map((entry) => entry.trim())
			.filter(Boolean).length;
		const assumptionCount = draftAssumptions
			.split('\n')
			.map((entry) => entry.trim())
			.filter(Boolean).length;
		return [
			scopeCount ? `${scopeCount} scope line item(s)` : 'No scope line items yet',
			assumptionCount ? `${assumptionCount} assumption(s)` : 'No assumptions recorded',
			draftStatus === 'sent' ? 'Customer-visible send completed' : 'Review before send'
		].join(' · ');
	});
	const bobMoves = $derived.by(() => {
		if (selectedSavedDraft && draftStatus !== 'sent') {
			return [
				{
					label: draftStatus === 'ready-to-send' ? 'Send estimate' : 'Finish draft review',
					detail: draftCommercialSummary,
					href: '#estimate-draft-desk'
				},
				{
					label: 'Summarize revision history',
					detail: `${selectedRevisionHistory.length} prior revision${selectedRevisionHistory.length === 1 ? '' : 's'}`,
					href: '#estimate-revisions'
				},
				{
					label: 'Open request intake',
					detail: selectedDraftRequest?.customerName ?? 'Return to request workspace',
					href: '/bdr/admin/requests?role=office-admin'
				}
			] satisfies BobMove[];
		}

		if (selectedEstimate) {
			const state = getRevenueDeskState(selectedEstimate);
			return [
				{
					label: state === 'Revision needed' ? 'Prep revision summary' : 'Review next revenue move',
					detail: selectedEstimate.nextStep,
					href: '#estimate-record'
				},
				{
					label: state === 'Approved / Deposit' ? 'Check deposit readiness' : 'Check send posture',
					detail: selectedEstimate.depositStatus,
					href: '#estimate-record'
				},
				{
					label: 'Open request intake',
					detail: selectedEstimate.customer?.displayName ?? 'Estimate source request',
					href: '/bdr/admin/requests?role=office-admin'
				}
			] satisfies BobMove[];
		}

		return [
			{
				label: 'Review revenue desk',
				detail: `${allEstimates.length} estimate${allEstimates.length === 1 ? '' : 's'} in queue`,
				href: '/bdr/admin/estimates?role=office-admin'
			}
		] satisfies BobMove[];
	});

	$effect(() => {
		if (!selectedDraftRequestId && draftSourceRequests[0]) {
			selectedDraftRequestId = draftSourceRequests[0].id;
		}
	});

	$effect(() => {
		const request = selectedDraftRequest;
		if (!request) return;
		const savedDraft = estimateDrafts[request.id];
		draftCustomerName = savedDraft?.customerName ?? request.customerName;
		draftSiteName = savedDraft?.siteName ?? request.siteName;
		draftServiceSummary = savedDraft?.serviceSummary ?? buildServiceSummarySeed(request);
		draftVisitFindings = savedDraft?.visitFindings ?? buildVisitFindingSeed(request);
		draftScopeLineItems = savedDraft?.scopeLineItems.join('\n') ?? '';
		draftNotes = savedDraft?.notes ?? request.nextAction;
		draftStatus = savedDraft?.status ?? 'draft';
		draftAssumptions =
			savedDraft?.assumptions.join('\n') ??
			[
				request.requestedTimeline ? `Customer requested timeline: ${request.requestedTimeline}` : '',
				request.siteVisitSchedule
					? `Visit scheduled for ${request.siteVisitSchedule.visitDate} (${request.siteVisitSchedule.windowStart} - ${request.siteVisitSchedule.windowEnd})`
					: ''
			]
				.filter(Boolean)
				.join('\n');
	});
</script>

<AdminWorkspace
	kicker="External Admin / Estimates"
	title="Focused revenue desk for drafts, reviews, revisions, and approvals"
	description="Keep estimate work centered on state, value, and the next revenue move without burying the queue in form-heavy detail."
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
				<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Revenue states</p>
				<div class="mt-3 grid gap-2">
					{#each revenueDeskSummary as item}
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2.5">
							<div class="flex items-center justify-between gap-3">
								<p class="text-sm font-semibold text-[var(--text-strong)]">{item.label}</p>
								<span class="text-sm font-semibold text-[var(--text-strong)]">{item.value}</span>
							</div>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{item.detail}</p>
						</div>
					{/each}
				</div>
			</div>

			<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
				<div class="flex items-start justify-between gap-3">
					<div>
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Bob revenue assist</p>
						<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">
							{selectedEstimate?.estimateNumber ?? selectedDraftRequest?.customerName ?? 'Revenue queue'}
						</p>
					</div>
					<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg text-[var(--accent-text)] shadow-sm">
						✨
					</span>
				</div>
				<div class="mt-3 space-y-2">
					{#each bobMoves as move}
						<a
							href={move.href}
							class="block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2.5 transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)]"
						>
							<p class="text-sm font-semibold text-[var(--text-strong)]">{move.label}</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{move.detail}</p>
						</a>
					{/each}
				</div>
			</div>
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-4">
			<label class="grid gap-1">
				<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Search revenue desk</span>
				<input
					bind:value={search}
					placeholder="Estimate number, customer, scope"
					class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none placeholder:text-[var(--muted)]"
				/>
			</label>

			<div class="space-y-2">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
					{filteredEstimates.length} estimates
				</p>
				{#each filteredEstimates as estimate}
					{@const state = getRevenueDeskState(estimate)}
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
						<div class="mt-3 flex flex-wrap items-center gap-2">
							<span class={`rounded-md border px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] ${revenueDeskStateTone(state)}`}>
								{state}
							</span>
							<span class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-[var(--text-base)]">
								{estimate.status}
							</span>
						</div>
						<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{estimate.nextStep}</p>
					</button>
				{/each}
			</div>
		</div>
	{/snippet}

	{#snippet work()}
		<div class="space-y-4">
			{#if selectedDraftRequest}
				<form id="estimate-draft-desk" method="POST" action="?/saveDraft" class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
					<input type="hidden" name="requestId" value={selectedDraftRequest.id} />
					<input type="hidden" name="revisionNumber" value={String(selectedSavedDraft?.revisionNumber ?? 1)} />
					<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div class="max-w-3xl">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Draft desk</p>
							<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">Start or revise a customer-ready estimate</h4>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">
								Pull request and visit context forward, then save, revise, or send without rebuilding the estimate by hand.
							</p>
						</div>
						<div class="w-full max-w-sm">
							<label class="grid gap-1">
								<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Source request</span>
								<select bind:value={selectedDraftRequestId} class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none">
									{#each draftSourceRequests as request}
										<option value={request.id}>{request.customerName} · {request.serviceType}</option>
									{/each}
								</select>
							</label>
						</div>
					</div>

					{#if form?.draftSaved && form.savedRequestId === selectedDraftRequest.id}
						<p class="mt-4 rounded-md border border-emerald-400/30 bg-emerald-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-emerald-400">
							Draft saved locally
						</p>
					{:else if form?.revisionCreated && form.savedRequestId === selectedDraftRequest.id}
						<p class="mt-4 rounded-md border border-emerald-400/30 bg-emerald-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-emerald-400">
							Revision v{form.revisionNumber} created
						</p>
					{:else if form?.draftSent && form.savedRequestId === selectedDraftRequest.id}
						<p class="mt-4 rounded-md border border-emerald-400/30 bg-emerald-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-emerald-400">
							Estimate sent
						</p>
					{:else if form?.draftMessage && form.savedRequestId === selectedDraftRequest.id}
						<p class="mt-4 rounded-md border border-amber-400/30 bg-amber-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-amber-700">
							{form.draftMessage}
						</p>
					{/if}

					<div class="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
						{#each selectedDraftTrace as item}
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
								<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">{item.label}</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{item.value}</p>
								<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{item.source}</p>
							</div>
						{/each}
					</div>

					<div class="mt-5 grid gap-4 lg:grid-cols-2">
						<label class="grid gap-2">
							<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Customer</span>
							<input bind:value={draftCustomerName} name="customerName" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
						</label>
						<label class="grid gap-2">
							<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Site</span>
							<input bind:value={draftSiteName} name="siteName" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
						</label>
					</div>

					<div class="mt-4 grid gap-4 xl:grid-cols-2">
						<label class="grid gap-2">
							<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Service summary</span>
							<textarea bind:value={draftServiceSummary} name="serviceSummary" rows="4" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none"></textarea>
						</label>
						<label class="grid gap-2">
							<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Visit findings</span>
							<textarea bind:value={draftVisitFindings} name="visitFindings" rows="4" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none"></textarea>
						</label>
					</div>

					<div class="mt-4 grid gap-4 xl:grid-cols-[minmax(0,1.15fr)_minmax(0,0.85fr)]">
						<label class="grid gap-2">
							<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Scope line items</span>
							<textarea bind:value={draftScopeLineItems} name="scopeLineItems" rows="6" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" placeholder="One line item per line"></textarea>
						</label>
						<div class="grid gap-4">
							<label class="grid gap-2">
								<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Assumptions</span>
								<textarea bind:value={draftAssumptions} name="assumptions" rows="4" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" placeholder="One assumption per line"></textarea>
							</label>
							<label class="grid gap-2">
								<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Internal notes</span>
								<textarea bind:value={draftNotes} name="notes" rows="4" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none"></textarea>
							</label>
						</div>
					</div>

					<div class="mt-4 grid gap-4 xl:grid-cols-[220px_minmax(0,1fr)]">
						<label class="grid gap-2">
							<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Draft status</span>
							<select bind:value={draftStatus} name="draftStatus" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none">
								<option value="draft">Draft</option>
								<option value="ready-to-send">Ready to Send</option>
								<option value="sent" disabled>Sent</option>
							</select>
						</label>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Commercial review</p>
							<div class="mt-2 flex flex-wrap items-center gap-2">
								<p class="text-sm font-semibold text-[var(--text-strong)]">Revision v{selectedSavedDraft?.revisionNumber ?? 1}</p>
								<span class="rounded-full border border-emerald-400/30 bg-emerald-400/10 px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] text-emerald-400">
									Latest version
								</span>
							</div>
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{draftCommercialSummary}</p>
							{#if selectedSavedDraft?.sentAtUtc}
								<p class="mt-3 text-xs text-[var(--text-muted)]">
									Sent by {selectedSavedDraft.sentBy} on {formatDate(selectedSavedDraft.sentAtUtc)}.
								</p>
							{/if}
						</div>
					</div>

					<div class="mt-5 flex flex-wrap gap-3">
						<button type="submit" class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-solid)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-solid-text)] transition hover:opacity-90">Save draft</button>
						<button
							type="submit"
							formaction="?/createRevision"
							class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel)] disabled:cursor-not-allowed disabled:opacity-50"
							disabled={!selectedSavedDraft}
						>
							Create revision
						</button>
						<button
							type="submit"
							formaction="?/sendDraft"
							class="rounded-md border border-emerald-400/35 bg-emerald-500 px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
							disabled={draftStatus !== 'ready-to-send'}
						>
							Send estimate
						</button>
						<a href="/bdr/admin/requests?role=office-admin" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel)]">Open request workspace</a>
						{#if estimateDrafts[selectedDraftRequest.id]?.savedAtUtc}
							<p class="text-xs leading-5 text-[var(--text-muted)]">Last saved {formatDate(estimateDrafts[selectedDraftRequest.id].savedAtUtc)}</p>
						{/if}
					</div>

					<div id="estimate-revisions" class="mt-5 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
						<div class="flex flex-wrap items-center justify-between gap-3">
							<div>
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Revision history</p>
							</div>
							<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">
								Current revision · v{selectedSavedDraft?.revisionNumber ?? 1}
							</p>
						</div>

						{#if selectedRevisionHistory.length}
							<div class="mt-4 grid gap-3 xl:grid-cols-2">
								{#each selectedRevisionHistory as revision}
									<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
										<div class="flex items-start justify-between gap-3">
											<div>
												<p class="text-sm font-semibold text-[var(--text-strong)]">Revision v{revision.revisionNumber}</p>
												<p class="mt-1 text-xs text-[var(--text-muted)]">
													Saved {formatDate(revision.savedAtUtc)}
													{#if revision.sentAtUtc}
														· Sent {formatDate(revision.sentAtUtc)}
													{/if}
												</p>
											</div>
											<span class="rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2.5 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.16em] text-[var(--text-base)]">
												{revision.status.replaceAll('-', ' ')}
											</span>
										</div>
										<p class="mt-3 text-sm font-semibold text-[var(--text-strong)]">{revision.commercialSummary}</p>
										<div class="mt-3 grid gap-2 text-xs text-[var(--text-muted)] md:grid-cols-2">
											<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2">
												{revision.scopeLineItems.length} scope line item(s)
											</div>
											<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2">
												{revision.assumptions.length} assumption(s)
											</div>
										</div>
										{#if revision.notes}
											<p class="mt-3 text-sm leading-6 text-[var(--text-muted)]">{revision.notes}</p>
										{/if}
									</div>
								{/each}
							</div>
						{:else}
							<div class="mt-4 rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-5 text-sm text-[var(--text-muted)]">
								No prior revisions yet. Save the estimate, then create a revision when internal or customer feedback requires a controlled update.
							</div>
						{/if}
					</div>
				</form>
			{/if}

			{#if selectedEstimate}
				<div id="estimate-record" class="space-y-4">
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

				<div class="grid gap-3 md:grid-cols-3">
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Revenue state</p>
						<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{getRevenueDeskState(selectedEstimate)}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Next move</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedEstimate.nextStep}</p>
					</div>
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
						<p class="text-xs uppercase tracking-[0.18em] text-[var(--muted)]">Production posture</p>
						<p class="mt-2 text-sm text-[var(--text-base)]">{selectedEstimate.productionReadiness}</p>
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
				No estimates match this view. Clear search or switch filters.
			</div>
			{/if}
		</div>
	{/snippet}
</AdminWorkspace>
