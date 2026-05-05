<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { buildEstimateViews } from '$lib/mvp-display';
	import { quoteRequestStatusMeta, type QuoteRequest, type QuoteRequestStatus } from '$lib/quote-requests';
	import { formatCurrency, formatDate } from '$lib/utils/format';
	import type { PageProps } from './$types';

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
		locations: EstimateLocation[];
		savedAtUtc: string;
		sentAtUtc?: string;
		sentBy?: string;
		delivery?: EstimateDelivery;
		revisionHistory: unknown[];
	};

	type EstimateDelivery = {
		status: 'sent' | 'approved' | 'changes-requested';
		method: 'review-link';
		reviewUrl: string;
		email: string;
		phone: string;
		sentAtUtc: string;
		approvedAtUtc?: string;
		changesRequestedAtUtc?: string;
		responseNote?: string;
	};

	type EstimateLocation = {
		id: string;
		name: string;
		lengthFeet: number;
		widthFeet: number;
		depthInches: number;
		wastePercent: number;
		numberOfPours: number;
	};

	type CalculatedLocation = EstimateLocation & {
		squareFeet: number;
		cubicYards: number;
		cubicYardsPerPour: number;
		formLinearFeet: number;
		rebarLinearFeet: number;
		materialCost: number;
		laborCost: number;
		estimatedTotal: number;
	};

	type EstimateDefaults = Record<string, number>;

	type EstimatePageData = PageProps['data'] & {
		quoteRequests?: QuoteRequest[];
		estimateDrafts?: Record<string, EstimateDraftRecord>;
		estimateDefaults?: EstimateDefaults;
	};

	type DraftStatus = EstimateDraftRecord['status'];
	type QuoteQueueFilter = 'all' | 'working' | 'sent' | 'won' | 'archived';

	let { data, form }: PageProps = $props();
	const pageData = $derived(data as EstimatePageData);

	const quoteRequests = $derived((pageData.quoteRequests ?? []) as QuoteRequest[]);
	const estimateDrafts = $derived((pageData.estimateDrafts ?? {}) as Record<string, EstimateDraftRecord>);
	const estimateDefaults = $derived((pageData.estimateDefaults ?? {}) as EstimateDefaults);
	const allEstimates = $derived(buildEstimateViews(pageData.estimates, pageData.customers));
	const workingEstimateStatuses: QuoteRequestStatus[] = [
		'qualified',
		'inspection-scheduled',
		'estimate-drafted'
	];
	const estimateQueueStatuses: QuoteRequestStatus[] = [...workingEstimateStatuses, 'estimate-sent', 'won', 'closed'];

	const requestQueue = $derived(
		quoteRequests.filter((request) => estimateQueueStatuses.includes(request.status) || estimateDrafts[request.id])
	);
	const draftCount = $derived(Object.values(estimateDrafts).filter((draft) => draft.status !== 'sent').length);
	const sentDraftCount = $derived(Object.values(estimateDrafts).filter((draft) => draft.status === 'sent').length);

	let selectedRequestId = $state('');
	let quoteSearch = $state('');
	let quoteQueueFilter = $state<QuoteQueueFilter>('all');
	let draftStatus = $state<DraftStatus>('draft');
	let notes = $state('');
	let locations = $state<EstimateLocation[]>([]);
	let locationDrawerOpen = $state(false);
	let editingLocationId = $state<string | null>(null);
	let locationName = $state('Garage foundation');
	let locationLengthFeet = $state(30);
	let locationWidthFeet = $state(24);
	let locationDepthInches = $state(4);
	let locationWastePercent = $state(10);
	let locationNumberOfPours = $state(1);
	let copiedCustomerLink = $state('');

	const isWonRequest = (request: QuoteRequest) =>
		request.status === 'won' || estimateDrafts[request.id]?.delivery?.status === 'approved';
	const isArchivedRequest = (request: QuoteRequest) => request.status === 'closed';
	const isSentRequest = (request: QuoteRequest) =>
		request.status === 'estimate-sent' && !isWonRequest(request) && !isArchivedRequest(request);
	const isWorkingRequest = (request: QuoteRequest) =>
		workingEstimateStatuses.includes(request.status) ||
		estimateDrafts[request.id]?.delivery?.status === 'changes-requested';
	const isActiveQueueRequest = (request: QuoteRequest) => !isWonRequest(request) && !isArchivedRequest(request);
	const quoteQueueFilters = $derived(
		[
			{ value: 'all' as const, label: 'All', count: requestQueue.filter(isActiveQueueRequest).length },
			{ value: 'working' as const, label: 'Working', count: requestQueue.filter(isWorkingRequest).length },
			{ value: 'sent' as const, label: 'Sent', count: requestQueue.filter(isSentRequest).length },
			{ value: 'won' as const, label: 'Won', count: requestQueue.filter(isWonRequest).length },
			{ value: 'archived' as const, label: 'Archived', count: requestQueue.filter(isArchivedRequest).length }
		]
	);
	const filterRequestQueue = (queue: QuoteRequest[], filter: QuoteQueueFilter, query: string) => {
		const search = query.trim().toLowerCase();
		const statusFilteredRequests = queue.filter((request) => {
			switch (filter) {
				case 'working':
					return isWorkingRequest(request);
				case 'sent':
					return isSentRequest(request);
				case 'won':
					return isWonRequest(request);
				case 'archived':
					return isArchivedRequest(request);
				default:
					return isActiveQueueRequest(request);
			}
		});

		return statusFilteredRequests.filter((request) => {
			const haystack = [
				request.customerName,
				request.companyName,
				request.siteName,
				request.serviceType,
				request.need,
				request.status.replaceAll('-', ' '),
				quoteRequestStatusMeta[request.status].label
			]
				.join(' ')
				.toLowerCase();
			return !search || haystack.includes(search);
		});
	};
	const selectedRequest = $derived(
		filterRequestQueue(requestQueue, quoteQueueFilter, quoteSearch).find((request) => request.id === selectedRequestId) ??
			filterRequestQueue(requestQueue, quoteQueueFilter, quoteSearch)[0] ??
			null
	);
	const selectedDraft = $derived(selectedRequest ? estimateDrafts[selectedRequest.id] ?? null : null);
	const customerReviewUrl = $derived(
		selectedDraft?.delivery?.reviewUrl ??
			(selectedRequest ? `/bdr/estimate/${encodeURIComponent(selectedRequest.id)}` : '')
	);
	const copyCustomerReviewLink = async () => {
		if (!customerReviewUrl) return;
		const origin = globalThis.location?.origin ?? '';
		await navigator.clipboard.writeText(`${origin}${customerReviewUrl}`);
		copiedCustomerLink = customerReviewUrl;
	};
	const customerReviewStatus = $derived(
		selectedDraft?.delivery?.status ?? (selectedDraft?.status === 'sent' ? 'sent' : 'not sent')
	);

	const concreteCostPerYard = $derived(estimateDefaults.concreteCostPerYard ?? 165);
	const laborRatePerSquareFoot = $derived(estimateDefaults.laborRatePerSquareFoot ?? 4);
	const rebarUnitCost = $derived(estimateDefaults.rebarUnitCost ?? 1.5);

	const locationOptions = [
		'Garage foundation',
		'Sidewalk',
		'Driveway',
		'Patio',
		'Concrete slab',
		'Steps & stoops',
		'Porch',
		'Pool deck',
		'Decorative concrete',
		'Other'
	];

	const createDefaultLocation = (): EstimateLocation => ({
		id: `location-${Date.now()}`,
		name: 'Garage foundation',
		lengthFeet: 30,
		widthFeet: 24,
		depthInches: 4,
		wastePercent: 10,
		numberOfPours: 1
	});

	const calculateLocation = (location: EstimateLocation): CalculatedLocation => {
		const squareFeet = Math.max(0, location.lengthFeet) * Math.max(0, location.widthFeet);
		const cubicYardsBase = (squareFeet * Math.max(0, location.depthInches)) / 12 / 27;
		const cubicYards = Math.ceil(cubicYardsBase * (1 + Math.max(0, location.wastePercent)) * 10) / 10;
		const cubicYardsPerPour = cubicYards / Math.max(1, location.numberOfPours);
		const formLinearFeet = Math.ceil(4 * Math.sqrt(squareFeet) * 1.1);
		const rebarLinearFeet = Math.ceil(Math.ceil(Math.sqrt(squareFeet)) * Math.sqrt(squareFeet) * 2 * 1.1);
		const materialCost = cubicYards * concreteCostPerYard + rebarLinearFeet * rebarUnitCost;
		const laborCost = squareFeet * laborRatePerSquareFoot;
		return {
			...location,
			squareFeet,
			cubicYards,
			cubicYardsPerPour,
			formLinearFeet,
			rebarLinearFeet,
			materialCost,
			laborCost,
			estimatedTotal: materialCost + laborCost
		};
	};

	const calculatedLocations = $derived(locations.map(calculateLocation));
	const totalSquareFeet = $derived(calculatedLocations.reduce((sum, location) => sum + location.squareFeet, 0));
	const totalCubicYards = $derived(calculatedLocations.reduce((sum, location) => sum + location.cubicYards, 0));
	const totalForms = $derived(calculatedLocations.reduce((sum, location) => sum + location.formLinearFeet, 0));
	const totalRebar = $derived(calculatedLocations.reduce((sum, location) => sum + location.rebarLinearFeet, 0));
	const totalMaterials = $derived(calculatedLocations.reduce((sum, location) => sum + location.materialCost, 0));
	const totalLabor = $derived(calculatedLocations.reduce((sum, location) => sum + location.laborCost, 0));
	const estimatedTotal = $derived(calculatedLocations.reduce((sum, location) => sum + location.estimatedTotal, 0));
	const hasLocations = $derived(locations.length > 0);
	const serializedLocations = $derived(JSON.stringify(locations));

	const serviceSummary = $derived.by(() => {
		if (!selectedRequest) return '';
		return [selectedRequest.serviceType, selectedRequest.need || selectedRequest.message]
			.filter(Boolean)
			.join(' - ');
	});

	const visitFindings = $derived.by(() => {
		if (!selectedRequest) return '';
		const visitEvent = [...selectedRequest.timeline]
			.reverse()
			.find((event) => event.type.startsWith('site-visit') && event.note?.trim());
		return visitEvent?.note?.trim() ?? selectedRequest.siteVisitSchedule?.notes ?? '';
	});

	const scopeLineItems = $derived.by(() => [
		...calculatedLocations.map((location) =>
			`${location.name}: ${location.lengthFeet} ft x ${location.widthFeet} ft x ${location.depthInches} in, ${location.cubicYards.toFixed(1)} CY, ${formatCurrency(location.estimatedTotal)}`
		),
		`${totalCubicYards.toFixed(1)} total CY`,
		`${totalForms.toFixed(0)} LF forms`,
		`${totalRebar.toFixed(0)} LF rebar`,
		`Materials ${formatCurrency(totalMaterials)}`,
		`Labor ${formatCurrency(totalLabor)}`,
		`Estimated total ${formatCurrency(estimatedTotal)}`
	].join('\n'));

	const assumptions = $derived.by(() => [
		`Concrete cost: ${formatCurrency(concreteCostPerYard)} / yard`,
		`Labor: ${formatCurrency(laborRatePerSquareFoot)} / sqft`,
		`Rebar: ${formatCurrency(rebarUnitCost)} / LF`,
		selectedRequest?.requestedTimeline ? `Customer timeline: ${selectedRequest.requestedTimeline}` : ''
	].filter(Boolean).join('\n'));

	const metrics = $derived([
		{ label: 'Quote requests', value: String(requestQueue.length) },
		{ label: 'Locations', value: String(locations.length) },
		{ label: sentDraftCount ? 'Sent estimates' : 'Existing estimates', value: String(sentDraftCount || allEstimates.length) }
	]);

	const results = $derived([
		{ label: 'Area', value: `${totalSquareFeet.toFixed(0)} sqft` },
		{ label: 'Cubic Yards', value: `${totalCubicYards.toFixed(1)} CY` },
		{ label: 'Locations', value: String(locations.length) },
		{ label: 'Rebar', value: `${totalRebar.toFixed(0)} LF` },
		{ label: 'Forms', value: `${totalForms.toFixed(0)} LF` },
		{ label: 'Materials', value: formatCurrency(totalMaterials) },
		{ label: 'Labor', value: formatCurrency(totalLabor) },
		{ label: 'Estimate', value: formatCurrency(estimatedTotal) }
	]);
	const drawerPreview = $derived(
		calculateLocation({
			id: editingLocationId ?? 'preview',
			name: locationName,
			lengthFeet: locationLengthFeet,
			widthFeet: locationWidthFeet,
			depthInches: locationDepthInches,
			wastePercent: locationWastePercent,
			numberOfPours: locationNumberOfPours
		})
	);

	const openLocationDrawer = (location?: EstimateLocation) => {
		editingLocationId = location?.id ?? null;
		locationName = location?.name ?? 'Garage foundation';
		locationLengthFeet = location?.lengthFeet ?? 30;
		locationWidthFeet = location?.widthFeet ?? 24;
		locationDepthInches = location?.depthInches ?? 4;
		locationWastePercent = location?.wastePercent ?? 10;
		locationNumberOfPours = location?.numberOfPours ?? 1;
		locationDrawerOpen = true;
	};

	const saveLocation = () => {
		const nextLocation: EstimateLocation = {
			id: editingLocationId ?? `location-${Date.now()}`,
			name: locationName,
			lengthFeet: locationLengthFeet,
			widthFeet: locationWidthFeet,
			depthInches: locationDepthInches,
			wastePercent: locationWastePercent,
			numberOfPours: Math.max(1, locationNumberOfPours)
		};
		locations = editingLocationId
			? locations.map((location) => (location.id === editingLocationId ? nextLocation : location))
			: [...locations, nextLocation];
		locationDrawerOpen = false;
	};

	const removeLocation = (id: string) => {
		locations = locations.filter((location) => location.id !== id);
	};

	$effect(() => {
		const visibleRequests = filterRequestQueue(requestQueue, quoteQueueFilter, quoteSearch);
		if (visibleRequests.length === 0) {
			selectedRequestId = '';
			return;
		}
		if (!visibleRequests.some((request) => request.id === selectedRequestId)) {
			selectedRequestId = visibleRequests[0].id;
		}
	});

	$effect(() => {
		if (!selectedRequest) return;
		const draft = estimateDrafts[selectedRequest.id];
		draftStatus = draft?.status ?? (selectedRequest.status === 'estimate-sent' ? 'sent' : 'draft');
		notes = draft?.notes ?? selectedRequest.nextAction;
		locations = draft?.locations?.length ? draft.locations : [createDefaultLocation()];
	});
