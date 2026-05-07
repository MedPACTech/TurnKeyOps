<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { formatCurrency } from '$lib/utils/format';
	import type { PageProps } from './$types';
	import {
		CalendarClock,
		CheckCircle2,
		ClipboardCheck,
		CircleDollarSign,
		ClipboardList,
		Hammer,
		Mail,
		MapPin,
		Package,
		PauseCircle,
		Phone,
		PlayCircle,
		RefreshCcw,
		Search,
		StickyNote,
		Truck,
		UserCheck,
		Wrench,
		XCircle
	} from 'lucide-svelte';

	type JobStatus = 'scheduled' | 'in-progress' | 'on-hold' | 'completed' | 'cancelled';
	type JobFilter = 'active' | JobStatus;
	type ConfirmationStatus = 'pending' | 'confirmed' | 'needs-reschedule';
	type OrderStatus = 'not-started' | 'requested' | 'ordered' | 'confirmed' | 'delivered';
	type ChecklistKey =
		| 'customer-confirmed'
		| 'site-access'
		| 'utility-locate'
		| 'base-material-ordered'
		| 'equipment-reserved'
		| 'concrete-ordered'
		| 'forms-reinforcement'
		| 'weather-check'
		| 'pour-confirmed'
		| 'cleanup-walkthrough';
	type JobPlanning = {
		customer: {
			confirmationStatus: ConfirmationStatus;
			confirmedAtUtc?: string;
			confirmationNote?: string;
			accessNotes?: string;
		};
		schedule: {
			targetDate: string;
			prepDate?: string;
			pourDate?: string;
			cleanupDate?: string;
		};
		materials: {
			baseMaterialStatus: OrderStatus;
			baseMaterialSupplier?: string;
			baseMaterialDeliveryDate?: string;
			baseMaterialDeliveryWindow?: string;
			reinforcementStatus: OrderStatus;
			reinforcementSupplier?: string;
			equipmentStatus: OrderStatus;
			equipmentVendor?: string;
			equipmentDeliveryDate?: string;
			equipmentDeliveryWindow?: string;
			concreteStatus: OrderStatus;
			concreteSupplier?: string;
			concreteDeliveryDate?: string;
			concreteDeliveryWindow?: string;
			concreteYards?: number;
			concreteMix?: string;
			pumpNeeded: boolean;
			notes?: string;
		};
		checklist: Record<ChecklistKey, boolean>;
		updatedAtUtc?: string;
		updatedBy?: string;
	};
	type JobActivity = {
		id: string;
		type: 'scheduled' | 'status-updated' | 'rescheduled' | 'planning-updated' | 'note';
		label: string;
		occurredAtUtc: string;
		actor: string;
		note?: string;
	};
	type JobRecord = {
		id: string;
		invoiceId: string;
		sourceRequestId: string;
		invoiceNumber: string;
		customerName: string;
		siteName: string;
		serviceSummary: string;
		serviceAddress: string;
		contactName: string;
		phone: string;
		email: string;
		amount: number;
		amountPaidAtScheduling: number;
		depositPercentRequired: number;
		scheduledDate: string;
		windowStart: string;
		windowEnd: string;
		crew: string;
		notes?: string;
		status: JobStatus;
		scheduledAtUtc: string;
		scheduledBy: string;
		updatedAtUtc: string;
		completedAtUtc?: string;
		cancelledAtUtc?: string;
		holdReason?: string;
		planning: JobPlanning;
		activity: JobActivity[];
	};
	type ScheduleReadyJob = {
		invoiceId: string;
		invoiceNumber: string;
		customerName: string;
		siteName: string;
		serviceSummary: string;
		serviceAddress: string;
		contactName: string;
		phone: string;
		email: string;
		amount: number;
		amountPaid: number;
		balanceDue: number;
		depositPercentRequired: number;
		paidPercent: number;
	};
	type JobsPageData = PageProps['data'] & {
		jobs?: JobRecord[];
		scheduleReadyJobs?: ScheduleReadyJob[];
		selectedJobId?: string;
	};

	let { data, form }: PageProps = $props();
	const pageData = $derived(data as JobsPageData);
	const jobs = $derived(pageData.jobs ?? []);
	const scheduleReadyJobs = $derived(pageData.scheduleReadyJobs ?? []);
	const actionMessage = $derived((form as { jobActionMessage?: string } | null)?.jobActionMessage);
	const formSelectedJobId = $derived((form as { selectedJobId?: string } | null)?.selectedJobId);

	let activeFilter = $state<JobFilter>('active');
	let searchQuery = $state('');
	let selectedJobId = $state('');
	let scheduleFormJobId = $state('');
	let scheduleDate = $state('');
	let scheduleWindowStart = $state('08:00');
	let scheduleWindowEnd = $state('12:00');
	let scheduleCrew = $state('Production crew');
	let scheduleNote = $state('');
	let statusNote = $state('');
	let jobNote = $state('');
	let planningFormJobId = $state('');
	let createJobDrawerOpen = $state(false);
	let customerConfirmationStatus = $state<ConfirmationStatus>('pending');
	let customerConfirmationNote = $state('');
	let accessNotes = $state('');
	let targetDate = $state('');
	let prepDate = $state('');
	let pourDate = $state('');
	let cleanupDate = $state('');
	let baseMaterialStatus = $state<OrderStatus>('not-started');
	let baseMaterialSupplier = $state('');
	let baseMaterialDeliveryDate = $state('');
	let baseMaterialDeliveryWindow = $state('');
	let reinforcementStatus = $state<OrderStatus>('not-started');
	let reinforcementSupplier = $state('');
	let equipmentStatus = $state<OrderStatus>('not-started');
	let equipmentVendor = $state('');
	let equipmentDeliveryDate = $state('');
	let equipmentDeliveryWindow = $state('');
	let concreteStatus = $state<OrderStatus>('not-started');
	let concreteSupplier = $state('');
	let concreteDeliveryDate = $state('');
	let concreteDeliveryWindow = $state('');
	let concreteYards = $state('');
	let concreteMix = $state('');
	let concretePumpNeeded = $state(false);
	let materialNotes = $state('');
	let selectedChecklistKeys = $state<ChecklistKey[]>([]);

	const activeStatusSet = new Set<JobStatus>(['scheduled', 'in-progress', 'on-hold']);
	const todayInput = new Date().toISOString().slice(0, 10);
	const tomorrowInput = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
	const orderStatusOptions: { value: OrderStatus; label: string }[] = [
		{ value: 'not-started', label: 'Not started' },
		{ value: 'requested', label: 'Requested' },
		{ value: 'ordered', label: 'Ordered' },
		{ value: 'confirmed', label: 'Confirmed' },
		{ value: 'delivered', label: 'Delivered' }
	];
	const confirmationStatusOptions: { value: ConfirmationStatus; label: string }[] = [
		{ value: 'pending', label: 'Pending' },
		{ value: 'confirmed', label: 'Confirmed' },
		{ value: 'needs-reschedule', label: 'Needs new time' }
	];
	const confirmationMeta: Record<ConfirmationStatus, { label: string; chipClass: string }> = {
		pending: { label: 'Customer pending', chipClass: 'border-amber-200 bg-amber-50 text-amber-700' },
		confirmed: { label: 'Customer confirmed', chipClass: 'border-emerald-200 bg-emerald-50 text-emerald-700' },
		'needs-reschedule': { label: 'Needs new time', chipClass: 'border-rose-200 bg-rose-50 text-rose-700' }
	};
	const orderStatusMeta: Record<OrderStatus, { label: string; chipClass: string }> = {
		'not-started': { label: 'Not started', chipClass: 'border-slate-200 bg-slate-100 text-slate-700' },
		requested: { label: 'Requested', chipClass: 'border-sky-200 bg-sky-50 text-sky-700' },
		ordered: { label: 'Ordered', chipClass: 'border-violet-200 bg-violet-50 text-violet-700' },
		confirmed: { label: 'Confirmed', chipClass: 'border-emerald-200 bg-emerald-50 text-emerald-700' },
		delivered: { label: 'Delivered', chipClass: 'border-slate-300 bg-white text-slate-700' }
	};
	const checklistItems: { key: ChecklistKey; label: string }[] = [
		{ key: 'customer-confirmed', label: 'Customer approved window' },
		{ key: 'site-access', label: 'Access and staging confirmed' },
		{ key: 'utility-locate', label: 'Utility locate handled' },
		{ key: 'base-material-ordered', label: 'Rock/gravel delivery scheduled' },
		{ key: 'equipment-reserved', label: 'Equipment reserved' },
		{ key: 'concrete-ordered', label: 'Concrete ordered' },
		{ key: 'forms-reinforcement', label: 'Forms and reinforcement ready' },
		{ key: 'weather-check', label: 'Weather checked' },
		{ key: 'pour-confirmed', label: 'Pour window confirmed' },
		{ key: 'cleanup-walkthrough', label: 'Cleanup and walkthrough planned' }
	];
	const statusMeta: Record<JobStatus, { label: string; chipClass: string; icon: typeof CalendarClock }> = {
		scheduled: {
			label: 'Scheduled',
			chipClass: 'border-sky-200 bg-sky-50 text-sky-700',
			icon: CalendarClock
		},
		'in-progress': {
			label: 'In progress',
			chipClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
			icon: PlayCircle
		},
		'on-hold': {
			label: 'On hold',
			chipClass: 'border-amber-200 bg-amber-50 text-amber-700',
			icon: PauseCircle
		},
		completed: {
			label: 'Completed',
			chipClass: 'border-slate-200 bg-slate-100 text-slate-700',
			icon: CheckCircle2
		},
		cancelled: {
			label: 'Cancelled',
			chipClass: 'border-rose-200 bg-rose-50 text-rose-700',
			icon: XCircle
		}
	};
	const filters = $derived(
		[
			{ value: 'active' as const, label: 'Active', count: jobs.filter((job) => activeStatusSet.has(job.status)).length },
			{ value: 'scheduled' as const, label: 'Scheduled', count: jobs.filter((job) => job.status === 'scheduled').length },
			{ value: 'in-progress' as const, label: 'Running', count: jobs.filter((job) => job.status === 'in-progress').length },
			{ value: 'on-hold' as const, label: 'Holds', count: jobs.filter((job) => job.status === 'on-hold').length },
			{ value: 'completed' as const, label: 'Done', count: jobs.filter((job) => job.status === 'completed').length }
		]
	);
	const matchesFilter = (job: JobRecord, filter: JobFilter) =>
		filter === 'active' ? activeStatusSet.has(job.status) : job.status === filter;
	const filteredJobs = $derived(
		jobs.filter((job) => {
			const search = searchQuery.trim().toLowerCase();
			const haystack = [
				job.customerName,
				job.siteName,
				job.serviceSummary,
				job.serviceAddress,
				job.invoiceNumber,
				job.crew,
				statusMeta[job.status].label
			]
				.join(' ')
				.toLowerCase();
			return matchesFilter(job, activeFilter) && (!search || haystack.includes(search));
		})
	);
	const selectedJob = $derived(
		jobs.find((job) => job.id === selectedJobId) ?? filteredJobs[0] ?? jobs[0] ?? null
	);
	const checklistCompleteCount = (job: JobRecord) => checklistItems.filter((item) => job.planning.checklist[item.key]).length;
	const checklistProgressPercent = (job: JobRecord) =>
		Math.round((checklistCompleteCount(job) / checklistItems.length) * 100);
	const isOrderCommitted = (status: OrderStatus) => status === 'ordered' || status === 'confirmed' || status === 'delivered';
	const activeJobs = $derived(jobs.filter((job) => activeStatusSet.has(job.status)));
	const heldJobs = $derived(jobs.filter((job) => job.status === 'on-hold'));
	const customerConfirmedJobs = $derived(
		activeJobs.filter((job) => job.planning.customer.confirmationStatus === 'confirmed')
	);
	const pourReadyJobs = $derived(
		activeJobs.filter(
			(job) =>
				job.planning.customer.confirmationStatus === 'confirmed' &&
				isOrderCommitted(job.planning.materials.concreteStatus) &&
				job.planning.checklist['pour-confirmed']
		)
	);
	const activeValue = $derived(activeJobs.reduce((sum, job) => sum + job.amount, 0));
	const metrics = $derived([
		{ label: 'Active jobs', value: String(activeJobs.length), icon: '🏗️' },
		{ label: 'Customer confirmed', value: String(customerConfirmedJobs.length), icon: '✓' },
		{ label: 'Pour ready', value: String(pourReadyJobs.length), icon: '✓' },
		{ label: 'On hold', value: String(heldJobs.length), icon: '!' },
		{ label: 'Active value', value: formatCurrency(activeValue), icon: '💵' }
	]);

	$effect(() => {
		const incomingSelectedJobId = formSelectedJobId || pageData.selectedJobId || '';
		if (incomingSelectedJobId && selectedJobId !== incomingSelectedJobId) {
			selectedJobId = incomingSelectedJobId;
		}
	});

	$effect(() => {
		if (!selectedJob || scheduleFormJobId === selectedJob.id) return;
		scheduleFormJobId = selectedJob.id;
		scheduleDate = selectedJob.scheduledDate;
		scheduleWindowStart = selectedJob.windowStart;
		scheduleWindowEnd = selectedJob.windowEnd;
		scheduleCrew = selectedJob.crew;
		scheduleNote = selectedJob.notes ?? '';
		statusNote = '';
		jobNote = '';
	});

	$effect(() => {
		if (!selectedJob || planningFormJobId === selectedJob.id) return;
		const planning = selectedJob.planning;
		planningFormJobId = selectedJob.id;
		customerConfirmationStatus = planning.customer.confirmationStatus;
		customerConfirmationNote = planning.customer.confirmationNote ?? '';
		accessNotes = planning.customer.accessNotes ?? '';
		targetDate = planning.schedule.targetDate || selectedJob.scheduledDate;
		prepDate = planning.schedule.prepDate ?? '';
		pourDate = planning.schedule.pourDate ?? '';
		cleanupDate = planning.schedule.cleanupDate ?? '';
		baseMaterialStatus = planning.materials.baseMaterialStatus;
		baseMaterialSupplier = planning.materials.baseMaterialSupplier ?? '';
		baseMaterialDeliveryDate = planning.materials.baseMaterialDeliveryDate ?? '';
		baseMaterialDeliveryWindow = planning.materials.baseMaterialDeliveryWindow ?? '';
		reinforcementStatus = planning.materials.reinforcementStatus;
		reinforcementSupplier = planning.materials.reinforcementSupplier ?? '';
		equipmentStatus = planning.materials.equipmentStatus;
		equipmentVendor = planning.materials.equipmentVendor ?? '';
		equipmentDeliveryDate = planning.materials.equipmentDeliveryDate ?? '';
		equipmentDeliveryWindow = planning.materials.equipmentDeliveryWindow ?? '';
		concreteStatus = planning.materials.concreteStatus;
		concreteSupplier = planning.materials.concreteSupplier ?? '';
		concreteDeliveryDate = planning.materials.concreteDeliveryDate ?? '';
		concreteDeliveryWindow = planning.materials.concreteDeliveryWindow ?? '';
		concreteYards = planning.materials.concreteYards ? String(planning.materials.concreteYards) : '';
		concreteMix = planning.materials.concreteMix ?? '';
		concretePumpNeeded = planning.materials.pumpNeeded;
		materialNotes = planning.materials.notes ?? '';
		selectedChecklistKeys = checklistItems
			.filter((item) => planning.checklist[item.key])
			.map((item) => item.key);
	});

	const toggleChecklistKey = (key: ChecklistKey, checked: boolean) => {
		selectedChecklistKeys = checked
			? [...new Set([...selectedChecklistKeys, key])]
			: selectedChecklistKeys.filter((item) => item !== key);
	};

	const closeCreateJobDrawer = () => {
		createJobDrawerOpen = false;
	};

	const formatDate = (value: string) =>
		new Date(`${value}T12:00:00`).toLocaleDateString('en-US', {
			month: 'short',
			day: 'numeric',
			year: 'numeric'
		});

	const formatDateTime = (value: string) =>
		new Date(value).toLocaleString('en-US', {
			month: 'short',
			day: 'numeric',
			hour: 'numeric',
			minute: '2-digit'
		});

	const formatTime = (value: string) => {
		const [hoursText = '', minutesText = ''] = value.split(':');
		const hours = Number(hoursText);
		const minutes = Number(minutesText);
		if (Number.isNaN(hours) || Number.isNaN(minutes)) return value;
		return new Date(2026, 0, 1, hours, minutes).toLocaleTimeString('en-US', {
			hour: 'numeric',
			minute: '2-digit'
		});
	};

	const statusActionClass = (status: JobStatus) =>
		`inline-flex min-h-10 items-center justify-center gap-2 rounded-md border px-3 py-2 text-sm font-semibold transition ${statusMeta[status].chipClass} hover:brightness-95`;
