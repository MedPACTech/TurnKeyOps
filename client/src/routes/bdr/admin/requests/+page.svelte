<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import {
		getQuoteRequestToneClasses,
		quoteRequestStatusMeta,
		quoteRequestStatusOptions,
		type QuoteRequest,
		type QuoteRequestStatus
	} from '$lib/quote-requests';
	import type { ActionData, PageProps } from './$types';

	let { data, form }: { data: PageProps['data']; form: ActionData } = $props();

	const requests = $derived(data.requests);
	const scheduleSiteVisitByRequestId = $derived(data.scheduleSiteVisitByRequestId);
	let selectedRequestId = $state('');
	let laneFilter = $state<'all' | 'new' | 'active' | 'estimate' | 'won'>('all');
	let priorityFilter = $state<'all' | QuoteRequest['priority']>('all');
	let statusFilter = $state<'all' | QuoteRequestStatus>('all');
	let search = $state('');

	$effect(() => {
		if (!selectedRequestId && requests[0]) {
			selectedRequestId = requests[0].id;
		}
	});

	const laneMatches = (request: QuoteRequest) => {
		if (laneFilter === 'new') return request.status === 'new';
		if (laneFilter === 'active') return ['contacted', 'inspection-scheduled'].includes(request.status);
		if (laneFilter === 'estimate') return ['estimate-drafted', 'estimate-sent'].includes(request.status);
		if (laneFilter === 'won') return request.status === 'won';
		return true;
	};

	const filteredRequests = $derived.by(() => {
		const query = search.trim().toLowerCase();

		return requests.filter((request) => {
			const matchesPriority = priorityFilter === 'all' || request.priority === priorityFilter;
			const matchesStatus = statusFilter === 'all' || request.status === statusFilter;
			const haystack = [
				request.customerName,
				request.projectType,
				request.serviceAddress,
				request.message,
				request.intakeSummary,
				request.assignedTo
			]
				.join(' ')
				.toLowerCase();
			const matchesSearch = !query || haystack.includes(query);

			return laneMatches(request) && matchesPriority && matchesStatus && matchesSearch;
		});
	});

	$effect(() => {
		if (!filteredRequests.length) {
			selectedRequestId = '';
			return;
		}

		if (!filteredRequests.some((request) => request.id === selectedRequestId)) {
			selectedRequestId = filteredRequests[0].id;
		}
	});

	const selectedRequest = $derived(
		filteredRequests.find((request) => request.id === selectedRequestId) ?? filteredRequests[0]
	);
	const selectedRequestScheduleHref = $derived(
		selectedRequest ? scheduleSiteVisitByRequestId[selectedRequest.id] ?? data.scheduleSiteVisitBaseHref : data.scheduleSiteVisitBaseHref
	);

	const metrics = $derived([
		{ label: 'Total requests', value: String(data.metrics.total), detail: 'Public-site and office-entered requests in one queue' },
		{ label: 'Needs response', value: String(data.metrics.newCount), detail: 'Fresh messages waiting on first office action' },
		{ label: 'Active work', value: String(data.metrics.activeCount), detail: 'Requests still moving through triage, inspection, or estimate handling' }
	]);

	const formatSubmittedAt = (value: string) =>
		new Date(value).toLocaleString([], {
			month: 'short',
			day: 'numeric',
			hour: 'numeric',
			minute: '2-digit'
		});

	const priorityTone = (priority: QuoteRequest['priority']) => {
		if (priority === 'emergency') return 'border-red-400/30 bg-red-400/10 text-red-200';
		if (priority === 'priority') return 'border-amber-400/30 bg-amber-400/10 text-amber-200';
		return 'border-white/10 bg-white/5 text-slate-200';
	};

	const priorityLabel = (priority: QuoteRequest['priority']) => {
		if (priority === 'emergency') return 'Emergency';
		if (priority === 'priority') return 'Priority';
		return 'Standard';
	};
</script>

<svelte:head>
	<title>BDR Admin · Quotes</title>
</svelte:head>

<AdminWorkspace
	{metrics}
	contextLabel="Queue lanes"
	focusLabel="Request focus"