</script>

<AdminWorkspace
	kicker="External Admin / Estimates"
	title="Estimates"
	metrics={metrics}
	focusLabel="Quote request"
	drawerOpen={locationDrawerOpen}
	drawerTitle={editingLocationId ? 'Edit Location' : 'Add Location'}
	closeDrawer={() => (locationDrawerOpen = false)}
>
	{#snippet focus()}
		{@const visibleRequests = filterRequestQueue(requestQueue, quoteQueueFilter, quoteSearch)}
		<div class="space-y-3">
			<label class="grid gap-2">
				<span class="sr-only">Search quote requests</span>
				<input
					bind:value={quoteSearch}
					type="search"
					placeholder="Search quotes"
					class="h-11 rounded-lg border border-[var(--shell-border)] bg-white/85 px-3 text-sm text-[var(--text-strong)] outline-none transition focus:border-[var(--accent-border)]"
				/>
			</label>

			<div class="flex flex-wrap gap-2">
				{#each quoteQueueFilters as filter}
					<button
						type="button"
						class={`rounded-full px-3 py-2 text-xs font-semibold transition ${quoteQueueFilter === filter.value ? 'bg-[var(--accent-solid)] text-white shadow-sm' : 'bg-white/85 text-[var(--text-muted)] shadow-sm hover:bg-white'}`}
						onclick={() => (quoteQueueFilter = filter.value)}
					>
						{filter.label} · {filter.count}
					</button>
				{/each}
			</div>

			{#if visibleRequests.length}
				{#each visibleRequests as request}
					<button
						type="button"
						class={`w-full rounded-lg px-3 py-3 text-left shadow-sm transition ${selectedRequest?.id === request.id ? 'bg-[#fff4ea] ring-1 ring-[rgba(249,115,22,0.32)]' : 'bg-white/80 hover:bg-white'}`}
						onclick={() => (selectedRequestId = request.id)}
					>
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-sm font-semibold text-[var(--text-strong)]">{request.customerName}</p>
								<p class="mt-1 text-xs text-[var(--text-muted)]">{request.siteName}</p>
							</div>
							<span class="rounded-full bg-white/80 px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.12em] text-[var(--accent-text)]">
								{isWonRequest(request) ? 'Won' : quoteRequestStatusMeta[request.status].label}
							</span>
						</div>
						<p class="mt-3 text-xs leading-5 text-[var(--text-muted)]">{request.serviceType}</p>
					</button>
				{/each}
			{:else}
				<div class="rounded-lg bg-white/80 px-4 py-5 text-sm text-[var(--text-muted)] shadow-sm">
					No quote requests match this view.
				</div>
			{/if}
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedRequest}
			<form method="POST" action="?/saveDraft" class="space-y-4">
				<input type="hidden" name="requestId" value={selectedRequest.id} />
				<input type="hidden" name="revisionNumber" value={String(selectedDraft?.revisionNumber ?? 1)} />
				<input type="hidden" name="customerName" value={selectedRequest.customerName} />
				<input type="hidden" name="siteName" value={selectedRequest.siteName} />
				<input type="hidden" name="serviceSummary" value={serviceSummary} />
				<input type="hidden" name="visitFindings" value={visitFindings} />
				<input type="hidden" name="scopeLineItems" value={scopeLineItems} />
				<input type="hidden" name="assumptions" value={assumptions} />
				<input type="hidden" name="locations" value={serializedLocations} />

				<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
					<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div>
							<p class="text-sm font-semibold text-[var(--text-strong)]">{selectedRequest.customerName}</p>
							<h2 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">Create estimate</h2>
							<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{selectedRequest.serviceType} · {selectedRequest.siteName}</p>
						</div>
						<div class="flex flex-wrap gap-2">
							<a href="/bdr/admin/requests" class="rounded-md bg-white px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">Quote</a>
							<a href="/bdr/admin/invoices" class="rounded-md bg-white px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">Invoice</a>
						</div>
					</div>

					{#if form?.draftSaved && form.savedRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">Estimate draft saved.</p>
					{:else if form?.draftSent && form.savedRequestId === selectedRequest.id}
						<div class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
							<p class="font-semibold">Estimate link created. The quote request is waiting on customer approval.</p>
							{#if form.reviewUrl}
								<a class="mt-1 inline-flex font-semibold underline" href={form.reviewUrl}>Open customer review</a>
							{/if}
						</div>
					{:else if form?.draftMessage && form.savedRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-700">{form.draftMessage}</p>
					{/if}
				</div>

				<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
							<div class="flex items-center gap-3">
								<span class="flex h-12 w-12 items-center justify-center rounded-lg bg-white text-xl shadow-sm">📍</span>
								<div>
									<h3 class="text-xl font-semibold text-[var(--text-strong)]">Locations</h3>
									<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedRequest.siteName}</p>
								</div>
							</div>
							<button
								type="button"
								class="rounded-md bg-[var(--accent-solid)] px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:opacity-90"
								onclick={() => openLocationDrawer()}
							>
								+ Add Location
							</button>
						</div>

						{#if calculatedLocations.length}
							<div class="mt-5 grid gap-3">
								{#each calculatedLocations as location}
									<div class="rounded-lg bg-[var(--shell-panel-strong)] p-4 shadow-sm">
										<div class="grid gap-3">
											<div>
												<p class="text-base font-semibold text-[var(--text-strong)]">{location.name}</p>
												<p class="mt-1 text-sm text-[var(--text-muted)]">
													{location.lengthFeet} ft x {location.widthFeet} ft x {location.depthInches} in · {location.numberOfPours} pour{location.numberOfPours === 1 ? '' : 's'}
												</p>
											</div>
											<div class="flex items-baseline justify-between gap-4 rounded-md bg-white/80 px-3 py-2">
												<p class="text-sm text-[var(--text-muted)]">Location total</p>
												<p class="text-xl font-semibold text-[var(--text-strong)]">{formatCurrency(location.estimatedTotal)}</p>
											</div>
										</div>
										<div class="mt-4 grid gap-2 text-sm sm:grid-cols-2">
											<div class="flex items-baseline justify-between gap-3 rounded-md bg-white/80 px-3 py-2">
												<p class="text-xs text-[var(--text-muted)]">Area</p>
												<p class="font-semibold text-[var(--text-strong)]">{location.squareFeet.toFixed(0)} sqft</p>
											</div>
											<div class="flex items-baseline justify-between gap-3 rounded-md bg-white/80 px-3 py-2">
												<p class="text-xs text-[var(--text-muted)]">Cubic Yards</p>
												<p class="font-semibold text-[var(--text-strong)]">{location.cubicYards.toFixed(1)} CY</p>
											</div>
											<div class="flex items-baseline justify-between gap-3 rounded-md bg-white/80 px-3 py-2">
												<p class="text-xs text-[var(--text-muted)]">Forms</p>
												<p class="font-semibold text-[var(--text-strong)]">{location.formLinearFeet.toFixed(0)} LF</p>
											</div>
											<div class="flex items-baseline justify-between gap-3 rounded-md bg-white/80 px-3 py-2">
												<p class="text-xs text-[var(--text-muted)]">Rebar</p>
												<p class="font-semibold text-[var(--text-strong)]">{location.rebarLinearFeet.toFixed(0)} LF</p>
											</div>
										</div>
										<div class="mt-4 grid gap-2 sm:flex sm:flex-wrap">
											<button
												type="button"
												class="rounded-md bg-white px-3 py-2 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel)]"
												onclick={() => openLocationDrawer(location)}
											>
												Edit
											</button>
											<button
												type="button"
												class="rounded-md bg-white px-3 py-2 text-sm font-semibold text-[var(--accent-text)] shadow-sm transition hover:bg-[var(--shell-panel)]"
												onclick={() => removeLocation(location.id)}
											>
												Remove
											</button>
										</div>
									</div>
								{/each}
							</div>
						{:else}
							<div class="mt-5 rounded-lg bg-[var(--shell-panel-strong)] px-4 py-8 text-center">
								<p class="text-sm font-semibold text-[var(--text-strong)]">No locations yet</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">Add the first part of the job to start the estimate.</p>
							</div>
						{/if}
				</div>

				<div class="grid gap-4">
						<div class="rounded-lg bg-emerald-50 p-5 shadow-[var(--shell-shadow)] ring-1 ring-emerald-200">
							<h3 class="text-xl font-semibold text-emerald-900">Results</h3>
							<div class="mt-5 grid gap-2">
								{#each results.slice(0, 7) as result}
									<div class="flex items-baseline justify-between gap-4 rounded-md bg-white/55 px-3 py-2">
										<p class="text-sm text-slate-600">{result.label}</p>
										<p class="text-right text-lg font-semibold text-slate-950">{result.value}</p>
									</div>
								{/each}
							</div>
							<div class="mt-5 border-t border-emerald-300 pt-4">
								<div class="flex flex-col gap-2 sm:flex-row sm:items-baseline sm:justify-between">
									<p class="text-lg text-slate-700">Estimated Total</p>
									<p class="text-3xl font-semibold text-emerald-800">{formatCurrency(estimatedTotal)}</p>
								</div>
							</div>
						</div>

						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<h3 class="text-base font-semibold text-[var(--text-strong)]">Estimate state</h3>
							<label class="mt-4 grid gap-2">
								<span class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Status</span>
								<select bind:value={draftStatus} name="draftStatus" class="h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none">
									<option value="draft">Draft</option>
									<option value="ready-to-send">Ready to Send</option>
									<option value="sent" disabled>Sent</option>
								</select>
							</label>
							<label class="mt-4 grid gap-2">
								<span class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Notes</span>
								<textarea bind:value={notes} name="notes" rows="5" class="rounded-lg border border-[var(--shell-border)] bg-white px-3 py-3 text-sm text-[var(--text-base)] outline-none"></textarea>
							</label>
							<div class="mt-4 space-y-2 text-sm text-[var(--text-muted)]">
								<p>Quote: {quoteRequestStatusMeta[selectedRequest.status].label}</p>
								{#if selectedDraft?.savedAtUtc}
									<p>Saved: {formatDate(selectedDraft.savedAtUtc)}</p>
								{/if}
								{#if selectedDraft?.sentAtUtc}
									<p>Sent: {formatDate(selectedDraft.sentAtUtc)}</p>
								{/if}
							</div>
						</div>

						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
								<div>
									<h3 class="text-base font-semibold text-[var(--text-strong)]">Customer review</h3>
									<p class="mt-1 text-sm text-[var(--text-muted)]">Approval happens from the customer-facing estimate packet.</p>
								</div>
								<span class="w-fit rounded-full bg-[#fff4ea] px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">
									{customerReviewStatus}
								</span>
							</div>
							<div class="mt-4 grid gap-2 text-sm text-[var(--text-muted)]">
								<p>Email: {selectedRequest.email || 'Not captured'}</p>
								<p>Phone: {selectedRequest.phone || 'Not captured'}</p>
								<p class="rounded-md bg-[var(--shell-panel-strong)] px-3 py-2 text-[var(--text-base)]">
									Email/SMS providers are not configured yet. Use the customer packet link for this v1 delivery path.
								</p>
								{#if selectedDraft?.delivery?.approvedAtUtc}
									<p>Approved: {formatDate(selectedDraft.delivery.approvedAtUtc)}</p>
								{:else if selectedDraft?.delivery?.changesRequestedAtUtc}
									<p>Changes requested: {formatDate(selectedDraft.delivery.changesRequestedAtUtc)}</p>
								{/if}
								{#if selectedDraft?.delivery?.responseNote}
									<p class="rounded-md bg-[var(--shell-panel-strong)] px-3 py-2 text-[var(--text-base)]">{selectedDraft.delivery.responseNote}</p>
								{/if}
							</div>
							<a href={customerReviewUrl} class="mt-4 inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)] sm:w-auto">
								Preview customer packet
							</a>
							<button
								type="button"
								class="mt-2 inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)] sm:ml-2 sm:mt-4 sm:w-auto"
								onclick={copyCustomerReviewLink}
							>
								{copiedCustomerLink === customerReviewUrl ? 'Link copied' : 'Copy customer link'}
							</button>
							<button
								type="button"
								class="mt-2 inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-muted)] shadow-sm sm:ml-2 sm:mt-4 sm:w-auto"
								disabled
							>
								Email unavailable
							</button>
							<button
								type="button"
								class="mt-2 inline-flex w-full justify-center rounded-md bg-white px-4 py-3 text-sm font-semibold text-[var(--text-muted)] shadow-sm sm:ml-2 sm:mt-4 sm:w-auto"
								disabled
							>
								SMS unavailable
							</button>
						</div>
				</div>

				<div class="grid gap-3 sm:flex sm:flex-wrap">
						<button
							type="submit"
							class="rounded-md bg-[var(--accent-solid)] px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
							disabled={!hasLocations}
						>
							Save estimate
						</button>
						<button
							type="submit"
							formaction="?/sendDraft"
							class="rounded-md bg-emerald-600 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
							disabled={!hasLocations || draftStatus !== 'ready-to-send'}
						>
							Send estimate
						</button>
						<button
							type="submit"
							formaction="?/createRevision"
							class="rounded-md bg-white px-5 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)] disabled:cursor-not-allowed disabled:opacity-50"
							disabled={!selectedDraft}
						>
							Create revision
						</button>
				</div>
			</form>
		{:else}
			<div class="rounded-lg bg-white/90 p-8 text-center shadow-[var(--shell-shadow)]">
				<h2 class="text-xl font-semibold text-[var(--text-strong)]">No quote request selected</h2>
				<p class="mt-2 text-sm text-[var(--text-muted)]">Move a quote request into the estimate lane before creating pricing.</p>
			</div>
		{/if}
	{/snippet}

	{#snippet drawer()}
		<div class="space-y-4">
			<label class="grid gap-2">
				<span class="text-sm font-semibold text-[var(--text-base)]">Location</span>
				<select bind:value={locationName} class="h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]">
					{#each locationOptions as option}
						<option value={option}>{option}</option>
					{/each}
				</select>
			</label>

			<div class="grid gap-4">
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Length (ft)</span>
					<input bind:value={locationLengthFeet} type="number" step="0.1" min="0" class="h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]" />
				</label>
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Width (ft)</span>
					<input bind:value={locationWidthFeet} type="number" step="0.1" min="0" class="h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]" />
				</label>
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Depth (inches)</span>
					<input bind:value={locationDepthInches} type="number" step="0.5" min="0" class="h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]" />
				</label>
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]">Waste %</span>
					<input bind:value={locationWastePercent} type="number" step="1" min="0" class="h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]" />
				</label>
				<label class="grid gap-2">
					<span class="text-sm font-semibold text-[var(--text-base)]"># of Pours</span>
					<input bind:value={locationNumberOfPours} type="number" step="1" min="1" class="h-12 rounded-lg border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)] outline-none focus:border-[var(--accent-border)]" />
				</label>
			</div>

			<div class="rounded-lg bg-emerald-50 p-4 ring-1 ring-emerald-200">
				<div class="grid gap-2 text-sm">
					<div class="flex items-baseline justify-between gap-3">
						<p class="text-slate-600">Cubic Yards</p>
						<p class="text-lg font-semibold text-slate-950">{drawerPreview.cubicYards.toFixed(1)} CY</p>
					</div>
					<div class="flex items-baseline justify-between gap-3">
						<p class="text-slate-600">Estimate</p>
						<p class="text-lg font-semibold text-emerald-800">{formatCurrency(drawerPreview.estimatedTotal)}</p>
					</div>
				</div>
			</div>

			<div class="flex flex-col gap-2 sm:flex-row">
				<button
					type="button"
					class="rounded-md bg-[var(--accent-solid)] px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:opacity-90"
					onclick={saveLocation}
				>
					{editingLocationId ? 'Save Location' : 'Add Location'}
				</button>
				<button
					type="button"
					class="rounded-md bg-white px-5 py-3 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]"
					onclick={() => (locationDrawerOpen = false)}
				>
					Cancel
				</button>
			</div>
		</div>
	{/snippet}
</AdminWorkspace>