</script>

{#snippet workSurface()}
	<div class="space-y-4">
	<section class="grid gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
			<aside class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
				<div class="flex flex-col gap-3">
					<div>
						<p class="text-base font-semibold text-[var(--text-strong)]">Production queue</p>
						<p class="mt-1 text-sm leading-5 text-[var(--text-muted)]">{filteredJobs.length} visible job{filteredJobs.length === 1 ? '' : 's'}</p>
					</div>
					<div class="flex gap-2">
						<button
							type="button"
							class="inline-flex min-h-10 flex-1 items-center justify-center gap-2 rounded-md bg-[var(--accent-solid)] px-3 py-2 text-sm font-semibold text-white transition hover:bg-[var(--accent-solid-hover)] disabled:cursor-not-allowed disabled:bg-slate-300"
							disabled={!scheduleReadyJobs.length}
							onclick={() => (createJobDrawerOpen = true)}
						>
							<Hammer class="h-4 w-4" aria-hidden="true" />
							Create job
							{#if scheduleReadyJobs.length}
								<span class="rounded-full bg-white/20 px-1.5 py-0.5 text-xs">{scheduleReadyJobs.length}</span>
							{/if}
						</button>
						<a
							href="/bdr/admin/calendar"
							class="inline-flex h-10 w-10 items-center justify-center rounded-md border border-[var(--shell-border)] bg-white text-[var(--text-base)] transition hover:bg-[var(--shell-panel-strong)]"
							aria-label="Open calendar"
							title="Open calendar"
						>
							<CalendarClock class="h-5 w-5" aria-hidden="true" />
						</a>
					</div>
				</div>

			<label class="mt-4 flex items-center gap-2 rounded-md border border-[var(--shell-border)] bg-white px-3 py-2">
				<Search class="h-4 w-4 text-[var(--text-muted)]" aria-hidden="true" />
				<span class="sr-only">Search jobs</span>
				<input
					bind:value={searchQuery}
					class="min-w-0 flex-1 bg-transparent text-sm text-[var(--text-strong)] outline-none placeholder:text-[var(--text-muted)]"
					placeholder="Search customer, crew, invoice"
				/>
			</label>

			<div class="mt-4 flex flex-wrap gap-2">
				{#each filters as filter}
					<button
						type="button"
						class={`inline-flex items-center gap-2 rounded-md px-3 py-2 text-xs font-semibold transition ${
							activeFilter === filter.value
								? 'bg-[var(--accent-solid)] text-white'
								: 'bg-[var(--shell-panel-strong)] text-[var(--text-muted)] hover:bg-white'
						}`}
						onclick={() => (activeFilter = filter.value)}
					>
						<span>{filter.label}</span>
						<span class="rounded-full bg-white/25 px-1.5 py-0.5">{filter.count}</span>
					</button>
				{/each}
			</div>

			<div class="mt-4 space-y-3">
				{#if filteredJobs.length}
					{#each filteredJobs as job}
						{@const StatusIcon = statusMeta[job.status].icon}
						<button
							type="button"
							class={`w-full rounded-lg border p-3 text-left transition ${
								selectedJob?.id === job.id
									? 'border-[var(--accent-solid)] bg-orange-50 shadow-sm'
									: 'border-[var(--shell-border)] bg-white hover:border-slate-300 hover:bg-[var(--shell-panel-strong)]'
							}`}
							onclick={() => (selectedJobId = job.id)}
						>
							<div class="flex items-start justify-between gap-3">
								<div class="min-w-0">
									<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{job.siteName || job.customerName}</p>
									<p class="mt-1 truncate text-xs text-[var(--text-muted)]">{job.customerName} · {job.invoiceNumber}</p>
								</div>
								<span class={`inline-flex shrink-0 items-center gap-1 rounded-full border px-2 py-1 text-[0.68rem] font-semibold ${statusMeta[job.status].chipClass}`}>
									<StatusIcon class="h-3.5 w-3.5" aria-hidden="true" />
									{statusMeta[job.status].label}
								</span>
							</div>
							<div class="mt-3 grid grid-cols-2 gap-2 text-xs text-[var(--text-muted)]">
								<span>{formatDate(job.scheduledDate)}</span>
								<span class="text-right">{formatTime(job.windowStart)} - {formatTime(job.windowEnd)}</span>
								<span class="truncate">{job.crew}</span>
								<span class="text-right font-semibold text-[var(--text-strong)]">{formatCurrency(job.amount)}</span>
							</div>
							<div class="mt-3 flex items-center justify-between gap-3">
								<span class={`inline-flex items-center rounded-full border px-2 py-1 text-[0.68rem] font-semibold ${confirmationMeta[job.planning.customer.confirmationStatus].chipClass}`}>
									{confirmationMeta[job.planning.customer.confirmationStatus].label}
								</span>
								<span class="text-xs font-semibold text-[var(--text-muted)]">{checklistProgressPercent(job)}% planned</span>
							</div>
						</button>
					{/each}
				{:else}
					<div class="rounded-lg border border-dashed border-[var(--shell-border)] bg-white p-4 text-sm leading-6 text-[var(--text-muted)]">
						No jobs match the current filter.
					</div>
				{/if}
			</div>
		</aside>

		{#if selectedJob}
			<section class="space-y-4">
				{#if actionMessage}
					<div class="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-800">
						{actionMessage}
					</div>
				{/if}

				<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
					<div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
						<div class="min-w-0">
							<div class="flex flex-wrap items-center gap-2">
								<span class={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold ${statusMeta[selectedJob.status].chipClass}`}>
									<CalendarClock class="h-4 w-4" aria-hidden="true" />
									{statusMeta[selectedJob.status].label}
								</span>
								<span class="rounded-full bg-[var(--shell-panel-strong)] px-2.5 py-1 text-xs font-semibold text-[var(--text-muted)]">{selectedJob.invoiceNumber}</span>
								<span class={`rounded-full border px-2.5 py-1 text-xs font-semibold ${confirmationMeta[selectedJob.planning.customer.confirmationStatus].chipClass}`}>
									{confirmationMeta[selectedJob.planning.customer.confirmationStatus].label}
								</span>
							</div>
							<h2 class="mt-3 text-2xl font-semibold leading-8 text-[var(--text-strong)]">{selectedJob.siteName || selectedJob.customerName}</h2>
							<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{selectedJob.serviceSummary}</p>
						</div>
						<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-right">
							<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Job value</p>
							<p class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{formatCurrency(selectedJob.amount)}</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">{checklistCompleteCount(selectedJob)}/{checklistItems.length} planning checks</p>
						</div>
					</div>

					<div class="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
						<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
							<CalendarClock class="h-4 w-4 text-sky-700" aria-hidden="true" />
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatDate(selectedJob.scheduledDate)}</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">{formatTime(selectedJob.windowStart)} - {formatTime(selectedJob.windowEnd)}</p>
						</div>
						<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
							<Hammer class="h-4 w-4 text-emerald-700" aria-hidden="true" />
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedJob.crew}</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">Assigned crew</p>
						</div>
						<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
							<MapPin class="h-4 w-4 text-amber-700" aria-hidden="true" />
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{selectedJob.customerName}</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{selectedJob.serviceAddress}</p>
						</div>
						<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
							<CircleDollarSign class="h-4 w-4 text-slate-700" aria-hidden="true" />
							<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{formatCurrency(selectedJob.amountPaidAtScheduling)}</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">Collected at release</p>
						</div>
					</div>
				</div>

				<div class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
					<div class="space-y-4">
						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
								<div class="flex items-center gap-2">
									<ClipboardCheck class="h-5 w-5 text-emerald-700" aria-hidden="true" />
									<h3 class="text-base font-semibold text-[var(--text-strong)]">Job planning</h3>
								</div>
								<span class="rounded-full bg-[var(--shell-panel-strong)] px-2.5 py-1 text-xs font-semibold text-[var(--text-muted)]">
									{checklistProgressPercent(selectedJob)}% ready
								</span>
							</div>

							<form method="POST" action="?/updatePlanning" class="mt-4 space-y-5">
								<input type="hidden" name="jobId" value={selectedJob.id} />

								<div class="grid gap-3 md:grid-cols-2">
									<label class="space-y-1">
										<span class="flex items-center gap-1.5 text-xs font-semibold text-[var(--text-muted)]">
											<UserCheck class="h-3.5 w-3.5" aria-hidden="true" />
											Customer confirmation
										</span>
										<select name="customerConfirmationStatus" bind:value={customerConfirmationStatus} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]">
											{#each confirmationStatusOptions as option}
												<option value={option.value}>{option.label}</option>
											{/each}
										</select>
									</label>
									<label class="space-y-1">
										<span class="text-xs font-semibold text-[var(--text-muted)]">Target job date</span>
										<input name="targetDate" type="date" bind:value={targetDate} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
									</label>
									<label class="space-y-1">
										<span class="text-xs font-semibold text-[var(--text-muted)]">Customer note</span>
										<input name="customerConfirmationNote" bind:value={customerConfirmationNote} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
									</label>
									<label class="space-y-1">
										<span class="text-xs font-semibold text-[var(--text-muted)]">Access / staging</span>
										<input name="accessNotes" bind:value={accessNotes} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
									</label>
								</div>

								<div class="grid gap-3 md:grid-cols-3">
									<label class="space-y-1">
										<span class="text-xs font-semibold text-[var(--text-muted)]">Prep date</span>
										<input name="prepDate" type="date" bind:value={prepDate} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
									</label>
									<label class="space-y-1">
										<span class="text-xs font-semibold text-[var(--text-muted)]">Pour date</span>
										<input name="pourDate" type="date" bind:value={pourDate} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
									</label>
									<label class="space-y-1">
										<span class="text-xs font-semibold text-[var(--text-muted)]">Cleanup date</span>
										<input name="cleanupDate" type="date" bind:value={cleanupDate} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
									</label>
								</div>

								<div class="grid gap-3 xl:grid-cols-2">
									<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
										<div class="flex items-center gap-2">
											<Package class="h-4 w-4 text-amber-700" aria-hidden="true" />
											<p class="text-sm font-semibold text-[var(--text-strong)]">Rock / gravel</p>
										</div>
										<div class="mt-3 grid gap-2 md:grid-cols-2">
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Status</span>
												<select name="baseMaterialStatus" bind:value={baseMaterialStatus} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]">
													{#each orderStatusOptions as option}
														<option value={option.value}>{option.label}</option>
													{/each}
												</select>
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Supplier</span>
												<input name="baseMaterialSupplier" bind:value={baseMaterialSupplier} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Delivery date</span>
												<input name="baseMaterialDeliveryDate" type="date" bind:value={baseMaterialDeliveryDate} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Delivery window</span>
												<input name="baseMaterialDeliveryWindow" bind:value={baseMaterialDeliveryWindow} placeholder="7-9 AM" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
										</div>
									</div>

									<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
										<div class="flex items-center gap-2">
											<Wrench class="h-4 w-4 text-sky-700" aria-hidden="true" />
											<p class="text-sm font-semibold text-[var(--text-strong)]">Equipment</p>
										</div>
										<div class="mt-3 grid gap-2 md:grid-cols-2">
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Status</span>
												<select name="equipmentStatus" bind:value={equipmentStatus} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]">
													{#each orderStatusOptions as option}
														<option value={option.value}>{option.label}</option>
													{/each}
												</select>
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Vendor</span>
												<input name="equipmentVendor" bind:value={equipmentVendor} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Delivery date</span>
												<input name="equipmentDeliveryDate" type="date" bind:value={equipmentDeliveryDate} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Delivery window</span>
												<input name="equipmentDeliveryWindow" bind:value={equipmentDeliveryWindow} placeholder="Before prep" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
										</div>
									</div>

									<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
										<div class="flex items-center gap-2">
											<Truck class="h-4 w-4 text-emerald-700" aria-hidden="true" />
											<p class="text-sm font-semibold text-[var(--text-strong)]">Concrete</p>
										</div>
										<div class="mt-3 grid gap-2 md:grid-cols-2">
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Status</span>
												<select name="concreteStatus" bind:value={concreteStatus} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]">
													{#each orderStatusOptions as option}
														<option value={option.value}>{option.label}</option>
													{/each}
												</select>
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Supplier</span>
												<input name="concreteSupplier" bind:value={concreteSupplier} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Truck date</span>
												<input name="concreteDeliveryDate" type="date" bind:value={concreteDeliveryDate} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Truck window</span>
												<input name="concreteDeliveryWindow" bind:value={concreteDeliveryWindow} placeholder="10-11 AM" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Yards</span>
												<input name="concreteYards" inputmode="decimal" bind:value={concreteYards} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Mix</span>
												<input name="concreteMix" bind:value={concreteMix} placeholder="4000 PSI broom" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="flex items-center gap-2 md:col-span-2">
												<input name="pumpNeeded" type="checkbox" bind:checked={concretePumpNeeded} class="h-4 w-4 rounded border-[var(--shell-border)]" />
												<span class="text-sm font-semibold text-[var(--text-strong)]">Pump or buggy needed</span>
											</label>
										</div>
									</div>

									<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
										<div class="flex items-center gap-2">
											<Hammer class="h-4 w-4 text-violet-700" aria-hidden="true" />
											<p class="text-sm font-semibold text-[var(--text-strong)]">Reinforcement</p>
										</div>
										<div class="mt-3 grid gap-2 md:grid-cols-2">
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Status</span>
												<select name="reinforcementStatus" bind:value={reinforcementStatus} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]">
													{#each orderStatusOptions as option}
														<option value={option.value}>{option.label}</option>
													{/each}
												</select>
											</label>
											<label class="space-y-1">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Supplier</span>
												<input name="reinforcementSupplier" bind:value={reinforcementSupplier} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
											</label>
											<label class="space-y-1 md:col-span-2">
												<span class="text-xs font-semibold text-[var(--text-muted)]">Material notes</span>
												<textarea name="materialNotes" bind:value={materialNotes} class="min-h-20 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6 text-[var(--text-strong)]"></textarea>
											</label>
										</div>
									</div>
								</div>

								<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
									<p class="text-sm font-semibold text-[var(--text-strong)]">Concrete checklist</p>
									<div class="mt-3 grid gap-2 md:grid-cols-2">
										{#each checklistItems as item}
											<label class="flex min-h-10 items-center gap-2 rounded-md bg-[var(--shell-panel-strong)] px-3 py-2 text-sm text-[var(--text-strong)]">
												<input
													name="checklist"
													type="checkbox"
													value={item.key}
													checked={selectedChecklistKeys.includes(item.key)}
													onchange={(event) => toggleChecklistKey(item.key, (event.currentTarget as HTMLInputElement).checked)}
													class="h-4 w-4 rounded border-[var(--shell-border)]"
												/>
												<span>{item.label}</span>
											</label>
										{/each}
									</div>
								</div>

								<button type="submit" class="inline-flex min-h-11 items-center justify-center gap-2 rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--accent-solid-hover)]">
									<ClipboardCheck class="h-4 w-4" aria-hidden="true" />
									Save job plan
								</button>
							</form>
						</div>

						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex items-center gap-2">
								<ClipboardList class="h-5 w-5 text-[var(--accent-solid)]" aria-hidden="true" />
								<h3 class="text-base font-semibold text-[var(--text-strong)]">Run controls</h3>
							</div>
							<textarea
								bind:value={statusNote}
								class="mt-4 min-h-20 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6 text-[var(--text-strong)] outline-none transition focus:border-[var(--accent-solid)]"
								placeholder="Optional status note for crew, weather, access, or customer context"
							></textarea>
							<div class="mt-3 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
								<form method="POST" action="?/updateStatus">
									<input type="hidden" name="jobId" value={selectedJob.id} />
									<input type="hidden" name="status" value="in-progress" />
									<input type="hidden" name="statusNote" value={statusNote} />
									<button type="submit" class={statusActionClass('in-progress')}>
										<PlayCircle class="h-4 w-4" aria-hidden="true" />
										Start
									</button>
								</form>
								<form method="POST" action="?/updateStatus">
									<input type="hidden" name="jobId" value={selectedJob.id} />
									<input type="hidden" name="status" value="on-hold" />
									<input type="hidden" name="statusNote" value={statusNote} />
									<button type="submit" class={statusActionClass('on-hold')}>
										<PauseCircle class="h-4 w-4" aria-hidden="true" />
										Hold
									</button>
								</form>
								<form method="POST" action="?/updateStatus">
									<input type="hidden" name="jobId" value={selectedJob.id} />
									<input type="hidden" name="status" value="completed" />
									<input type="hidden" name="statusNote" value={statusNote} />
									<button type="submit" class={statusActionClass('completed')}>
										<CheckCircle2 class="h-4 w-4" aria-hidden="true" />
										Complete
									</button>
								</form>
								<form method="POST" action="?/updateStatus">
									<input type="hidden" name="jobId" value={selectedJob.id} />
									<input type="hidden" name="status" value="cancelled" />
									<input type="hidden" name="statusNote" value={statusNote} />
									<button type="submit" class={statusActionClass('cancelled')}>
										<XCircle class="h-4 w-4" aria-hidden="true" />
										Cancel
									</button>
								</form>
							</div>
						</div>

						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex items-center gap-2">
								<RefreshCcw class="h-5 w-5 text-sky-700" aria-hidden="true" />
								<h3 class="text-base font-semibold text-[var(--text-strong)]">Schedule and crew</h3>
							</div>
							<form method="POST" action="?/rescheduleJob" class="mt-4 grid gap-3 md:grid-cols-2">
								<input type="hidden" name="jobId" value={selectedJob.id} />
								<label class="space-y-1">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Production date</span>
									<input name="scheduledDate" type="date" bind:value={scheduleDate} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
								</label>
								<label class="space-y-1">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Crew</span>
									<input name="crew" bind:value={scheduleCrew} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
								</label>
								<label class="space-y-1">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Window start</span>
									<input name="windowStart" type="time" bind:value={scheduleWindowStart} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
								</label>
								<label class="space-y-1">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Window end</span>
									<input name="windowEnd" type="time" bind:value={scheduleWindowEnd} class="h-11 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
								</label>
								<label class="space-y-1 md:col-span-2">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Schedule note</span>
									<textarea name="scheduleNote" bind:value={scheduleNote} class="min-h-20 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6 text-[var(--text-strong)]"></textarea>
								</label>
								<div class="md:col-span-2">
									<button type="submit" class="inline-flex min-h-11 items-center justify-center gap-2 rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--accent-solid-hover)]">
										<RefreshCcw class="h-4 w-4" aria-hidden="true" />
										Update schedule
									</button>
								</div>
							</form>
						</div>
					</div>

					<aside class="space-y-4">
						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex items-center gap-2">
								<ClipboardCheck class="h-4 w-4 text-emerald-700" aria-hidden="true" />
								<h3 class="text-base font-semibold text-[var(--text-strong)]">Plan health</h3>
							</div>
							<div class="mt-4 space-y-2">
								<div class="flex items-center justify-between gap-3 rounded-md bg-[var(--shell-panel-strong)] px-3 py-2">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Customer</span>
									<span class={`rounded-full border px-2 py-1 text-[0.68rem] font-semibold ${confirmationMeta[selectedJob.planning.customer.confirmationStatus].chipClass}`}>
										{confirmationMeta[selectedJob.planning.customer.confirmationStatus].label}
									</span>
								</div>
								<div class="flex items-center justify-between gap-3 rounded-md bg-[var(--shell-panel-strong)] px-3 py-2">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Rock / gravel</span>
									<span class={`rounded-full border px-2 py-1 text-[0.68rem] font-semibold ${orderStatusMeta[selectedJob.planning.materials.baseMaterialStatus].chipClass}`}>
										{orderStatusMeta[selectedJob.planning.materials.baseMaterialStatus].label}
									</span>
								</div>
								<div class="flex items-center justify-between gap-3 rounded-md bg-[var(--shell-panel-strong)] px-3 py-2">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Equipment</span>
									<span class={`rounded-full border px-2 py-1 text-[0.68rem] font-semibold ${orderStatusMeta[selectedJob.planning.materials.equipmentStatus].chipClass}`}>
										{orderStatusMeta[selectedJob.planning.materials.equipmentStatus].label}
									</span>
								</div>
								<div class="flex items-center justify-between gap-3 rounded-md bg-[var(--shell-panel-strong)] px-3 py-2">
									<span class="text-xs font-semibold text-[var(--text-muted)]">Concrete</span>
									<span class={`rounded-full border px-2 py-1 text-[0.68rem] font-semibold ${orderStatusMeta[selectedJob.planning.materials.concreteStatus].chipClass}`}>
										{orderStatusMeta[selectedJob.planning.materials.concreteStatus].label}
									</span>
								</div>
							</div>
							<div class="mt-4 rounded-lg border border-[var(--shell-border)] bg-white p-3">
								<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Planning progress</p>
								<div class="mt-3 h-2 overflow-hidden rounded-full bg-[var(--shell-panel-strong)]">
									<div class="h-full rounded-full bg-[var(--accent-solid)]" style={`width: ${checklistProgressPercent(selectedJob)}%`}></div>
								</div>
								<p class="mt-2 text-xs text-[var(--text-muted)]">
									{checklistCompleteCount(selectedJob)} of {checklistItems.length} checks complete
								</p>
							</div>
						</div>

						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex items-center gap-2">
								<Phone class="h-4 w-4 text-[var(--accent-solid)]" aria-hidden="true" />
								<h3 class="text-base font-semibold text-[var(--text-strong)]">Customer contact</h3>
							</div>
							<p class="mt-3 text-sm font-semibold text-[var(--text-strong)]">{selectedJob.contactName}</p>
							<p class="mt-2 flex items-center gap-2 text-sm text-[var(--text-muted)]">
								<Phone class="h-4 w-4" aria-hidden="true" />
								{selectedJob.phone}
							</p>
							<p class="mt-2 flex items-center gap-2 break-all text-sm text-[var(--text-muted)]">
								<Mail class="h-4 w-4 shrink-0" aria-hidden="true" />
								{selectedJob.email}
							</p>
						</div>

						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<div class="flex items-center gap-2">
								<StickyNote class="h-4 w-4 text-amber-700" aria-hidden="true" />
								<h3 class="text-base font-semibold text-[var(--text-strong)]">Job notes</h3>
							</div>
							{#if selectedJob.notes}
								<p class="mt-3 rounded-md bg-[var(--shell-panel-strong)] p-3 text-sm leading-6 text-[var(--text-muted)]">{selectedJob.notes}</p>
							{/if}
							<form method="POST" action="?/addJobNote" class="mt-3 space-y-3">
								<input type="hidden" name="jobId" value={selectedJob.id} />
								<textarea
									name="jobNote"
									bind:value={jobNote}
									class="min-h-24 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm leading-6 text-[var(--text-strong)]"
									placeholder="Add a production note"
								></textarea>
								<button type="submit" class="inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-[var(--shell-border)] bg-white px-3 py-2 text-sm font-semibold text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]">
									<StickyNote class="h-4 w-4" aria-hidden="true" />
									Save note
								</button>
							</form>
						</div>

						<div class="rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)]">
							<h3 class="text-base font-semibold text-[var(--text-strong)]">Activity</h3>
							<div class="mt-4 space-y-3">
								{#each selectedJob.activity.slice(0, 6) as item}
									<div class="border-l-2 border-[var(--shell-border)] pl-3">
										<p class="text-sm font-semibold text-[var(--text-strong)]">{item.label}</p>
										<p class="mt-1 text-xs text-[var(--text-muted)]">{formatDateTime(item.occurredAtUtc)} · {item.actor}</p>
										{#if item.note}
											<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{item.note}</p>
										{/if}
									</div>
								{/each}
							</div>
						</div>
					</aside>
				</div>
			</section>
		{:else}
			<section class="rounded-lg border border-dashed border-[var(--shell-border)] bg-white/90 p-8 text-center shadow-[var(--shell-shadow)]">
				<Hammer class="mx-auto h-10 w-10 text-[var(--text-muted)]" aria-hidden="true" />
				<h2 class="mt-3 text-xl font-semibold text-[var(--text-strong)]">No production jobs yet</h2>
				<p class="mx-auto mt-2 max-w-md text-sm leading-6 text-[var(--text-muted)]">
					Schedule a deposit-cleared invoice to create the first production job.
				</p>
				<div class="mt-4 flex flex-wrap justify-center gap-2">
					{#if scheduleReadyJobs.length}
						<button
							type="button"
							class="inline-flex min-h-11 items-center justify-center gap-2 rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--accent-solid-hover)]"
							onclick={() => (createJobDrawerOpen = true)}
						>
							<Hammer class="h-4 w-4" aria-hidden="true" />
							Create job
						</button>
					{/if}
					<a href="/bdr/admin/invoices" class="inline-flex min-h-11 items-center justify-center rounded-md border border-[var(--shell-border)] bg-white px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]">
						Open invoices
					</a>
				</div>
			</section>
		{/if}
	</section>
	</div>
{/snippet}

{#snippet createJobDrawer()}
	<div class="space-y-4">
		<div class="rounded-lg border border-sky-100 bg-sky-50 p-4 text-sky-900">
			<div class="flex items-center gap-2">
				<ClipboardCheck class="h-4 w-4" aria-hidden="true" />
				<p class="text-sm font-semibold">Invoice release</p>
			</div>
			<p class="mt-2 text-sm leading-6 text-sky-800">
				Create the job after confirming the invoice cleared the deposit gate, then finish schedule and weather planning in the job workspace.
			</p>
			<div class="mt-3 flex flex-wrap gap-2">
				<a href="/bdr/admin/invoices" class="inline-flex min-h-9 items-center justify-center rounded-md border border-sky-200 bg-white px-3 py-1.5 text-xs font-semibold text-sky-900 transition hover:bg-sky-100">
					Open invoices
				</a>
				<a href="/bdr/admin/calendar" class="inline-flex min-h-9 items-center justify-center rounded-md border border-sky-200 bg-white px-3 py-1.5 text-xs font-semibold text-sky-900 transition hover:bg-sky-100">
					Calendar / weather
				</a>
			</div>
		</div>

		{#if scheduleReadyJobs.length}
			<div class="space-y-3">
				{#each scheduleReadyJobs as readyJob}
					<form method="POST" action="?/scheduleReadyJob" class="rounded-lg border border-[var(--shell-border)] bg-white p-4">
						<input type="hidden" name="invoiceId" value={readyJob.invoiceId} />
						<div class="flex items-start justify-between gap-3">
							<div class="min-w-0">
								<p class="truncate text-sm font-semibold text-[var(--text-strong)]">{readyJob.siteName || readyJob.customerName}</p>
								<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">
									{readyJob.invoiceNumber} · {Math.round(readyJob.paidPercent)}% paid · {formatCurrency(readyJob.amountPaid)} collected
								</p>
								<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{readyJob.serviceSummary}</p>
							</div>
							<span class="shrink-0 rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">
								{formatCurrency(readyJob.amount)}
							</span>
						</div>
						<div class="mt-4 grid gap-3 sm:grid-cols-2">
							<label class="space-y-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Target date</span>
								<input name="scheduledDate" type="date" value={tomorrowInput} min={todayInput} class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
							</label>
							<label class="space-y-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Crew</span>
								<input name="crew" value="Production crew" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
							</label>
							<label class="space-y-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Window start</span>
								<input name="windowStart" type="time" value="08:00" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
							</label>
							<label class="space-y-1">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Window end</span>
								<input name="windowEnd" type="time" value="12:00" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" />
							</label>
							<label class="space-y-1 sm:col-span-2">
								<span class="text-xs font-semibold text-[var(--text-muted)]">Release note</span>
								<input name="scheduleNotes" class="h-10 w-full rounded-md border border-[var(--shell-border)] bg-white px-3 text-sm text-[var(--text-strong)]" placeholder="Customer, access, or schedule context" />
							</label>
						</div>
						<button type="submit" class="mt-3 inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-[var(--accent-solid)] px-3 py-2 text-sm font-semibold text-white transition hover:bg-[var(--accent-solid-hover)]">
							<Hammer class="h-4 w-4" aria-hidden="true" />
							Create job
						</button>
					</form>
				{/each}
			</div>
		{:else}
			<div class="rounded-lg border border-dashed border-[var(--shell-border)] bg-white p-5 text-sm leading-6 text-[var(--text-muted)]">
				No invoices are ready to release into production yet.
			</div>
		{/if}
	</div>
{/snippet}

<AdminWorkspace
	title="Jobs"
	description="Production jobs, crew status, holds, and completion controls for work that has cleared the billing gate."
	metrics={metrics}
	work={workSurface}
	drawer={createJobDrawer}
	drawerOpen={createJobDrawerOpen}
	drawerTitle="Create job"
	closeDrawer={closeCreateJobDrawer}
/>