>
	{#snippet context()}
		<div class="space-y-3">
			{#each [
				{ key: 'all' as const, label: 'All queue', detail: `${requests.length} total requests` },
				{ key: 'new' as const, label: 'New intake', detail: `${requests.filter((request) => request.status === 'new').length} unread-style requests` },
				{ key: 'active' as const, label: 'Working', detail: `${requests.filter((request) => ['contacted', 'inspection-scheduled'].includes(request.status)).length} in contact or inspection motion` },
				{ key: 'estimate' as const, label: 'Estimate desk', detail: `${requests.filter((request) => ['estimate-drafted', 'estimate-sent'].includes(request.status)).length} waiting on quote work` },
				{ key: 'won' as const, label: 'Won / handoff', detail: `${requests.filter((request) => request.status === 'won').length} ready for production follow-through` }
			] as lane}
				<button
					type="button"
					class={`w-full rounded-md border px-3 py-3 text-left transition ${laneFilter === lane.key ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:bg-[var(--shell-panel-strong)]'}`}
					onclick={() => (laneFilter = lane.key)}
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{lane.label}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{lane.detail}</p>
				</button>
			{/each}
		</div>
	{/snippet}

	{#snippet focus()}
		<div class="space-y-4">
			<label class="grid gap-1">
				<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Search requests</span>
				<input
					bind:value={search}
					placeholder="Name, scope, address, owner"
					class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none placeholder:text-[var(--muted)]"
				/>
			</label>

			<div class="grid gap-3">
				<label class="grid gap-1">
					<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Priority</span>
					<select bind:value={priorityFilter} class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none">
						<option value="all">All priorities</option>
						<option value="emergency">Emergency</option>
						<option value="priority">Priority</option>
						<option value="standard">Standard</option>
					</select>
				</label>
				<label class="grid gap-1">
					<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Stage</span>
					<select bind:value={statusFilter} class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none">
						<option value="all">All stages</option>
						{#each quoteRequestStatusOptions as option}
							<option value={option.value}>{option.label}</option>
						{/each}
					</select>
				</label>
			</div>

			<div class="space-y-2">
				{#if filteredRequests.length}
					{#each filteredRequests as request}
						<button
							type="button"
							class={`w-full rounded-xl border p-3 text-left transition ${selectedRequest?.id === request.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)] shadow-[0_14px_34px_rgba(234,88,12,0.10)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel)] hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel-strong)]'}`}
							onclick={() => (selectedRequestId = request.id)}
						>
							<div class="flex items-start justify-between gap-3">
								<div class="min-w-0">
									<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{request.customerName}</p>
									<p class="mt-1 line-clamp-1 text-xs text-[var(--text-muted)]">{request.projectType}</p>
								</div>
								<p class="shrink-0 text-[0.7rem] text-[var(--muted)]">{formatSubmittedAt(request.submittedAtUtc)}</p>
							</div>
							<div class="mt-3 flex flex-wrap gap-2">
								<span class={`rounded-full border px-2 py-1 text-[0.6rem] font-semibold uppercase tracking-[0.14em] ${priorityTone(request.priority)}`}>{priorityLabel(request.priority)}</span>
								<span class={`rounded-full border px-2 py-1 text-[0.6rem] font-semibold uppercase tracking-[0.14em] ${getQuoteRequestToneClasses(quoteRequestStatusMeta[request.status].tone)}`}>{quoteRequestStatusMeta[request.status].label}</span>
							</div>
							<p class="mt-3 line-clamp-2 text-xs leading-5 text-[var(--text-base)]">{request.intakeSummary}</p>
						</button>
					{/each}
				{:else}
					<div class="rounded-xl border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-5 text-center text-sm text-[var(--text-muted)]">
						No requests match this queue view.
					</div>
				{/if}
			</div>
		</div>
	{/snippet}

	{#snippet work()}
		{#if selectedRequest}
			<div class="space-y-4">
				<section class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
					<div class="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
						<div class="max-w-3xl">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Selected quote request</p>
							<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedRequest.customerName}</h4>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{selectedRequest.projectType} · {selectedRequest.propertyType} · {selectedRequest.serviceAddress}</p>
						</div>
						<div class="flex flex-wrap gap-2">
							<span class={`rounded-full border px-3 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.16em] ${priorityTone(selectedRequest.priority)}`}>{priorityLabel(selectedRequest.priority)}</span>
							<span class={`rounded-full border px-3 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.16em] ${getQuoteRequestToneClasses(quoteRequestStatusMeta[selectedRequest.status].tone)}`}>{quoteRequestStatusMeta[selectedRequest.status].label}</span>
						</div>
					</div>

					<div class="mt-5 grid gap-3 lg:grid-cols-3">
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
							<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">What matters</p>
							<p class="mt-2 text-base font-semibold text-[var(--text-strong)]">{selectedRequest.intakeSummary}</p>
						</div>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
							<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Owner</p>
							<p class="mt-2 text-base font-semibold text-[var(--text-strong)]">{selectedRequest.assignedTo}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">Source: {selectedRequest.source}</p>
						</div>
						<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
							<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Submitted</p>
							<p class="mt-2 text-base font-semibold text-[var(--text-strong)]">{formatSubmittedAt(selectedRequest.submittedAtUtc)}</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedRequest.preferredTimeline}</p>
						</div>
					</div>
				</section>

				<section class="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_360px]">
					<div class="space-y-4">
						<div class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Customer message</p>
							<p class="mt-4 text-sm leading-7 text-[var(--text-base)]">{selectedRequest.message}</p>
						</div>

						<form method="POST" action="?/updateRequest" class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
							<input type="hidden" name="id" value={selectedRequest.id} />
							<div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
								<div>
									<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Operator controls</p>
									<h5 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">Work the request</h5>
								</div>
								{#if form?.success}
									<p class="rounded-full border border-emerald-400/30 bg-emerald-400/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-emerald-400">Saved</p>
								{:else if form?.message}
									<p class="rounded-full border border-amber-400/30 bg-amber-400/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-amber-300">{form.message}</p>
								{/if}
							</div>

							<div class="mt-5 grid gap-4 md:grid-cols-2">
								<div class="grid gap-2">
									<label class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]" for="status">Stage</label>
									<select id="status" name="status" class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none">
										{#each quoteRequestStatusOptions as option}
											<option value={option.value} selected={selectedRequest.status === option.value}>{option.label}</option>
										{/each}
									</select>
								</div>
								<div class="grid gap-2">
									<label class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]" for="assignedTo">Owner</label>
									<input id="assignedTo" name="assignedTo" value={selectedRequest.assignedTo} class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</div>
							</div>

							<div class="mt-4 grid gap-2">
								<label class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]" for="nextAction">Next action</label>
								<textarea id="nextAction" name="nextAction" rows="4" class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none">{selectedRequest.nextAction}</textarea>
							</div>

							<div class="mt-5 flex flex-wrap gap-3">
								<button type="submit" class="rounded-xl border border-[var(--accent-border)] bg-[var(--accent-solid)] px-5 py-3 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent-solid-text)] transition hover:opacity-90">Save changes</button>
								<a href={selectedRequestScheduleHref} class="rounded-xl border border-[var(--accent-border)] bg-[var(--accent-soft)] px-5 py-3 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)] transition hover:bg-[var(--shell-panel)]">Schedule site visit</a>
								<a href="/bdr/admin/estimates?role=office-admin" class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-5 py-3 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel)]">Open estimate lane</a>
							</div>
						</form>
					</div>

					<aside class="space-y-4">
						<div class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Contact</p>
							<div class="mt-4 space-y-3">
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
									<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Email</p>
									<p class="mt-2 break-all text-sm text-[var(--text-strong)]">{selectedRequest.email}</p>
								</div>
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
									<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Phone</p>
									<p class="mt-2 text-sm text-[var(--text-strong)]">{selectedRequest.phone}</p>
								</div>
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
									<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Service address</p>
									<p class="mt-2 text-sm leading-6 text-[var(--text-strong)]">{selectedRequest.serviceAddress}</p>
								</div>
							</div>
						</div>

						<div class="rounded-xl border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Action stack</p>
							<div class="mt-4 space-y-3">
								<a href={selectedRequestScheduleHref} class="block rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-4 py-3 text-sm font-semibold text-[var(--accent-text)] transition hover:bg-[var(--shell-panel)]">Schedule site visit</a>
								<p class="rounded-md border border-[var(--accent-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-xs leading-6 text-[var(--text-muted)]">Launches the calendar with this quote request in context so office can book the field visit intentionally, not just jump to a generic calendar screen.</p>
								<a href="/bdr/admin/estimates?role=office-admin" class="block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-sm font-semibold text-[var(--text-strong)] transition hover:bg-[var(--shell-panel)]">Move to estimate lane</a>
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Next action</p>
									<p class="mt-2 text-sm text-[var(--text-strong)]">{selectedRequest.nextAction}</p>
								</div>
								<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Stage meaning</p>
									<p class="mt-2 text-sm text-[var(--text-strong)]">{quoteRequestStatusMeta[selectedRequest.status].detail}</p>
								</div>
							</div>
						</div>
					</aside>
				</section>
			</div>
		{:else}
			<div class="rounded-xl border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
				Pick a request from the queue to open the workspace.
			</div>
		{/if}
	{/snippet}
</AdminWorkspace>
