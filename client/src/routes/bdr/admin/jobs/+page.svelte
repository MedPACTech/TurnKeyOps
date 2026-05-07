<script lang="ts">
	import AdminWorkspace from '$lib/components/admin/AdminWorkspace.svelte';
	import { formatCurrency } from '$lib/utils/format';
	import type { PageProps } from './$types';
	import {
		AlertTriangle,
		CalendarClock,
		CheckCircle2,
		CircleDollarSign,
		ClipboardList,
		Hammer,
		Mail,
		MapPin,
		PauseCircle,
		Phone,
		PlayCircle,
		RefreshCcw,
		Search,
		StickyNote,
		XCircle
	} from 'lucide-svelte';

	type JobStatus = 'scheduled' | 'in-progress' | 'on-hold' | 'completed' | 'cancelled';
	type JobFilter = 'active' | JobStatus;
	type JobActivity = {
		id: string;
		type: 'scheduled' | 'status-updated' | 'rescheduled' | 'note';
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
		activity: JobActivity[];
	};
	type ScheduleReadyJob = {
		invoiceId: string;
		invoiceNumber: string;
		customerName: string;
		siteName: string;
		serviceSummary: string;
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

	const activeStatusSet = new Set<JobStatus>(['scheduled', 'in-progress', 'on-hold']);
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
	const activeJobs = $derived(jobs.filter((job) => activeStatusSet.has(job.status)));
	const runningJobs = $derived(jobs.filter((job) => job.status === 'in-progress'));
	const heldJobs = $derived(jobs.filter((job) => job.status === 'on-hold'));
	const activeValue = $derived(activeJobs.reduce((sum, job) => sum + job.amount, 0));
	const nextJob = $derived(activeJobs[0] ?? null);
	const metrics = $derived([
		{ label: 'Active jobs', value: String(activeJobs.length), icon: '🏗️' },
		{ label: 'Running now', value: String(runningJobs.length), icon: '▶️' },
		{ label: 'On hold', value: String(heldJobs.length), icon: '⏸️' },
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

{#snippet contextRail()}
	<div class="space-y-3">
		{#if nextJob}
			<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
				<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Next job</p>
				<p class="mt-2 text-sm font-semibold text-[var(--text-strong)]">{nextJob.siteName || nextJob.customerName}</p>
				<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">
					{formatDate(nextJob.scheduledDate)} · {formatTime(nextJob.windowStart)} - {formatTime(nextJob.windowEnd)}
				</p>
			</div>
		{/if}
		<div class="rounded-lg border border-sky-100 bg-sky-50 p-3 text-sky-900">
			<div class="flex items-center gap-2">
				<CalendarClock class="h-4 w-4" aria-hidden="true" />
				<p class="text-sm font-semibold">Schedule-ready</p>
			</div>
			<p class="mt-2 text-xs leading-5 text-sky-800">
				{scheduleReadyJobs.length} invoice{scheduleReadyJobs.length === 1 ? '' : 's'} cleared the deposit gate and can be scheduled into production.
			</p>
		</div>
		<div class="space-y-2">
			{#each scheduleReadyJobs.slice(0, 4) as readyJob}
				<a
					href="/bdr/admin/invoices"
					class="block rounded-lg border border-[var(--shell-border)] bg-white p-3 transition hover:border-emerald-200 hover:bg-emerald-50"
				>
					<p class="text-sm font-semibold text-[var(--text-strong)]">{readyJob.siteName}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">
						{readyJob.invoiceNumber} · {Math.round(readyJob.paidPercent)}% paid
					</p>
				</a>
			{/each}
		</div>
	</div>
{/snippet}

{#snippet focusRail()}
	<div class="space-y-3">
		<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
			<div class="flex items-center gap-2">
				<Hammer class="h-4 w-4 text-emerald-700" aria-hidden="true" />
				<p class="text-sm font-semibold text-[var(--text-strong)]">Lifecycle lane</p>
			</div>
			<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">
				Scheduled jobs now move through running, hold, completion, and cancellation states from one production desk.
			</p>
		</div>
		<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
			<div class="flex items-center gap-2">
				<CircleDollarSign class="h-4 w-4 text-sky-700" aria-hidden="true" />
				<p class="text-sm font-semibold text-[var(--text-strong)]">Billing tether</p>
			</div>
			<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">
				Each job keeps invoice number, deposit percent, collected amount, and total value visible while production works.
			</p>
		</div>
		<div class="rounded-lg border border-[var(--shell-border)] bg-white p-3">
			<div class="flex items-center gap-2">
				<AlertTriangle class="h-4 w-4 text-amber-600" aria-hidden="true" />
				<p class="text-sm font-semibold text-[var(--text-strong)]">Hold discipline</p>
			</div>
			<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">
				Use hold notes for weather, access, material, paperwork, or customer decisions that block the crew.
			</p>
		</div>
	</div>
{/snippet}

{#snippet workSurface()}
	<section class="grid gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
		<aside class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
			<div class="flex items-center justify-between gap-3">
				<div>
					<p class="text-base font-semibold text-[var(--text-strong)]">Production queue</p>
					<p class="mt-1 text-sm leading-5 text-[var(--text-muted)]">{filteredJobs.length} visible job{filteredJobs.length === 1 ? '' : 's'}</p>
				</div>
				<a
					href="/bdr/admin/calendar"
					class="inline-flex h-10 w-10 items-center justify-center rounded-md border border-[var(--shell-border)] bg-white text-[var(--text-base)] transition hover:bg-[var(--shell-panel-strong)]"
					aria-label="Open calendar"
					title="Open calendar"
				>
					<CalendarClock class="h-5 w-5" aria-hidden="true" />
				</a>
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
							</div>
							<h2 class="mt-3 text-2xl font-semibold leading-8 text-[var(--text-strong)]">{selectedJob.siteName || selectedJob.customerName}</h2>
							<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{selectedJob.serviceSummary}</p>
						</div>
						<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 text-right">
							<p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--muted)]">Job value</p>
							<p class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{formatCurrency(selectedJob.amount)}</p>
							<p class="mt-1 text-xs text-[var(--text-muted)]">{selectedJob.depositPercentRequired}% deposit met at scheduling</p>
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
				<a href="/bdr/admin/invoices" class="mt-4 inline-flex min-h-11 items-center justify-center rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--accent-solid-hover)]">
					Open invoice lane
				</a>
			</section>
		{/if}
	</section>
{/snippet}

<AdminWorkspace
	title="Jobs"
	description="Production jobs, crew status, holds, and completion controls for work that has cleared the billing gate."
	metrics={metrics}
	contextLabel="Production"
	focusLabel="Controls"
	context={contextRail}
	focus={focusRail}
	work={workSurface}
/>
