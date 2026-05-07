<script lang="ts">
	import {
		bdrEmployeePermissionMeta,
		bdrEmployeeSkillMeta,
		getBdrEmployeeByName,
		getEligibleBdrEmployeesForTask,
		getRecommendedBdrEmployeeForTask,
		quoteWorkflowTaskRequirements
	} from '$lib/bdr-team';
	import {
		buildQuoteRequestWorkflowGuidance,
		buildQuoteRequestQualification,
		quoteRequestSiteVisitCancellationReasonOptions,
		getQuoteRequestWorkflowLane,
		getQuoteRequestWorkflowPhase,
		isQuoteRequestUnassigned,
		quoteRequestMissingInfoReasonOptions,
		quoteRequestStatusMeta,
		quoteRequestWorkflowActionMeta,
		quoteRequestWorkflowLaneMeta,
		type QuoteRequest,
		type QuoteRequestMissingInfoReasonCode,
		type QuoteRequestWorkflowActionKey,
		type QuoteRequestWorkflowLane
	} from '$lib/quote-requests';
	import { AlertTriangle, CalendarCheck, CheckCircle2, ExternalLink, FileText, Lock, Pencil } from 'lucide-svelte';
	import type { ActionData, PageProps } from './$types';

	type BobMove = {
		label: string;
		detail: string;
		href: string;
	};
	type QuoteQueueFilter = 'all' | QuoteRequestWorkflowLane | 'closed';
	type QuoteTone = 'amber' | 'blue' | 'violet' | 'emerald' | 'slate';

	let { data, form }: { data: PageProps['data']; form: ActionData } = $props();

	const requests = $derived(data.requests);
	const scheduleSiteVisitByRequestId = $derived(data.scheduleSiteVisitByRequestId);
	let selectedRequestId = $state('');
	let laneFilter = $state<QuoteQueueFilter>('new');
	let search = $state('');
	let selectedAttachmentId = $state('');
	let scheduleVisitDate = $state('');
	let scheduleWindowStart = $state('09:00');
	let scheduleWindowEnd = $state('10:30');
	let scheduleSiteContact = $state('');
	let scheduleSiteContactPhone = $state('');
	let scheduleAssignedFieldResource = $state('');
	let scheduleNotes = $state('');
	let cancellationReasonCode = $state('');
	let cancellationNotes = $state('');
	let workflowAssignedTo = $state('');
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

	const employeeContacts = $derived(data.employeeContacts);
	const fieldResourceSuggestions = $derived(
		employeeContacts
			.filter((employee) => employee.skills.includes('field-inspection'))
			.map((employee) => employee.displayName)
	);
	const quoteToneStyles: Record<
		QuoteTone,
		{
			cardTop: string;
			notice: string;
			pill: string;
		}
	> = {
		amber: {
			cardTop: 'border-t-amber-400',
			notice: 'border-amber-400 bg-amber-50 text-amber-900',
			pill: 'bg-amber-100 text-amber-800'
		},
		blue: {
			cardTop: 'border-t-sky-400',
			notice: 'border-sky-400 bg-sky-50 text-sky-900',
			pill: 'bg-sky-100 text-sky-800'
		},
		violet: {
			cardTop: 'border-t-violet-400',
			notice: 'border-violet-400 bg-violet-50 text-violet-900',
			pill: 'bg-violet-100 text-violet-800'
		},
		emerald: {
			cardTop: 'border-t-emerald-400',
			notice: 'border-emerald-400 bg-emerald-50 text-emerald-900',
			pill: 'bg-emerald-100 text-emerald-800'
		},
		slate: {
			cardTop: 'border-t-slate-400',
			notice: 'border-slate-400 bg-slate-50 text-slate-900',
			pill: 'bg-slate-100 text-slate-700'
		}
	};

	$effect(() => {
		if (!selectedRequestId && requests[0]) {
			selectedRequestId = requests[0].id;
		}
	});

	const laneMatches = (request: QuoteRequest) => {
		if (laneFilter === 'all') return true;
		if (laneFilter === 'closed') return request.status === 'closed';
		if (laneFilter === 'won') return request.status === 'won';
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
	const selectedWorkflowGuidance = $derived(selectedRequest ? buildQuoteRequestWorkflowGuidance(selectedRequest) : null);
	const selectedWorkflowTaskRequirement = $derived(
		selectedWorkflowGuidance?.taskKey ? quoteWorkflowTaskRequirements[selectedWorkflowGuidance.taskKey] : null
	);
	const selectedWorkflowEligibleEmployees = $derived(
		getEligibleBdrEmployeesForTask(selectedWorkflowGuidance?.taskKey)
	);
	const selectedWorkflowOtherEmployees = $derived(
		employeeContacts.filter(
			(employee) => !selectedWorkflowEligibleEmployees.some((eligible) => eligible.id === employee.id)
		)
	);
	const selectedWorkflowRecommendedEmployee = $derived(
		getRecommendedBdrEmployeeForTask(selectedWorkflowGuidance?.taskKey, selectedRequest)
	);
	const selectedWorkflowAssignedEmployee = $derived(getBdrEmployeeByName(workflowAssignedTo));
	const selectedTone = $derived<QuoteTone>(selectedRequest ? quoteRequestStatusMeta[selectedRequest.status].tone : 'slate');
	const selectedToneStyle = $derived(quoteToneStyles[selectedTone]);
	const selectedNoticeDetail = $derived.by(() => {
		if (!selectedRequest) return '';
		return selectedWorkflowGuidance?.detail ?? quoteRequestStatusMeta[selectedRequest.status].detail;
	});
	const selectedWorkflowPhase = $derived(selectedRequest ? getQuoteRequestWorkflowPhase(selectedRequest.status) : null);
	const selectedRequestScheduleSectionId = $derived(
		selectedRequest ? `schedule-site-visit-${selectedRequest.id}` : 'schedule-site-visit'
	);
	const selectedRequestCanOpenScheduler = $derived(
		Boolean(selectedRequest && (selectedWorkflowGuidance?.canBookVisit || selectedRequest.status === 'inspection-scheduled'))
	);
	const selectedSiteVisitIsComplete = $derived(
		Boolean(selectedRequest?.siteVisitSchedule && selectedRequest.status !== 'inspection-scheduled')
	);

	$effect(() => {
		if (!selectedRequest?.attachments.some((attachment) => attachment.id === selectedAttachmentId)) {
			selectedAttachmentId = '';
		}
	});

	$effect(() => {
		if (!selectedRequest) {
			workflowAssignedTo = '';
			return;
		}

		const existingEmployee = getBdrEmployeeByName(selectedRequest.assignedTo);
		const existingIsEligible = Boolean(
			existingEmployee &&
				selectedWorkflowEligibleEmployees.some((employee) => employee.id === existingEmployee.id)
		);
		workflowAssignedTo = existingIsEligible
			? (existingEmployee?.displayName ?? selectedRequest.assignedTo)
			: selectedWorkflowRecommendedEmployee?.displayName || selectedRequest.assignedTo;
	});

	const laneOptions = $derived([
		{ key: 'all' as const, label: 'All queue', count: requests.length },
		...quoteRequestWorkflowLaneMeta.map((lane) => ({
			key: lane.key,
			label: lane.label,
			count: requests.filter((request) =>
				lane.key === 'won'
					? request.status === 'won'
					: getQuoteRequestWorkflowLane(request.status) === lane.key
			).length
		})),
		{ key: 'closed' as const, label: 'Closed', count: requests.filter((request) => request.status === 'closed').length }
	]);

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
		cancellationReasonCode = '';
		cancellationNotes = '';
	});

	const quoteSummaryCards = $derived([
		{ label: 'New quotes', value: String(data.metrics.newCount), icon: '📥' },
		{ label: 'Active', value: String(data.metrics.activeCount), icon: '🔎' },
		{
			label: 'Ready to book',
			value: String(requests.filter((request) => buildQuoteRequestWorkflowGuidance(request).canBookVisit).length),
			icon: '📅'
		},
		{
			label: 'Blocked',
			value: String(requests.filter((request) => buildQuoteRequestQualification(request).blockerLabels.length).length),
			icon: '⚠️'
		}
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

	const bobMoves = $derived.by(() => {
		if (!selectedRequest) {
			return [
				{
					label: 'Review queue',
					detail: `${filteredRequests.length} request${filteredRequests.length === 1 ? '' : 's'} in view`,
					href: '/bdr/admin/requests'
				}
			] satisfies BobMove[];
		}

		const moves: BobMove[] = [];
		const blockers = selectedQualification?.blockerLabels ?? [];
		const attachmentCount = selectedRequest.attachments.length;

		if (blockers.length) {
			moves.push({
				label: 'Chase missing intake',
				detail: blockers.slice(0, 2).join(' · '),
				href: `#request-triage-${selectedRequest.id}`
			});
		} else if (!selectedRequest.siteVisitSchedule && selectedRequestCanOpenScheduler) {
			moves.push({
				label: 'Book site visit',
				detail: selectedRequest.requestedTimeline || 'Qualified and ready to schedule',
				href: `#${selectedRequestScheduleSectionId}`
			});
		} else if (selectedRequest.status === 'inspection-scheduled' && selectedRequest.siteVisitSchedule) {
			moves.push({
				label: 'Complete site visit',
				detail: `${selectedRequest.siteVisitSchedule.visitDate} · ${selectedRequest.siteVisitSchedule.assignedFieldResource}`,
				href: `#request-triage-${selectedRequest.id}`
			});
		} else if (selectedRequest.status === 'estimate-drafted') {
			moves.push({
				label: 'Send estimate',
				detail: selectedRequest.nextAction,
				href: `#request-triage-${selectedRequest.id}`
			});
		} else if (selectedRequest.status === 'estimate-sent') {
			moves.push({
				label: 'Close outcome',
				detail: 'Mark won or close after customer follow-up',
				href: `#request-triage-${selectedRequest.id}`
			});
		} else {
			moves.push({
				label: 'Confirm queue owner',
				detail: selectedRequest.assignedTo || 'Unassigned intake',
				href: `#request-triage-${selectedRequest.id}`
			});
		}

		moves.push({
			label: `Call ${selectedRequest.contactName.split(' ')[0] || 'customer'}`,
			detail: selectedRequest.phone || selectedRequest.email || selectedRequest.siteName,
			href: `#contact-site-${selectedRequest.id}`
		});
		moves.push({
			label: 'Review scope and files',
			detail: `${attachmentCount} file${attachmentCount === 1 ? '' : 's'} · ${selectedRequest.serviceType}`,
			href: `#request-details-${selectedRequest.id}`
		});

		return moves;
	});

	const quoteCardStateClass = (request: QuoteRequest, isSelected: boolean) => {
		const tone = quoteRequestStatusMeta[request.status].tone;
		const topBorderClass = quoteToneStyles[tone].cardTop;
		return isSelected
			? `border-x border-b border-x-transparent border-b-transparent border-t-4 ${topBorderClass} bg-[#fff4ea] shadow-[0_1px_2px_rgba(15,23,42,0.08),0_10px_24px_rgba(249,115,22,0.14)] ring-1 ring-[rgba(249,115,22,0.32)]`
			: `border-x border-b border-x-transparent border-b-transparent border-t-4 ${topBorderClass} bg-white/88 shadow-[var(--shell-shadow)] hover:bg-white hover:shadow-md`;
	};
	const workflowActionButtonClass = (action: QuoteRequestWorkflowActionKey) => {
		const tone = quoteRequestWorkflowActionMeta[action].tone;
		if (tone === 'danger') {
			return 'bg-rose-50 text-rose-700 shadow-sm transition hover:bg-rose-100';
		}
		if (tone === 'primary') {
			return 'bg-[var(--accent-solid)] text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]';
		}
		return 'bg-white/80 text-[var(--text-strong)] shadow-sm transition hover:bg-white';
	};
	const qualificationReadyText = 'Ready to book';

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
	const selectedQuickFacts = $derived.by(() => {
		if (!selectedRequest) return [] as { label: string; value: string; detail: string }[];
		return [
			{
				label: 'Lane',
				value: selectedWorkflowPhase?.label ?? quoteRequestStatusMeta[selectedRequest.status].label,
				detail: selectedWorkflowLane ? quoteRequestWorkflowLaneMeta.find((lane) => lane.key === selectedWorkflowLane)?.label ?? 'Queue' : 'Queue'
			},
			{
				label: 'Source',
				value: selectedRequest.source === 'public-site' ? 'Public site' : selectedRequest.source === 'office' ? 'Office' : 'Referral',
				detail: selectedRequest.priority === 'emergency' ? 'Emergency' : `${selectedRequest.attachments.length} file${selectedRequest.attachments.length === 1 ? '' : 's'}`
			},
			{ label: 'Property', value: selectedRequest.propertyType, detail: selectedRequest.projectType },
			{ label: 'Timeline', value: selectedRequest.requestedTimeline, detail: selectedRequest.preferredTimeline }
		];
	});

	$effect(() => {
		resetDetailDraft(selectedRequest);
		resetContactDraft(selectedRequest);
	});
	const isQualificationCheckBlocked = (code: QuoteRequestMissingInfoReasonCode) =>
		Boolean(selectedQualification?.missingInfoReasonCodes.includes(code));
	const getQualificationBlockerDetail = (code: QuoteRequestMissingInfoReasonCode) =>
		quoteRequestMissingInfoReasonOptions.find((option) => option.value === code)?.detail ??
		'Review this qualification item before booking the site visit.';

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

<div class="space-y-5">
	<section class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
		<h1 class="text-2xl font-semibold leading-8 tracking-normal text-[var(--text-strong)]">Quotes</h1>
		<a
			href="/bdr/admin/estimates"
			class="inline-flex items-center justify-center rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold leading-5 text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)] focus:outline-none focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-offset-2"
		>
			+ New Estimate
		</a>
	</section>

	<section class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
		{#each quoteSummaryCards as card}
			<div class="flex h-32 flex-col justify-between rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
				<div class="flex h-10 w-10 items-center justify-center rounded-lg bg-white/85 text-xl" aria-hidden="true">
					{card.icon}
				</div>
				<div>
					<p class="text-3xl font-semibold leading-none tracking-normal text-[var(--text-strong)]">{card.value}</p>
					<p class="mt-2 text-sm font-medium leading-5 text-[var(--text-muted)]">{card.label}</p>
				</div>
			</div>
		{/each}
	</section>

	<section class="grid gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
		<aside class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
			<div class="flex items-center justify-between gap-3">
				<h2 class="text-base font-semibold leading-6 text-[var(--text-strong)]">Quote queue</h2>
				<span class="rounded-full bg-[var(--shell-panel-strong)] px-3 py-1 text-xs font-semibold text-[var(--text-muted)]">
					{filteredRequests.length}
				</span>
			</div>

			<div class="mt-4 flex flex-wrap gap-2">
				{#each laneOptions as lane}
					<button
						type="button"
						class={`rounded-full px-3 py-1.5 text-xs font-semibold transition ${
							laneFilter === lane.key
								? 'bg-[var(--accent-solid)] text-white shadow-sm'
								: 'bg-white/80 text-[var(--text-muted)] shadow-sm hover:bg-white hover:text-[var(--text-strong)]'
						}`}
						onclick={() => (laneFilter = lane.key)}
					>
						{lane.label} · {lane.count}
					</button>
				{/each}
			</div>

			<label class="mt-4 block">
				<span class="sr-only">Search quotes</span>
				<input
					bind:value={search}
					placeholder="Search quotes"
					class="w-full rounded-md border border-transparent bg-white px-3 py-2.5 text-sm text-[var(--text-base)] shadow-sm outline-none placeholder:text-[var(--muted)] focus:border-[var(--accent-border)] focus:ring-2 focus:ring-[var(--focus-ring)]"
				/>
			</label>

			<div class="mt-4 space-y-2">
				{#if filteredRequests.length}
					{#each filteredRequests as request}
						{@const qualification = buildQuoteRequestQualification(request)}
						<button
							type="button"
							class={`w-full rounded-lg p-3 text-left transition ${quoteCardStateClass(request, selectedRequest?.id === request.id)}`}
							onclick={() => (selectedRequestId = request.id)}
						>
							<div class="flex items-start gap-3">
								<span class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-white/85 text-xl" aria-hidden="true">
									📥
								</span>
								<div class="min-w-0 flex-1">
									<div class="flex items-start justify-between gap-2">
										<p class="truncate text-sm font-semibold leading-5 text-[var(--text-strong)]">{request.customerName}</p>
										<p class="shrink-0 text-xs leading-5 text-[var(--text-muted)]">{formatSubmittedAt(request.submittedAtUtc)}</p>
									</div>
									<p class="mt-1 truncate text-xs leading-5 text-[var(--text-muted)]">{request.siteName}</p>
									<p class="truncate text-xs leading-5 text-[var(--text-muted)]">{request.serviceType}</p>
									<div class="mt-3 flex flex-wrap gap-2">
										<span class="rounded-full bg-[var(--accent-soft)] px-2.5 py-1 text-xs font-semibold text-[var(--accent-text)]">
											{quoteRequestStatusMeta[request.status].label}
										</span>
										{#if qualification.blockerLabels.length}
											<span class="inline-flex min-w-0 items-center gap-1 rounded-full bg-amber-100 px-2.5 py-1 text-xs font-semibold text-amber-800">
												<AlertTriangle size={12} />
												<span class="truncate">{qualification.blockerLabels[0]}</span>
											</span>
										{:else}
											<span class="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">
												<CheckCircle2 size={12} />
												{qualificationReadyText}
											</span>
										{/if}
									</div>
								</div>
							</div>
						</button>
					{/each}
				{:else}
					<div class="rounded-lg bg-white/70 p-6 text-center text-sm text-[var(--text-muted)] shadow-sm">
						No quotes match this view.
					</div>
				{/if}
			</div>
		</aside>

		<div class="min-w-0 space-y-4">
			{#if selectedRequest}
				<article id={`request-triage-${selectedRequest.id}`} class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
					<div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
						<div class="flex min-w-0 items-start gap-3">
							<span class="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-white/85 text-2xl" aria-hidden="true">
								📝
							</span>
							<div class="min-w-0">
								<h2 class="truncate text-2xl font-semibold leading-8 text-[var(--text-strong)]">{selectedRequest.companyName}</h2>
								<p class="mt-1 text-sm leading-5 text-[var(--text-muted)]">{selectedRequest.serviceType} · {selectedRequest.siteName}</p>
							</div>
						</div>
						<div class="flex flex-wrap items-center gap-2 lg:justify-end">
							<span class={`rounded-full px-3 py-1.5 text-sm font-semibold ${selectedToneStyle.pill}`}>
								{quoteRequestStatusMeta[selectedRequest.status].label}
							</span>
							<span class="rounded-full bg-white/80 px-3 py-1.5 text-sm font-medium text-[var(--text-muted)] shadow-sm">
								{formatSubmittedAt(selectedRequest.submittedAtUtc)}
							</span>
						</div>
					</div>

					{#if form?.success && form.updatedRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">Saved</p>
					{:else if form?.message && form.updatedRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-800">{form.message}</p>
					{/if}

					{#if form?.workflowSuccess && form.workflowRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">Workflow updated</p>
					{:else if form?.workflowMessage && form.workflowRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-800">{form.workflowMessage}</p>
					{/if}

					<div class={`mt-4 rounded-lg border-l-4 px-4 py-3 shadow-sm ${selectedToneStyle.notice}`}>
						<p class="text-sm font-semibold">{quoteRequestStatusMeta[selectedRequest.status].label}</p>
						<p class="mt-1 text-sm leading-6 opacity-85">{selectedNoticeDetail}</p>
					</div>

					<div class="mt-5 grid gap-4 xl:grid-cols-[minmax(0,1.45fr)_340px]">
						<div class="rounded-lg bg-[var(--shell-panel-strong)] p-4">
							<div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
								<div>
									<p class="text-base font-semibold leading-6 text-[var(--text-strong)]">Workflow</p>
									<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{selectedWorkflowGuidance?.detail}</p>
								</div>
								<span class={`w-fit rounded-full px-3 py-1 text-xs font-semibold shadow-sm ${selectedToneStyle.pill}`}>
									{selectedWorkflowGuidance?.phaseLabel}
								</span>
							</div>

							<form method="POST" action="?/updateRequest" class="mt-4 space-y-4">
								<input type="hidden" name="id" value={selectedRequest.id} />
								<input type="hidden" name="status" value={selectedRequest.status} />
								<input type="hidden" name="contactName" value={selectedRequest.contactName} />
								<input type="hidden" name="email" value={selectedRequest.email} />
								<input type="hidden" name="phone" value={selectedRequest.phone} />
								<input type="hidden" name="siteName" value={selectedRequest.siteName} />
								<input type="hidden" name="address1" value={selectedAddress.address1} />
								<input type="hidden" name="address2" value={selectedAddress.address2} />
								<input type="hidden" name="city" value={selectedAddress.city} />
								<input type="hidden" name="state" value={selectedAddress.state} />
								<input type="hidden" name="postalCode" value={selectedAddress.postalCode} />
								<input type="hidden" name="requestedTimeline" value={selectedRequest.requestedTimeline} />
								{#if selectedRequest.status === 'needs-info'}
									{#each selectedQualification?.missingInfoReasonCodes ?? [] as code}
										<input type="hidden" name="missingInfoReasonCodes" value={code} />
									{/each}
								{/if}

								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">Task owner</span>
									<select id="assignedTo" name="assignedTo" bind:value={workflowAssignedTo} class="min-h-12 rounded-md border border-[var(--shell-border)] bg-white px-3 py-3 text-sm text-[var(--text-base)] outline-none">
										{#if selectedRequest.assignedTo && !getBdrEmployeeByName(selectedRequest.assignedTo)}
											<option value={selectedRequest.assignedTo}>{selectedRequest.assignedTo} · current queue</option>
										{/if}
										{#if selectedWorkflowEligibleEmployees.length}
											<optgroup label="Skill match">
												{#each selectedWorkflowEligibleEmployees as employee}
													<option value={employee.displayName}>
														{employee.displayName} · {employee.title}
													</option>
												{/each}
											</optgroup>
										{/if}
										{#if selectedWorkflowOtherEmployees.length}
											<optgroup label="Override">
												{#each selectedWorkflowOtherEmployees as employee}
													<option value={employee.displayName}>
														{employee.displayName} · {employee.title}
													</option>
												{/each}
											</optgroup>
										{/if}
									</select>
								</label>

								<div class="rounded-lg bg-white/70 px-3 py-3">
									<div class="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
										<div>
											<p class="text-sm font-semibold text-[var(--text-strong)]">
												{selectedWorkflowTaskRequirement?.label ?? 'Workflow task'}
											</p>
											<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">
												{#if selectedWorkflowRecommendedEmployee && selectedWorkflowTaskRequirement}
													Auto-picked {selectedWorkflowRecommendedEmployee.displayName} for {bdrEmployeeSkillMeta[selectedWorkflowTaskRequirement.skill].label.toLowerCase()}.
												{:else}
													No skill-matched employee is available for this workflow task.
												{/if}
											</p>
										</div>
										{#if selectedWorkflowAssignedEmployee}
											<span class="w-fit rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
												{selectedWorkflowAssignedEmployee.availability}
											</span>
										{/if}
									</div>
									{#if selectedWorkflowTaskRequirement}
										<div class="mt-3 flex flex-wrap gap-2">
											<span class="rounded-full bg-[var(--accent-soft)] px-2.5 py-1 text-xs font-semibold text-[var(--accent-text)]">
												{bdrEmployeeSkillMeta[selectedWorkflowTaskRequirement.skill].label}
											</span>
											<span class="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-[var(--text-muted)] shadow-sm">
												{bdrEmployeePermissionMeta[selectedWorkflowTaskRequirement.permission].label}
											</span>
										</div>
									{/if}
								</div>

								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">Next action</span>
									<textarea id="nextAction" name="nextAction" rows="5" class="min-h-32 w-full resize-y rounded-md border border-[var(--shell-border)] bg-white px-3 py-3 text-sm leading-6 text-[var(--text-base)] outline-none">{selectedRequest.nextAction}</textarea>
								</label>

								<button type="submit" class="rounded-md bg-white/80 px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-white">Save owner/action</button>
							</form>

							<div class="mt-4 flex flex-wrap gap-3">
								{#if selectedWorkflowGuidance?.canBookVisit}
									<a href={`#${selectedRequestScheduleSectionId}`} class="inline-flex items-center gap-2 rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]">
										<CalendarCheck size={16} />
										Book visit
									</a>
								{/if}

								{#each selectedWorkflowGuidance?.actions ?? [] as action}
									<form method="POST" action="?/applyWorkflowAction">
										<input type="hidden" name="id" value={selectedRequest.id} />
										<input type="hidden" name="workflowAction" value={action} />
										<input type="hidden" name="assignedTo" value={workflowAssignedTo} />
										<button type="submit" class={`inline-flex items-center gap-2 rounded-md px-4 py-2.5 text-sm font-semibold ${workflowActionButtonClass(action)}`}>
											{#if action === 'request-missing-info'}
												<AlertTriangle size={16} />
											{:else if action === 'mark-visit-complete' || action === 'mark-won'}
												<CheckCircle2 size={16} />
											{:else if action === 'close-quote'}
												<Lock size={16} />
											{:else}
												<CalendarCheck size={16} />
											{/if}
											{quoteRequestWorkflowActionMeta[action].label}
										</button>
									</form>
								{/each}
							</div>
						</div>

						<aside class="rounded-lg bg-white/75 p-4 shadow-sm">
							<div class="flex items-start justify-between gap-3">
								<div class="flex items-start gap-3">
									<span class="flex h-10 w-10 shrink-0 items-center justify-center text-2xl" aria-hidden="true">👷‍♂️</span>
									<div>
										<h3 class="text-base font-semibold leading-6 text-[var(--text-strong)]">Bob</h3>
										<p class="text-sm leading-5 text-[var(--text-muted)]">Next moves</p>
									</div>
								</div>
								<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg shadow-sm" title="AI-driven">✨</span>
							</div>
							<div class="mt-4 space-y-2">
								{#each bobMoves as move}
									<a href={move.href} class="block rounded-lg bg-[var(--shell-panel-strong)] px-3 py-3 shadow-sm transition hover:bg-white">
										<p class="text-sm font-semibold leading-5 text-[var(--text-strong)]">{move.label}</p>
										<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{move.detail}</p>
									</a>
								{/each}
							</div>
						</aside>
					</div>
				</article>

				<section id={`request-details-${selectedRequest.id}`} class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
					<article class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<div class="flex items-center justify-between gap-3">
							<h3 class="text-base font-semibold leading-6 text-[var(--text-strong)]">Quote details</h3>
							<button
								type="button"
								class={`inline-flex h-9 w-9 items-center justify-center rounded-md bg-white/80 text-[var(--accent-text)] shadow-sm transition hover:bg-white ${detailInlineEditing ? 'ring-2 ring-[var(--accent-border)]' : ''}`}
								onclick={() => {
									if (detailInlineEditing) {
										resetDetailDraft(selectedRequest);
										return;
									}
									detailInlineEditing = true;
								}}
								aria-label="Edit quote details"
								title="Edit quote details"
							>
								<Pencil size={16} />
							</button>
						</div>

						<div class="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
							{#each selectedQuickFacts as item}
								<div class="rounded-lg bg-[var(--shell-panel-strong)] px-3 py-3">
									<p class="text-xs font-medium text-[var(--text-muted)]">{item.label}</p>
									<p class="mt-2 truncate text-sm font-semibold text-[var(--text-strong)]">{item.value}</p>
									<p class="mt-1 truncate text-xs leading-5 text-[var(--text-muted)]">{item.detail}</p>
								</div>
							{/each}
						</div>

						<div class="mt-4 rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3">
							<p class="text-sm font-semibold text-[var(--text-strong)]">Scope</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{selectedRequest.need}</p>
						</div>

						{#if detailInlineEditing}
							<form method="POST" action="?/updateRequest" class="mt-4 rounded-lg bg-[var(--accent-soft)] p-4">
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
										<span class="text-sm font-medium text-[var(--text-muted)]">Timeline</span>
										<input bind:value={detailRequestedTimeline} name="requestedTimeline" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
									</label>
									<label class="grid gap-2">
										<span class="text-sm font-medium text-[var(--text-muted)]">Next action</span>
										<textarea bind:value={detailNextAction} name="nextAction" rows="3" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none"></textarea>
									</label>
								</div>
								<div class="mt-4 flex flex-wrap gap-3">
									<button type="submit" class="rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]">Save details</button>
									<button type="button" class="rounded-md bg-white/80 px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-white" onclick={() => resetDetailDraft(selectedRequest)}>Cancel</button>
								</div>
							</form>
						{/if}

						<div class="mt-4 rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3">
							<div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
								<p class="text-sm font-semibold text-[var(--text-strong)]">Qualification</p>
								<span class={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-semibold ${selectedQualification?.isQualified ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-100 text-amber-800'}`}>
									{#if selectedQualification?.isQualified}
										<CheckCircle2 size={13} />
										{qualificationReadyText}
									{:else}
										<AlertTriangle size={13} />
										Blocked
									{/if}
								</span>
							</div>
							<div class="mt-3 grid gap-2 md:grid-cols-2">
								{#each selectedQualification?.checks ?? [] as check}
									{@const isBlocked = isQualificationCheckBlocked(check.missingInfoReasonCode)}
									<div class="rounded-md bg-white/75 px-3 py-2.5">
										<div class="flex items-start justify-between gap-3">
											<p class="text-sm font-semibold text-[var(--text-base)]">{check.label}</p>
											<span class={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${isBlocked ? 'bg-amber-100 text-amber-800' : 'bg-emerald-50 text-emerald-700'}`}>{isBlocked ? 'Need' : 'Ready'}</span>
										</div>
										{#if isBlocked}
											<p class="mt-2 text-xs leading-5 text-amber-800">{getQualificationBlockerDetail(check.missingInfoReasonCode)}</p>
										{/if}
									</div>
								{/each}
							</div>
						</div>
					</article>

					<article id={`contact-site-${selectedRequest.id}`} class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<div class="flex items-center justify-between gap-3">
							<h3 class="text-base font-semibold leading-6 text-[var(--text-strong)]">Contact</h3>
							<button
								type="button"
								class={`inline-flex h-9 w-9 items-center justify-center rounded-md bg-white/80 text-[var(--accent-text)] shadow-sm transition hover:bg-white ${contactInlineEditing ? 'ring-2 ring-[var(--accent-border)]' : ''}`}
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
							<form method="POST" action="?/updateRequest" class="mt-4 space-y-3 rounded-lg bg-[var(--accent-soft)] p-4">
								<input type="hidden" name="id" value={selectedRequest.id} />
								<input type="hidden" name="status" value={selectedRequest.status} />
								<input type="hidden" name="assignedTo" value={selectedRequest.assignedTo} />
								<input type="hidden" name="nextAction" value={selectedRequest.nextAction} />
								<input type="hidden" name="requestedTimeline" value={selectedRequest.requestedTimeline} />
								{#each selectedQualification?.missingInfoReasonCodes ?? [] as code}
									<input type="hidden" name="missingInfoReasonCodes" value={code} />
								{/each}
								<label class="grid gap-1">
									<span class="text-sm font-medium text-[var(--text-muted)]">Name</span>
									<input bind:value={contactNameDraft} name="contactName" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-1">
									<span class="text-sm font-medium text-[var(--text-muted)]">Email</span>
									<input bind:value={contactEmailDraft} type="email" name="email" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-1">
									<span class="text-sm font-medium text-[var(--text-muted)]">Phone</span>
									<input bind:value={contactPhoneDraft} name="phone" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-1">
									<span class="text-sm font-medium text-[var(--text-muted)]">Site</span>
									<input bind:value={contactSiteNameDraft} name="siteName" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-1">
									<span class="text-sm font-medium text-[var(--text-muted)]">Address</span>
									<input bind:value={contactAddress1Draft} name="address1" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<input type="hidden" name="address2" value={contactAddress2Draft} />
								<div class="grid grid-cols-[minmax(0,1fr)_78px_96px] gap-2">
									<input bind:value={contactCityDraft} name="city" placeholder="City" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
									<input bind:value={contactStateDraft} name="state" placeholder="State" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
									<input bind:value={contactPostalCodeDraft} name="postalCode" placeholder="Zip" class="rounded-md border border-[var(--shell-border)] bg-white px-3 py-2.5 text-sm text-[var(--text-base)] outline-none" />
								</div>
								<div class="flex flex-wrap gap-3">
									<button type="submit" class="rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]">Save contact</button>
									<button type="button" class="rounded-md bg-white/80 px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-white" onclick={() => resetContactDraft(selectedRequest)}>Cancel</button>
								</div>
							</form>
						{:else}
							<div class="mt-4 space-y-3">
								<div class="rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-base font-semibold text-[var(--text-strong)]">{selectedRequest.contactName}</p>
									<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedRequest.email}</p>
									<p class="text-sm text-[var(--text-muted)]">{selectedRequest.phone}</p>
								</div>
								<div class="rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3">
									<p class="text-sm font-semibold text-[var(--text-strong)]">{selectedRequest.siteName}</p>
									<div class="mt-2 space-y-0.5 text-sm leading-5 text-[var(--text-muted)]">
										{#if selectedAddress.address1}<p>{selectedAddress.address1}</p>{/if}
										{#if selectedAddress.address2}<p>{selectedAddress.address2}</p>{/if}
										{#if selectedAddress.city}<p>{selectedAddress.city}</p>{/if}
										{#if selectedAddress.state || selectedAddress.postalCode}
											<p>{[selectedAddress.state, selectedAddress.postalCode].filter(Boolean).join(' ')}</p>
										{/if}
									</div>
								</div>
							</div>
						{/if}
					</article>
				</section>

				<section id={selectedRequestScheduleSectionId} class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
					<div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
						<h3 class="text-base font-semibold leading-6 text-[var(--text-strong)]">Site visit</h3>
						{#if selectedSiteVisitIsComplete}
							<span class="inline-flex items-center gap-2 rounded-full bg-emerald-50 px-3 py-1.5 text-sm font-semibold text-emerald-700">
								<CheckCircle2 size={14} />
								Completed
							</span>
						{:else if selectedRequest.siteVisitSchedule}
							<span class="inline-flex items-center gap-2 rounded-full bg-emerald-50 px-3 py-1.5 text-sm font-semibold text-emerald-700">
								<CheckCircle2 size={14} />
								Scheduled
							</span>
						{:else if selectedRequestCanOpenScheduler}
							<span class="inline-flex items-center gap-2 rounded-full bg-[var(--accent-soft)] px-3 py-1.5 text-sm font-semibold text-[var(--accent-text)]">
								<CalendarCheck size={14} />
								Ready to schedule
							</span>
						{:else}
							<span class="inline-flex items-center gap-2 rounded-full bg-amber-100 px-3 py-1.5 text-sm font-semibold text-amber-800">
								<AlertTriangle size={14} />
								Blocked
							</span>
						{/if}
					</div>

					{#if form?.scheduleSuccess && form.scheduledRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">Site visit scheduled</p>
					{:else if form?.scheduleMessage && form.scheduledRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-800">{form.scheduleMessage}</p>
					{/if}

					{#if form?.cancelSuccess && form.cancelledRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm font-semibold text-emerald-700">Site visit cancelled</p>
					{:else if form?.cancelMessage && form.cancelledRequestId === selectedRequest.id}
						<p class="mt-4 rounded-md bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-800">{form.cancelMessage}</p>
					{/if}

					{#if selectedRequest.siteVisitSchedule}
						<div class="mt-4 grid gap-3 lg:grid-cols-3">
							<div class="rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3">
								<p class="text-xs font-medium text-[var(--text-muted)]">Window</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatScheduleDate(selectedRequest.siteVisitSchedule.visitDate)}</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">{formatScheduleWindow(selectedRequest.siteVisitSchedule.windowStart, selectedRequest.siteVisitSchedule.windowEnd)}</p>
							</div>
							<div class="rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3">
								<p class="text-xs font-medium text-[var(--text-muted)]">Contact</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedRequest.siteVisitSchedule.siteContact}</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedRequest.siteVisitSchedule.siteContactPhone || 'Phone not captured'}</p>
							</div>
							<div class="rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3">
								<p class="text-xs font-medium text-[var(--text-muted)]">Field owner</p>
								<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedRequest.siteVisitSchedule.assignedFieldResource}</p>
								<p class="mt-1 text-sm text-[var(--text-muted)]">{selectedRequest.siteVisitSchedule.scheduledBy}</p>
							</div>
						</div>
					{/if}

					{#if selectedRequestCanOpenScheduler}
						<form method="POST" action="?/scheduleSiteVisit" class="mt-5 space-y-4">
							<input type="hidden" name="id" value={selectedRequest.id} />
							<div class="grid gap-4 lg:grid-cols-[160px_150px_150px_minmax(0,1fr)]">
								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">Visit date</span>
									<input bind:value={scheduleVisitDate} type="date" name="visitDate" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">Start</span>
									<input bind:value={scheduleWindowStart} type="time" name="windowStart" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">End</span>
									<input bind:value={scheduleWindowEnd} type="time" name="windowEnd" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">Field owner</span>
									<input bind:value={scheduleAssignedFieldResource} list="field-resource-options" name="assignedFieldResource" placeholder="Estimator or crew" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
							</div>
							<div class="grid gap-4 lg:grid-cols-2">
								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">Site contact</span>
									<input bind:value={scheduleSiteContact} name="siteContact" required class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
								<label class="grid gap-2">
									<span class="text-sm font-medium text-[var(--text-muted)]">Phone</span>
									<input bind:value={scheduleSiteContactPhone} name="siteContactPhone" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" />
								</label>
							</div>
							<label class="grid gap-2">
								<span class="text-sm font-medium text-[var(--text-muted)]">Notes</span>
								<textarea bind:value={scheduleNotes} name="notes" rows="3" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-3 text-sm text-[var(--text-base)] outline-none" placeholder="Access, scope, parking"></textarea>
							</label>
							<datalist id="field-resource-options">
								{#each fieldResourceSuggestions as resource}
									<option value={resource}></option>
								{/each}
							</datalist>
							<div class="flex flex-wrap gap-3">
								<button type="submit" class="rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)]">
									{selectedRequest.siteVisitSchedule ? 'Update visit' : 'Schedule visit'}
								</button>
								<a href={selectedRequestScheduleHref} class="inline-flex items-center gap-2 rounded-md bg-white/80 px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-white">
									<ExternalLink size={14} />
									Calendar
								</a>
							</div>
						</form>
					{:else}
						<div class="mt-4 rounded-lg bg-amber-50 px-4 py-3 text-sm leading-6 text-amber-800">
							{#if selectedQualification?.blockerLabels.length}
								{selectedQualification.blockerLabels.join(' · ')}
							{:else}
								Qualification must clear before booking a site visit.
							{/if}
						</div>
					{/if}

					{#if selectedRequest.status === 'inspection-scheduled' && selectedRequest.siteVisitSchedule}
						<form method="POST" action="?/cancelSiteVisit" class="mt-4 rounded-lg bg-rose-50 p-4">
							<input type="hidden" name="id" value={selectedRequest.id} />
							<div class="grid gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
								<label class="grid gap-2">
									<span class="text-sm font-medium text-rose-900">Cancel reason</span>
									<select bind:value={cancellationReasonCode} name="cancellationReasonCode" required class="rounded-md border border-rose-200 bg-white px-3 py-3 text-sm text-[var(--text-base)] outline-none">
										<option value="">Select a reason</option>
										{#each quoteRequestSiteVisitCancellationReasonOptions as reason}
											<option value={reason.value}>{reason.label}</option>
										{/each}
									</select>
								</label>
								<label class="grid gap-2">
									<span class="text-sm font-medium text-rose-900">Notes</span>
									<textarea bind:value={cancellationNotes} name="cancellationNotes" rows="3" class="rounded-md border border-rose-200 bg-white px-3 py-3 text-sm text-[var(--text-base)] outline-none"></textarea>
								</label>
							</div>
							<button type="submit" class="mt-4 rounded-md bg-rose-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-rose-700">
								Cancel visit
							</button>
						</form>
					{/if}
				</section>

				<section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
					<article class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<h3 class="text-base font-semibold leading-6 text-[var(--text-strong)]">Files</h3>
						<div class="mt-4">
							{#if selectedRequest.attachments.length}
								<div class="flex flex-wrap gap-3">
									{#each selectedRequest.attachments as attachment}
										<button
											type="button"
											class={`w-[88px] rounded-lg p-2 text-center shadow-sm transition ${selectedAttachment?.id === attachment.id ? 'bg-[var(--accent-soft)] ring-2 ring-[var(--accent-border)]' : 'bg-[var(--shell-panel-strong)] hover:bg-white'}`}
											onclick={() => viewAttachment(attachment)}
											title={attachment.fileName}
										>
											<span class="mx-auto flex h-[72px] w-[72px] items-center justify-center overflow-hidden rounded-md bg-white text-[var(--accent-text)] shadow-sm">
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
											<span class="mt-2 block truncate text-xs font-semibold leading-4 text-[var(--text-strong)]">{formatAttachmentName(attachment.fileName)}</span>
										</button>
									{/each}
								</div>

								{#if selectedAttachment}
									<div class="mt-5 border-t border-[var(--shell-border)] pt-5">
										<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
											<div>
												<h4 class="break-all text-base font-semibold text-[var(--text-strong)]">{selectedAttachment.fileName}</h4>
												<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedAttachment.contentType} · {formatAttachmentSize(selectedAttachment.sizeBytes)}</p>
											</div>
											<div class="flex flex-wrap gap-2">
												<a
													href={selectedAttachmentUrl}
													target="_blank"
													rel="noreferrer"
													class="inline-flex items-center gap-2 rounded-md bg-[var(--accent-soft)] px-3 py-2 text-sm font-semibold text-[var(--accent-text)]"
												>
													<ExternalLink size={15} />
													Open
												</a>
												<button
													type="button"
													class="inline-flex items-center rounded-md bg-white/80 px-3 py-2 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-white"
													onclick={closeAttachmentPreview}
												>
													Close
												</button>
											</div>
										</div>

										<div class="mt-4 overflow-hidden rounded-md bg-[var(--shell-panel-strong)]">
											{#if selectedAttachment.contentType.startsWith('image/')}
												<img src={selectedAttachmentUrl} alt={selectedAttachment.fileName} class="max-h-[560px] w-full object-contain" />
											{:else if selectedAttachmentCanPreview}
												<iframe src={selectedAttachmentUrl} title={selectedAttachment.fileName} class="h-[560px] w-full bg-white"></iframe>
											{:else}
												<div class="p-5 text-sm leading-6 text-[var(--text-muted)]">
													Open this file in a new tab to review it.
												</div>
											{/if}
										</div>
									</div>
								{/if}
							{:else}
								<p class="rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3 text-sm text-[var(--text-muted)]">No files attached.</p>
							{/if}
						</div>
					</article>

					<article class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
						<h3 class="text-base font-semibold leading-6 text-[var(--text-strong)]">Activity</h3>
						<div class="mt-4 space-y-3">
							{#each selectedRequest.timeline as event}
								<div class="rounded-lg bg-[var(--shell-panel-strong)] p-3">
									<div class="flex items-start justify-between gap-3">
										<div>
											<p class="text-sm font-semibold text-[var(--text-strong)]">{event.label}</p>
											<p class="mt-1 text-xs text-[var(--text-muted)]">{event.actor}</p>
										</div>
										<p class="shrink-0 text-xs text-[var(--text-muted)]">{formatSubmittedAt(event.occurredAtUtc)}</p>
									</div>
									{#if event.note}
										<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{event.note}</p>
									{:else if event.payload}
										<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{event.payload.serviceType} · {event.payload.siteName}</p>
									{/if}
								</div>
							{/each}
						</div>
					</article>
				</section>
			{:else}
				<div class="rounded-lg bg-white/90 p-8 text-center text-sm text-[var(--text-muted)] shadow-[var(--shell-shadow)]">
					Select a quote to work.
				</div>
			{/if}
		</div>
	</section>
</div>
