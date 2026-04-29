<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import {
		buildQuoteRequestQualification,
		buildQuoteRequestWorkflowModel,
		getQuoteRequestWorkflowLane,
		getQuoteRequestWorkflowPhase,
		isQuoteRequestUnassigned,
		quoteRequestMissingInfoReasonOptions,
		quoteRequestStatusMeta,
		quoteRequestStatusOptions,
		quoteRequestWorkflowLaneMeta,
		type QuoteRequest,
		type QuoteRequestMissingInfoReasonCode,
		type QuoteRequestStatus,
		type QuoteRequestWorkflowLane
	} from '$lib/quote-requests';
	import { AlertTriangle, CalendarCheck, CheckCircle2, ExternalLink, FileText, Lock, Pencil } from 'lucide-svelte';
	import type { ActionData, PageProps } from './$types';

	let { data, form }: { data: PageProps['data']; form: ActionData } = $props();

	const requests = $derived(data.requests);
	const scheduleSiteVisitByRequestId = $derived(data.scheduleSiteVisitByRequestId);
	let selectedRequestId = $state('');
	let laneFilter = $state<'all' | QuoteRequestWorkflowLane>('all');
	let search = $state('');
	let selectedAttachmentId = $state('');
	let triageStatus = $state<QuoteRequestStatus>('new');
	let scheduleVisitDate = $state('');
	let scheduleWindowStart = $state('09:00');
	let scheduleWindowEnd = $state('10:30');
	let scheduleSiteContact = $state('');
	let scheduleSiteContactPhone = $state('');
	let scheduleAssignedFieldResource = $state('');
	let scheduleNotes = $state('');
	let detailInlineEditing = $state(false);
	let contactInlineEditing = $state(false);
	let detailRequestedTimeline = $state('');
	let detailNextAction = $state('');
	let contactNameDraft = $state('');
	let contactEmailDraft = $state('');
	let contactPhoneDraft = $state('');
	let contactSiteNameDraft = $state('');
	let contactAddress1Draft = $state('');
	let contactAddress2Draft = $state('');
	let contactCityDraft = $state('');
	let contactStateDraft = $state('');
	let contactPostalCodeDraft = $state('');

	const fieldResourceSuggestions = ['Estimator - Maya', 'Estimator - Chris', 'Estimator - Lane', 'Ella - office admin', 'Estimator - Jordan'];

	$effect(() => {
		if (!selectedRequestId && requests[0]) {
			selectedRequestId = requests[0].id;
		}
	});

	const laneMatches = (request: QuoteRequest) => {
		if (laneFilter === 'all') return true;
		return getQuoteRequestWorkflowLane(request.status) === laneFilter;
	};

	const filteredRequests = $derived.by(() => {
		const query = search.trim().toLowerCase();

		return requests.filter((request) => {
			const qualification = buildQuoteRequestQualification(request);
			const haystack = [
				quoteRequestStatusMeta[request.status].label,
				request.companyName,
				request.contactName,
				request.customerName,
				request.email,
				request.phone,
				request.siteName,
				request.serviceType,
				request.projectType,
				request.serviceAddress,
				request.propertyType,
				request.requestedTimeline,
				request.preferredTimeline,
				request.priority,
				request.need,
				request.message,
				request.intakeSummary,
				request.assignedTo,
				request.nextAction,
				...qualification.blockerLabels,
				request.source
			]
				.join(' ')
				.toLowerCase();
			const matchesSearch = !query || haystack.includes(query);

			return laneMatches(request) && matchesSearch;
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
	const selectedAttachment = $derived(
		selectedRequest?.attachments.find((attachment) => attachment.id === selectedAttachmentId) ?? null
	);
	const selectedAttachmentUrl = $derived(
		selectedRequest && selectedAttachment
			? `/bdr/admin/requests/attachments/${encodeURIComponent(selectedRequest.id)}/${encodeURIComponent(selectedAttachment.id)}`
			: ''
	);
	const selectedAttachmentCanPreview = $derived(
		Boolean(
			selectedAttachment &&
				(selectedAttachment.contentType.startsWith('image/') ||
					selectedAttachment.contentType === 'application/pdf' ||
					selectedAttachment.contentType.startsWith('text/'))
		)
	);
	const selectedQualification = $derived(selectedRequest ? buildQuoteRequestQualification(selectedRequest) : null);
	const selectedWorkflow = $derived(selectedRequest ? buildQuoteRequestWorkflowModel(selectedRequest.status) : []);
	const selectedWorkflowPhase = $derived(selectedRequest ? getQuoteRequestWorkflowPhase(selectedRequest.status) : null);
	const selectedRequestScheduleSectionId = $derived(
		selectedRequest ? `schedule-site-visit-${selectedRequest.id}` : 'schedule-site-visit'
	);
	const selectedRequestCanOpenScheduler = $derived(
		Boolean(
			selectedRequest &&
				(selectedQualification?.isQualified || selectedRequest.status === 'inspection-scheduled' || selectedRequest.siteVisitSchedule)
		)
	);

	$effect(() => {
		if (!selectedRequest?.attachments.some((attachment) => attachment.id === selectedAttachmentId)) {
			selectedAttachmentId = '';
		}
	});

	$effect(() => {
		if (selectedRequest) {
			triageStatus = selectedRequest.status;
		}
	});

	const resetDetailDraft = (request: QuoteRequest | null | undefined) => {
		detailRequestedTimeline = request?.requestedTimeline ?? '';
		detailNextAction = request?.nextAction ?? '';
		detailInlineEditing = false;
	};

	const resetContactDraft = (request: QuoteRequest | null | undefined) => {
		const address = parseAddress(request?.serviceAddress ?? '');
		contactNameDraft = request?.contactName ?? '';
		contactEmailDraft = request?.email ?? '';
		contactPhoneDraft = request?.phone ?? '';
		contactSiteNameDraft = request?.siteName ?? '';
		contactAddress1Draft = address.address1;
		contactAddress2Draft = address.address2;
		contactCityDraft = address.city;
		contactStateDraft = address.state;
		contactPostalCodeDraft = address.postalCode;
		contactInlineEditing = false;
	};

	const defaultScheduleDate = () => {
		const value = new Date();
		value.setDate(value.getDate() + 1);
		return value.toISOString().slice(0, 10);
	};

	$effect(() => {
		if (!selectedRequest) return;
		const existingSchedule = selectedRequest.siteVisitSchedule;
		scheduleVisitDate = existingSchedule?.visitDate ?? defaultScheduleDate();
		scheduleWindowStart = existingSchedule?.windowStart ?? '09:00';
		scheduleWindowEnd = existingSchedule?.windowEnd ?? '10:30';
		scheduleSiteContact = existingSchedule?.siteContact ?? selectedRequest.contactName;
		scheduleSiteContactPhone = existingSchedule?.siteContactPhone ?? selectedRequest.phone;
		scheduleAssignedFieldResource = existingSchedule?.assignedFieldResource ?? (isQuoteRequestUnassigned(selectedRequest) ? '' : selectedRequest.assignedTo);
		scheduleNotes = existingSchedule?.notes ?? '';
	});

	const metrics = $derived([
		{ label: 'Total requests', value: String(data.metrics.total), detail: 'Public-site and office-entered requests in one queue' },
		{ label: 'Needs response', value: String(data.metrics.newCount), detail: 'Fresh messages waiting on first office action' },
		{ label: 'Active work', value: String(data.metrics.activeCount), detail: 'Requests still moving through qualification or site-visit handling' }
	]);

	const formatSubmittedAt = (value: string) =>
		new Date(value).toLocaleString([], {
			month: 'short',
			day: 'numeric',
			hour: 'numeric',
			minute: '2-digit'
		});

	const formatScheduleDate = (value: string) =>
		new Date(`${value}T12:00:00`).toLocaleDateString([], {
			month: 'short',
			day: 'numeric',
			year: 'numeric'
		});

	const formatScheduleTime = (value: string) => {
		const [hoursText = '0', minutesText = '0'] = value.split(':');
		const hours = Number(hoursText);
		const minutes = Number(minutesText);
		if (Number.isNaN(hours) || Number.isNaN(minutes)) return value;
		return new Date(2026, 0, 1, hours, minutes).toLocaleTimeString([], {
			hour: 'numeric',
			minute: '2-digit'
		});
	};

	const formatScheduleWindow = (windowStart: string, windowEnd: string) =>
		`${formatScheduleTime(windowStart)} – ${formatScheduleTime(windowEnd)}`;

	const quoteCardStateClass = (request: QuoteRequest, isSelected: boolean) => {
		const stateClass = request.status === 'new' ? 'border-t-4 border-t-emerald-400' : 'border-t border-t-[var(--shell-border)]';
		const selectionClass = isSelected
			? 'border-x-[var(--accent-border)] border-b-[var(--accent-border)] bg-[var(--accent-soft)]'
			: 'border-x-[var(--shell-border)] border-b-[var(--shell-border)] bg-[var(--shell-panel)] hover:border-x-[var(--accent-border)] hover:border-b-[var(--accent-border)] hover:bg-[var(--shell-panel-strong)]';
		return `${stateClass} ${selectionClass}`;
	};

	const parseAddress = (value: string) => {
		const parts = value.split(',').map((part) => part.trim()).filter(Boolean);
		const [address1 = '', ...rest] = parts;
		const maybeStateZip = rest.at(-1) ?? '';
		const maybeCity = rest.length > 1 ? (rest.at(-2) ?? '') : '';
		const address2 = rest.length > 2 ? rest.slice(0, -2).join(', ') : '';
		const [state = '', ...zipParts] = maybeStateZip.split(/\s+/).filter(Boolean);
		return {
			address1,
			address2,
			city: maybeCity,
			state,
			postalCode: zipParts.join(' ')
		};
	};

	const selectedAddress = $derived(parseAddress(selectedRequest?.serviceAddress ?? ''));
	const selectedWorkflowLane = $derived(selectedRequest ? getQuoteRequestWorkflowLane(selectedRequest.status) : null);
	const selectedOpsSnapshot = $derived.by(() => {
		if (!selectedRequest) return [] as { label: string; value: string; detail: string }[];
		return [
			{ label: 'Workflow lane', value: selectedWorkflowPhase?.label ?? 'Workflow', detail: selectedWorkflowLane ? quoteRequestWorkflowLaneMeta.find((lane) => lane.key === selectedWorkflowLane)?.label ?? 'Lane' : 'Lane' },
			{ label: 'Source', value: selectedRequest.source === 'public-site' ? 'Public site' : selectedRequest.source === 'office' ? 'Office entered' : 'Referral', detail: selectedRequest.priority === 'emergency' ? 'Emergency priority' : `${selectedRequest.attachments.length} attachment${selectedRequest.attachments.length === 1 ? '' : 's'}` },
			{ label: 'Property', value: selectedRequest.propertyType, detail: selectedRequest.projectType },
			{ label: 'Requested timeline', value: selectedRequest.requestedTimeline, detail: selectedRequest.preferredTimeline }
		];
	});

	$effect(() => {
		resetDetailDraft(selectedRequest);
		resetContactDraft(selectedRequest);
	});
	const isMissingInfoReasonChecked = (code: QuoteRequestMissingInfoReasonCode) =>
		Boolean(
			selectedQualification?.suggestedMissingInfoReasonCodes.includes(code) ||
				selectedRequest?.qualification.missingInfoReasonCodes.includes(code)
		);

	const formatAttachmentSize = (sizeBytes: number) => {
		if (sizeBytes >= 1024 * 1024) return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
		if (sizeBytes >= 1024) return `${Math.round(sizeBytes / 1024)} KB`;
		return `${sizeBytes} B`;
	};

	const formatAttachmentName = (fileName: string) => {
		if (fileName.length <= 36) return fileName;

		const extensionIndex = fileName.lastIndexOf('.');
		const extension = extensionIndex > 0 ? fileName.slice(extensionIndex) : '';
		const baseName = extension ? fileName.slice(0, extensionIndex) : fileName;

		return `${baseName.slice(0, 24)}...${extension}`;
	};

	const isImageAttachment = (attachment: QuoteRequest['attachments'][number]) =>
		attachment.contentType.startsWith('image/');

	const viewAttachment = (attachment: QuoteRequest['attachments'][number]) => {
		selectedAttachmentId = attachment.id;
	};

	const closeAttachmentPreview = () => {
		selectedAttachmentId = '';
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
			{#each [{ key: 'all' as const, label: 'All queue', detail: `${requests.length} total requests` }, ...quoteRequestWorkflowLaneMeta.map((lane) => ({
				key: lane.key,
				label: lane.label,
				detail: `${requests.filter((request) => getQuoteRequestWorkflowLane(request.status) === lane.key).length} ${lane.detail.toLowerCase()}`
			}))] as lane}
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

			<div class="space-y-2">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">
					{filteredRequests.length} matching quotes
				</p>
				{#if filteredRequests.length}
					{#each filteredRequests as request}
						{@const qualification = buildQuoteRequestQualification(request)}
						<button
							type="button"
							class={`w-full rounded-md border px-3 py-3 text-left transition ${quoteCardStateClass(request, selectedRequest?.id === request.id)}`}
							onclick={() => (selectedRequestId = request.id)}
						>
							<div class="flex items-start justify-between gap-3">
								<div class="min-w-0">
									<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{request.customerName}</p>
									<p class="mt-1 line-clamp-1 text-xs text-[var(--text-muted)]">{request.siteName} · {request.serviceType}</p>
								</div>
								<p class="shrink-0 text-[0.68rem] text-[var(--muted)]">{formatSubmittedAt(request.submittedAtUtc)}</p>
							</div>
							<div class="mt-3 flex flex-wrap items-center gap-2">
								<span class={`rounded-md border px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] ${selectedRequest?.id === request.id ? 'border-[var(--accent-border)] bg-[var(--shell-panel)] text-[var(--accent-text)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel-strong)] text-[var(--text-muted)]'}`}>
									{quoteRequestStatusMeta[request.status].label}
								</span>
								{#if qualification.blockerLabels.length}
									<span class="inline-flex min-w-0 items-center gap-1 rounded-md border border-amber-400/35 bg-amber-400/10 px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.12em] text-amber-700">
										<AlertTriangle size={12} />
										<span class="truncate">{qualification.blockerLabels.slice(0, 2).join(' · ')}</span>
									</span>
								{:else}
									<span class="inline-flex items-center gap-1 rounded-md border border-emerald-400/30 bg-emerald-400/10 px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.12em] text-emerald-300">
										<CheckCircle2 size={12} />
										Qualified inputs
									</span>
								{/if}
							</div>
						</button>
					{/each}
				{:else}
					<div class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-4 text-center text-sm text-[var(--text-muted)]">
						No quotes match this queue and search.
					</div>
				{/if}
			</div>
		</div>
	{/snippet}

	{#snippet work()}
		<div class="space-y-4">
			{#if selectedRequest}
				<form id={`request-triage-${selectedRequest.id}`} method="POST" action="?/updateRequest" class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
					<input type="hidden" name="id" value={selectedRequest.id} />
					<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div>
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Triage</p>
							<h4 class="mt-1 text-2xl font-semibold text-[var(--text-strong)]">{selectedRequest.companyName}</h4>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{selectedRequest.contactName} · {selectedRequest.serviceType} · {selectedRequest.siteName}</p>
						</div>
						<div class="text-left lg:text-right">
							<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Submitted</p>
							<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">{formatSubmittedAt(selectedRequest.submittedAtUtc)}</p>
						</div>
					</div>

					{#if form?.success && form.updatedRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md border border-emerald-400/30 bg-emerald-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-emerald-400">Saved</p>
					{:else if form?.message && form.updatedRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md border border-amber-400/30 bg-amber-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-amber-700">{form.message}</p>
					{/if}

					<div class="mt-5 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
						<div class="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
							<div>
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Workflow status model</p>
								<p class="mt-1 text-sm font-semibold text-[var(--text-strong)]">{selectedWorkflowPhase?.label ?? 'Workflow'}</p>
								<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{selectedWorkflowPhase?.detail ?? 'Track the request from intake through outcome without losing context.'}</p>
							</div>
							<span class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">
								{quoteRequestStatusMeta[selectedRequest.status].label}
							</span>
						</div>
						<div class="mt-4 grid gap-3 lg:grid-cols-5">
							{#each selectedWorkflow as phase}
								<div class={`rounded-md border px-3 py-3 ${phase.isCurrent ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : phase.isComplete ? 'border-emerald-400/25 bg-emerald-400/10' : 'border-[var(--shell-border)] bg-[var(--shell-panel)]'}`}>
									<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">{phase.label}</p>
									<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{phase.statuses.map((status) => quoteRequestStatusMeta[status].label).join(' · ')}</p>
									<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{phase.detail}</p>
								</div>
							{/each}
						</div>
					</div>

					<div class="mt-5 grid gap-4 lg:grid-cols-[180px_220px_minmax(0,1fr)]">
						<div class="grid gap-2">
							<label class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]" for="status">Stage</label>
							<select id="status" name="status" bind:value={triageStatus} class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none">
								{#each quoteRequestStatusOptions as option}
									<option value={option.value}>{option.label}</option>
								{/each}
							</select>
						</div>
						<div class="grid gap-2">
							<label class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]" for="assignedTo">Owner</label>
							<input id="assignedTo" name="assignedTo" value={selectedRequest.assignedTo} class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
						</div>
						<div class="grid gap-2">
							<label class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]" for="nextAction">Next action</label>
							<textarea id="nextAction" name="nextAction" rows="3" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none">{selectedRequest.nextAction}</textarea>
						</div>
					</div>

					{#if triageStatus === 'needs-info'}
						<div class="mt-5 rounded-md border border-amber-400/30 bg-amber-400/10 p-4">
							<div class="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
								<div>
									<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-amber-700">Needs Info reason codes</p>
									<p class="mt-1 text-sm leading-6 text-amber-800">Select the structured blockers the customer must resolve before qualification can finish.</p>
								</div>
								<span class="rounded-md border border-amber-300/30 bg-white/60 px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] text-amber-800">Required</span>
							</div>
							<div class="mt-4 grid gap-2 lg:grid-cols-2">
								{#each quoteRequestMissingInfoReasonOptions as reason}
									<label class="flex gap-3 rounded-md border border-amber-300/20 bg-white/60 p-3 text-left">
										<input
											type="checkbox"
											name="missingInfoReasonCodes"
											value={reason.value}
											checked={isMissingInfoReasonChecked(reason.value)}
											class="mt-1 h-4 w-4 rounded border-amber-200 bg-[var(--shell-panel)] text-[var(--accent-solid)]"
										/>
										<span>
											<span class="block text-sm font-semibold text-amber-800">{reason.label}</span>
											<span class="mt-1 block text-xs leading-5 text-amber-800">{reason.detail}</span>
										</span>
									</label>
								{/each}
							</div>
						</div>
					{:else if selectedQualification?.blockerLabels.length}
						<div class="mt-5 rounded-md border border-amber-400/25 bg-amber-400/10 px-4 py-3 text-sm leading-6 text-amber-800">
							<span class="font-semibold">Blocking inputs:</span> {selectedQualification.blockerLabels.join(' · ')}. Move the request to Needs Info to store customer-facing reason codes.
						</div>
					{/if}

					<div class="mt-5 flex flex-wrap gap-3">
						<button type="submit" class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-solid)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-solid-text)] transition hover:opacity-90">Save changes</button>
						{#if selectedRequestCanOpenScheduler}
							<a href={`#${selectedRequestScheduleSectionId}`} class="inline-flex items-center gap-2 rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-text)] transition hover:bg-[var(--shell-panel)]">
								<CalendarCheck size={15} />
								{selectedRequest.siteVisitSchedule ? 'Review site visit' : 'Open site visit workspace'}
							</a>
						{:else}
							<span class="inline-flex items-center gap-2 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]" title="Mark the request Qualified after clearing qualification blockers to schedule a site visit.">
								<Lock size={15} />
								Qualify before scheduling
							</span>
						{/if}
						<a href="/bdr/admin/estimates?role=office-admin" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel)]">Open estimate lane</a>
					</div>
				</form>

				<section id={selectedRequestScheduleSectionId} class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
					<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div class="max-w-3xl">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Site visit scheduling workspace</p>
							<h5 class="mt-1 text-xl font-semibold text-[var(--text-strong)]">Book the field handoff without leaving the quote request detail flow</h5>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">
								Propose the visit date, time window, site contact, and assigned field resource here. Saving this workspace moves the request to Site Visit Scheduled and writes the confirmation into the activity timeline.
							</p>
						</div>
						{#if selectedRequest.siteVisitSchedule}
							<span class="inline-flex items-center gap-2 rounded-md border border-emerald-400/35 bg-emerald-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.14em] text-emerald-300">
								<CheckCircle2 size={14} />
								Scheduled
							</span>
						{:else if selectedRequestCanOpenScheduler}
							<span class="inline-flex items-center gap-2 rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-2 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]">
								<CalendarCheck size={14} />
								Ready to book
							</span>
						{:else}
							<span class="inline-flex items-center gap-2 rounded-md border border-amber-400/30 bg-amber-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.14em] text-amber-700">
								<AlertTriangle size={14} />
								Blocked
							</span>
						{/if}
					</div>

					{#if form?.scheduleSuccess && form.scheduledRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md border border-emerald-400/30 bg-emerald-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-emerald-400">Site visit scheduled</p>
					{:else if form?.scheduleMessage && form.scheduledRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md border border-amber-400/30 bg-amber-400/10 px-3 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-amber-700">{form.scheduleMessage}</p>
					{/if}

					{#if selectedRequest.siteVisitSchedule}
						<div class="mt-4 grid gap-3 lg:grid-cols-3">
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
								<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Current visit window</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatScheduleDate(selectedRequest.siteVisitSchedule.visitDate)}</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">{formatScheduleWindow(selectedRequest.siteVisitSchedule.windowStart, selectedRequest.siteVisitSchedule.windowEnd)}</p>
							</div>
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
								<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Site contact</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedRequest.siteVisitSchedule.siteContact}</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedRequest.siteVisitSchedule.siteContactPhone || 'Phone not captured'}</p>
							</div>
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
								<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Assigned field resource</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedRequest.siteVisitSchedule.assignedFieldResource}</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">Scheduled by {selectedRequest.siteVisitSchedule.scheduledBy}</p>
							</div>
						</div>
					{/if}

					{#if selectedRequestCanOpenScheduler}
						<form method="POST" action="?/scheduleSiteVisit" class="mt-5 space-y-4">
							<input type="hidden" name="id" value={selectedRequest.id} />
							<div class="grid gap-4 lg:grid-cols-[160px_150px_150px_minmax(0,1fr)]">
								<label class="grid gap-2">
									<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Visit date</span>
									<input bind:value={scheduleVisitDate} type="date" name="visitDate" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Window start</span>
									<input bind:value={scheduleWindowStart} type="time" name="windowStart" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Window end</span>
									<input bind:value={scheduleWindowEnd} type="time" name="windowEnd" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Assigned field resource</span>
									<input bind:value={scheduleAssignedFieldResource} list="field-resource-options" name="assignedFieldResource" placeholder="Estimator or crew" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
							</div>
							<div class="grid gap-4 lg:grid-cols-2">
								<label class="grid gap-2">
									<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Site contact</span>
									<input bind:value={scheduleSiteContact} name="siteContact" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Site contact phone</span>
									<input bind:value={scheduleSiteContactPhone} name="siteContactPhone" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
							</div>
							<label class="grid gap-2">
								<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Field notes</span>
								<textarea bind:value={scheduleNotes} name="notes" rows="3" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" placeholder="Access instructions, expected scope, ladder notes, parking details"></textarea>
							</label>
							<datalist id="field-resource-options">
								{#each fieldResourceSuggestions as resource}
									<option value={resource}></option>
								{/each}
							</datalist>
							<div class="flex flex-wrap gap-3">
								<button type="submit" class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-solid)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-solid-text)] transition hover:opacity-90">
									{selectedRequest.siteVisitSchedule ? 'Update site visit' : 'Schedule site visit'}
								</button>
								<a href={selectedRequestScheduleHref} class="inline-flex items-center gap-2 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel)]">
									<ExternalLink size={14} />
									Open calendar view
								</a>
							</div>
						</form>
					{:else}
						<div class="mt-4 rounded-md border border-amber-400/25 bg-amber-400/10 px-4 py-3 text-sm leading-6 text-amber-800">
							<span class="font-semibold">Scheduling is blocked:</span>
							{#if selectedQualification?.blockerLabels.length}
								{selectedQualification.blockerLabels.join(' · ')}. Clear the blockers and move the request to Qualified before booking.
							{:else}
								Move the request to Qualified before booking.
							{/if}
						</div>
					{/if}
				</section>

				<section class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
					<div class="grid gap-5 xl:grid-cols-[minmax(0,1fr)_320px]">
						<div class="space-y-5">
							<div class="flex items-start justify-between gap-3">
								<div>
									<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Request details</p>
									<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">Dense record view for the scope summary, timing, and next-step posture.</p>
								</div>
								<button
									type="button"
									class={`inline-flex items-center gap-2 rounded-md border px-3 py-2 text-[0.68rem] font-semibold uppercase tracking-[0.14em] transition ${detailInlineEditing ? 'border-[var(--accent-border)] bg-[var(--accent-soft)] text-[var(--accent-text)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel-strong)] text-[var(--text-strong)] hover:bg-[var(--shell-panel)]'}`}
									onclick={() => {
										if (detailInlineEditing) {
											resetDetailDraft(selectedRequest);
											return;
										}
										detailInlineEditing = true;
									}}
								>
									<Pencil size={14} />
									{detailInlineEditing ? 'Cancel edit' : 'Edit inline'}
								</button>
							</div>

							<div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
								{#each selectedOpsSnapshot as item}
									<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{item.label}</p>
										<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{item.value}</p>
										<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{item.detail}</p>
									</div>
								{/each}
							</div>

							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
								<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Scope summary</p>
								<p class="mt-3 text-sm leading-7 text-[var(--text-base)]">{selectedRequest.need}</p>
							</div>

							{#if detailInlineEditing}
								<form method="POST" action="?/updateRequest" class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
									<input type="hidden" name="id" value={selectedRequest.id} />
									<input type="hidden" name="status" value={selectedRequest.status} />
									<input type="hidden" name="assignedTo" value={selectedRequest.assignedTo} />
									<input type="hidden" name="contactName" value={selectedRequest.contactName} />
									<input type="hidden" name="email" value={selectedRequest.email} />
									<input type="hidden" name="phone" value={selectedRequest.phone} />
									<input type="hidden" name="siteName" value={selectedRequest.siteName} />
									<input type="hidden" name="address1" value={selectedAddress.address1} />
									<input type="hidden" name="address2" value={selectedAddress.address2} />
									<input type="hidden" name="city" value={selectedAddress.city} />
									<input type="hidden" name="state" value={selectedAddress.state} />
									<input type="hidden" name="postalCode" value={selectedAddress.postalCode} />
									{#each selectedQualification?.missingInfoReasonCodes ?? [] as code}
										<input type="hidden" name="missingInfoReasonCodes" value={code} />
									{/each}
									<div class="grid gap-4 lg:grid-cols-[220px_minmax(0,1fr)]">
										<label class="grid gap-2">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Requested timeline</span>
											<input bind:value={detailRequestedTimeline} name="requestedTimeline" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
										</label>
										<label class="grid gap-2">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Next action</span>
											<textarea bind:value={detailNextAction} name="nextAction" rows="3" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none"></textarea>
										</label>
									</div>
									<div class="mt-4 flex flex-wrap gap-3">
										<button type="submit" class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-solid)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-solid-text)] transition hover:opacity-90">Save details</button>
										<button type="button" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]" onclick={() => resetDetailDraft(selectedRequest)}>Cancel</button>
									</div>
								</form>
							{:else}
								<div class="grid gap-3 md:grid-cols-2">
									<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Request timeframe</p>
										<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedRequest.requestedTimeline}</p>
										<p class="mt-1 text-xs text-[var(--text-muted)]">Customer-facing expectation window</p>
									</div>
									<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Next action</p>
										<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{selectedRequest.nextAction}</p>
									</div>
								</div>
							{/if}

							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
								<div class="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
									<div>
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Qualification checklist</p>
										<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Service fit, site readiness, attachments, contact readiness, and scheduling readiness.</p>
									</div>
									<span class={`inline-flex items-center gap-1 rounded-md border px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] ${selectedQualification?.isQualified ? 'border-emerald-400/40 bg-emerald-400/10 text-emerald-300' : 'border-amber-400/40 bg-amber-400/10 text-amber-700'}`}>
										{#if selectedQualification?.isQualified}
											<CheckCircle2 size={13} />
											Qualified
										{:else}
											<AlertTriangle size={13} />
											Blocked
										{/if}
									</span>
								</div>
								<div class="mt-3 space-y-2">
									{#each selectedQualification?.checks ?? [] as check}
										<div class="flex items-start justify-between gap-3 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm">
											<div class="min-w-0">
												<p class="font-semibold text-[var(--text-base)]">{check.label}</p>
												<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{check.detail}</p>
											</div>
											<span class={`shrink-0 rounded-md border px-2 py-1 text-[0.62rem] font-semibold uppercase tracking-[0.14em] ${check.complete ? 'border-emerald-400/40 bg-emerald-400/10 text-emerald-300' : 'border-amber-400/40 bg-amber-400/10 text-amber-700'}`}>{check.complete ? 'Ready' : 'Needs info'}</span>
										</div>
									{/each}
								</div>
								{#if selectedQualification?.blockerLabels.length}
									<div class="mt-3 rounded-md border border-amber-400/25 bg-amber-400/10 px-3 py-2 text-xs leading-5 text-amber-800">
										<span class="font-semibold">Blocking reasons:</span> {selectedQualification.blockerLabels.join(' · ')}
									</div>
								{/if}
							</div>
						</div>
						<div class="space-y-4 border-t border-[var(--shell-border)] pt-5 text-sm xl:border-l xl:border-t-0 xl:pl-5 xl:pt-0">
							<div class="flex items-center justify-between gap-3">
								<div>
									<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Contact and site</p>
									<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">Keep customer, site, and address edits in the same dense workspace.</p>
								</div>
								<button
									type="button"
									class="inline-flex h-9 w-9 items-center justify-center rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] text-[var(--accent-text)] transition hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)]"
									onclick={() => {
										if (contactInlineEditing) {
											resetContactDraft(selectedRequest);
											return;
										}
										contactInlineEditing = true;
									}}
									aria-label="Edit contact information"
									title="Edit contact information"
								>
									<Pencil size={16} />
								</button>
							</div>
							{#if contactInlineEditing}
								<form method="POST" action="?/updateRequest" class="space-y-4 rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4">
									<input type="hidden" name="id" value={selectedRequest.id} />
									<input type="hidden" name="status" value={selectedRequest.status} />
									<input type="hidden" name="assignedTo" value={selectedRequest.assignedTo} />
									<input type="hidden" name="nextAction" value={selectedRequest.nextAction} />
									<input type="hidden" name="requestedTimeline" value={selectedRequest.requestedTimeline} />
									{#each selectedQualification?.missingInfoReasonCodes ?? [] as code}
										<input type="hidden" name="missingInfoReasonCodes" value={code} />
									{/each}
									<div class="grid gap-3">
										<label class="grid gap-1">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Contact name</span>
											<input bind:value={contactNameDraft} name="contactName" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
										</label>
										<label class="grid gap-1">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Email</span>
											<input bind:value={contactEmailDraft} type="email" name="email" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
										</label>
										<label class="grid gap-1">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Phone</span>
											<input bind:value={contactPhoneDraft} name="phone" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
										</label>
										<label class="grid gap-1">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Site</span>
											<input bind:value={contactSiteNameDraft} name="siteName" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
										</label>
										<label class="grid gap-1">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Address 1</span>
											<input bind:value={contactAddress1Draft} name="address1" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
										</label>
										<label class="grid gap-1">
											<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Address 2</span>
											<input bind:value={contactAddress2Draft} name="address2" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
										</label>
										<div class="grid grid-cols-[minmax(0,1fr)_88px_112px] gap-2">
											<label class="grid gap-1">
												<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">City</span>
												<input bind:value={contactCityDraft} name="city" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
											</label>
											<label class="grid gap-1">
												<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">State</span>
												<input bind:value={contactStateDraft} name="state" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
											</label>
											<label class="grid gap-1">
												<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Zip</span>
												<input bind:value={contactPostalCodeDraft} name="postalCode" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
											</label>
										</div>
									</div>
									<div class="flex flex-wrap gap-3">
										<button type="submit" class="rounded-md border border-[var(--accent-border)] bg-[var(--accent-solid)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-solid-text)] transition hover:opacity-90">Save contact</button>
										<button type="button" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-2.5 text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]" onclick={() => resetContactDraft(selectedRequest)}>Cancel</button>
									</div>
								</form>
							{:else}
								<div class="space-y-4">
									<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
										<p class="text-base font-semibold text-[var(--text-strong)]">{selectedRequest.contactName}</p>
										<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedRequest.email}</p>
										<p class="text-sm text-[var(--text-muted)]">{selectedRequest.phone}</p>
									</div>
									<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3">
										<p class="text-[0.58rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Site</p>
										<p class="mt-1 font-semibold text-[var(--text-strong)]">{selectedRequest.siteName}</p>
										<div class="mt-2 space-y-0.5 text-sm leading-5 text-[var(--text-muted)]">
											{#if selectedAddress.address1}
												<p>{selectedAddress.address1}</p>
											{/if}
											{#if selectedAddress.address2}
												<p>{selectedAddress.address2}</p>
											{/if}
											{#if selectedAddress.city}
												<p>{selectedAddress.city}</p>
											{/if}
											{#if selectedAddress.state || selectedAddress.postalCode}
												<p>{[selectedAddress.state, selectedAddress.postalCode].filter(Boolean).join(' ')}</p>
											{/if}
										</div>
									</div>
								</div>
							{/if}
						</div>
					</div>
				</section>

				<section class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Attachments</p>
							<div class="mt-4">
								{#if selectedRequest.attachments.length}
									<div class="flex flex-wrap gap-3">
										{#each selectedRequest.attachments as attachment}
											<button
												type="button"
												class={`w-[88px] rounded-md border p-2 text-center transition ${selectedAttachment?.id === attachment.id ? 'border-[var(--accent-border)] bg-[var(--accent-soft)]' : 'border-[var(--shell-border)] bg-[var(--shell-panel-strong)] hover:border-[var(--accent-border)] hover:bg-[var(--shell-panel)]'}`}
												onclick={() => viewAttachment(attachment)}
												title={attachment.fileName}
											>
												<span class="mx-auto flex h-[75px] w-[75px] items-center justify-center overflow-hidden rounded-md border border-[var(--shell-border)] bg-black/20 text-[var(--accent-text)]">
													{#if isImageAttachment(attachment)}
														<img
															src={`/bdr/admin/requests/attachments/${encodeURIComponent(selectedRequest.id)}/${encodeURIComponent(attachment.id)}`}
															alt={attachment.fileName}
															class="h-full w-full object-cover"
														/>
													{:else}
														<FileText size={24} />
													{/if}
												</span>
												<span class="mt-2 block truncate text-[0.68rem] font-semibold leading-4 text-[var(--text-strong)]">{formatAttachmentName(attachment.fileName)}</span>
											</button>
										{/each}
									</div>

									{#if selectedAttachment}
										<div class="mt-5 border-t border-[var(--shell-border)] pt-5">
											<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
												<div>
													<h5 class="break-all text-lg font-semibold text-[var(--text-strong)]">{selectedAttachment.fileName}</h5>
													<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedAttachment.contentType} · {formatAttachmentSize(selectedAttachment.sizeBytes)}</p>
												</div>
												<div class="flex flex-wrap gap-2">
													<a
														href={selectedAttachmentUrl}
														target="_blank"
														rel="noreferrer"
														class="inline-flex items-center gap-2 rounded-md border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-2 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--accent-text)]"
													>
														<ExternalLink size={15} />
														Enlarge
													</a>
													<button
														type="button"
														class="inline-flex items-center rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--text-strong)] transition hover:bg-[var(--shell-panel)]"
														onclick={closeAttachmentPreview}
													>
														Close
													</button>
												</div>
											</div>

											<div class="mt-4 overflow-hidden rounded-md border border-[var(--shell-border)] bg-black/20">
												{#if selectedAttachment.contentType.startsWith('image/')}
													<img src={selectedAttachmentUrl} alt={selectedAttachment.fileName} class="max-h-[640px] w-full object-contain" />
												{:else if selectedAttachmentCanPreview}
													<iframe src={selectedAttachmentUrl} title={selectedAttachment.fileName} class="h-[640px] w-full bg-white"></iframe>
												{:else}
													<div class="p-5 text-sm leading-6 text-[var(--text-muted)]">
														This file type cannot be previewed inline. Enlarge it in a new tab to review the document.
													</div>
												{/if}
											</div>
										</div>
									{:else}
										<p class="mt-4 rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-sm text-[var(--text-muted)]">Select a file to preview it here.</p>
									{/if}
								{:else}
									<p class="rounded-md border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-sm text-[var(--text-muted)]">No files attached.</p>
								{/if}
							</div>
				</section>

				<section class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel)] p-5">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Activity history</p>
					<p class="mt-2 text-sm text-[var(--text-muted)]">Submission, ownership, status, scheduling, and estimate activity stay visible from the main request workspace.</p>
					<div class="mt-4 space-y-3">
						{#each selectedRequest.timeline as event}
							<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
								<div class="flex items-start justify-between gap-3">
									<div>
										<p class="text-sm font-semibold text-[var(--text-strong)]">{event.label}</p>
										<p class="mt-1 text-xs text-[var(--text-muted)]">{event.actor}</p>
									</div>
									<p class="shrink-0 text-xs text-[var(--muted)]">{formatSubmittedAt(event.occurredAtUtc)}</p>
								</div>
								{#if event.siteVisitSchedule}
									<div class="mt-3 grid gap-3 lg:grid-cols-3">
										<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
											<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Visit window</p>
											<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatScheduleDate(event.siteVisitSchedule.visitDate)}</p>
											<p class="mt-1 text-xs text-[var(--text-muted)]">{formatScheduleWindow(event.siteVisitSchedule.windowStart, event.siteVisitSchedule.windowEnd)}</p>
										</div>
										<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
											<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Site contact</p>
											<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{event.siteVisitSchedule.siteContact}</p>
											<p class="mt-1 text-xs text-[var(--text-muted)]">{event.siteVisitSchedule.siteContactPhone || 'Phone not captured'}</p>
										</div>
										<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-3">
											<p class="text-[0.58rem] font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Field resource</p>
											<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{event.siteVisitSchedule.assignedFieldResource}</p>
											<p class="mt-1 text-xs text-[var(--text-muted)]">Scheduled by {event.siteVisitSchedule.scheduledBy}</p>
										</div>
									</div>
								{:else if event.payload}
									<p class="mt-3 text-xs leading-5 text-[var(--text-muted)]">{event.payload.companyName} submitted {event.payload.serviceType} for {event.payload.siteName} with timeline "{event.payload.requestedTimeline}".</p>
								{/if}
								{#if event.note}
									<p class="mt-3 text-xs leading-5 text-[var(--text-muted)]">{event.note}</p>
								{/if}
							</div>
						{/each}
					</div>
				</section>
			{:else}
				<div class="rounded-lg border border-dashed border-[var(--shell-border)] bg-[var(--shell-panel)] p-8 text-center text-sm text-[var(--text-muted)]">
					Pick a request from the queue to open the workspace.
				</div>
			{/if}
		</div>
	{/snippet}

</AdminWorkspace>
