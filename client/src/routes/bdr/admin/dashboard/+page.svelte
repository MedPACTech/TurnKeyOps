<script lang="ts">
	import { formatCurrency } from '$lib/utils/format';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	type RangeKey = 'week' | 'month' | 'year';
	type SummaryCard = {
		label: string;
		value: string;
		href: string;
		icon: string;
		trend?: string;
		trendClass?: string;
	};

	let activeRange = $state<RangeKey>('month');
	let completedOrderIds = $state<string[]>([]);

	const rangeOptions: Array<{ key: RangeKey; label: string }> = [
		{ key: 'week', label: 'Week' },
		{ key: 'month', label: 'Month' },
		{ key: 'year', label: 'Year' }
	];

	const dashboardRanges = {
		week: {
			upcomingJobs: 14,
			quotes: 9,
			scheduledYards: 1820,
			crewUtilization: 86,
			avgTicket: 12400,
			closeRate: 41
		},
		month: {
			upcomingJobs: 48,
			quotes: 37,
			scheduledYards: 6840,
			crewUtilization: 91,
			avgTicket: 18750,
			closeRate: 46
		},
		year: {
			upcomingJobs: 612,
			quotes: 428,
			scheduledYards: 82100,
			crewUtilization: 88,
			avgTicket: 21400,
			closeRate: 49
		}
	} as const;

	const activeMetrics = $derived(dashboardRanges[activeRange]);
	const scheduleReadyJobs = $derived((data.scheduleReadyJobs ?? []).filter((job) => !job.isScheduled));

	const summaryCards = $derived([
		{
			label: 'Active jobs',
			value: String(activeMetrics.upcomingJobs),
			href: '/bdr/admin/jobs',
			icon: '🏗️'
		},
		{
			label: 'Pending estimates',
			value: String(data.snapshot.summary.estimateCount),
			href: '/bdr/admin/estimates',
			icon: '📝'
		},
		{
			label: 'Quote requests',
			value: String(data.requestInbox.length),
			href: '/bdr/admin/requests',
			icon: '📥'
		},
		{
			label: 'Ready to schedule',
			value: String(scheduleReadyJobs.length),
			href: '/bdr/admin/calendar',
			icon: '📅'
		},
		{
			label: 'Overdue',
			value: '0',
			href: '/bdr/admin/invoices',
			icon: '⚠️'
		},
		{
			label: 'Revenue (Month)',
			value: '$0',
			href: '/bdr/admin/invoices',
			icon: '💰',
			trend: '▲',
			trendClass: 'text-green-600'
		}
	] satisfies SummaryCard[]);

	const revenueSeries = [
		{ label: 'Jan', actual: 420000, prior: 356000, projected: 435000 },
		{ label: 'Feb', actual: 468000, prior: 379000, projected: 482000 },
		{ label: 'Mar', actual: 512000, prior: 401000, projected: 530000 },
		{ label: 'Apr', actual: 548000, prior: 418000, projected: 571000 },
		{ label: 'May', actual: 594000, prior: 452000, projected: 628000 },
		{ label: 'Jun', actual: 638000, prior: 488000, projected: 672000 },
		{ label: 'Jul', actual: 702000, prior: 519000, projected: 735000 },
		{ label: 'Aug', actual: 724000, prior: 541000, projected: 758000 },
		{ label: 'Sep', actual: 688000, prior: 526000, projected: 716000 },
		{ label: 'Oct', actual: 646000, prior: 503000, projected: 670000 },
		{ label: 'Nov', actual: 588000, prior: 462000, projected: 612000 },
		{ label: 'Dec', actual: 552000, prior: 438000, projected: 590000 }
	] as const;

	const maxRevenue = Math.max(...revenueSeries.flatMap((item) => [item.actual, item.prior, item.projected]));
	const chartPoints = (key: 'actual' | 'prior' | 'projected') =>
		revenueSeries
			.map((item, index) => {
				const x = (index / (revenueSeries.length - 1)) * 100;
				const y = 100 - (item[key] / maxRevenue) * 100;
				return `${x},${y}`;
			})
			.join(' ');

	const concreteYardsToday = $derived(Math.max(28, Math.round(activeMetrics.scheduledYards / 120)));
	const priorityRequest = $derived(data.requestInbox[0]);
	const isOrderComplete = (id: string) => completedOrderIds.includes(id);
	const toggleOrderComplete = (id: string) => {
		completedOrderIds = isOrderComplete(id)
			? completedOrderIds.filter((orderId) => orderId !== id)
			: [...completedOrderIds, id];
	};

	const bobMoves = $derived([
		scheduleReadyJobs[0]
			? {
					label: `Schedule ${scheduleReadyJobs[0].siteName}`,
					detail: `${formatCurrency(scheduleReadyJobs[0].amountPaid)} collected · ${scheduleReadyJobs[0].depositPercentRequired}% deposit met`,
					href: '/bdr/admin/jobs'
				}
			: null,
		priorityRequest
			? {
					label: priorityRequest.contactName ? `Call ${priorityRequest.contactName}` : 'Review newest request',
					detail: priorityRequest.siteName || priorityRequest.serviceAddress || priorityRequest.nextAction,
					href: '/bdr/admin/requests'
				}
			: {
					label: 'Review request queue',
					detail: `${data.requestInbox.length} requests`,
					href: '/bdr/admin/requests'
				},
		{
			label: 'Price pending estimates',
			detail: `${data.snapshot.summary.estimateCount} estimates - ${formatCurrency(data.snapshot.summary.estimateValue)}`,
			href: '/bdr/admin/estimates'
		},
		{
			label: 'Confirm schedule gaps',
			detail: `${activeMetrics.upcomingJobs} active jobs`,
			href: '/bdr/admin/jobs'
		},
		{
			label: 'Collect open invoices',
			detail: `${data.snapshot.summary.invoiceCount} invoices`,
			href: '/bdr/admin/invoices'
		}
	].filter((item): item is { label: string; detail: string; href: string } => Boolean(item)));

	const orderItems = $derived([
		{
			id: 'concrete',
			icon: '🚚',
			item: 'Concrete',
			quantity: `${concreteYardsToday} yd`,
			detail: "Today's pours",
			href: '/bdr/admin/calendar'
		},
		{
			id: 'forms-rebar',
			icon: '🧱',
			item: 'Forms and rebar',
			quantity: '6 kits',
			detail: 'Patio prep',
			href: '/bdr/admin/requests'
		},
		{
			id: 'pump-truck',
			icon: '🛻',
			item: 'Pump truck',
			quantity: '1 slot',
			detail: 'Access window',
			href: '/bdr/admin/calendar'
		},
		{
			id: 'finish-kit',
			icon: '✨',
			item: 'Finish kit',
			quantity: '4 sets',
			detail: 'Cure compound',
			href: '/bdr/admin/estimates'
		}
	]);
</script>

<svelte:head>
	<title>BDR Admin · Dashboard</title>
</svelte:head>

<div class="space-y-5">
	<section class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
		<div>
			<h1 class="text-2xl font-semibold leading-8 tracking-normal text-[var(--text-strong)]">Dashboard</h1>
		</div>
		<a
			href="/bdr/admin/estimates"
			class="inline-flex items-center justify-center rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold leading-5 text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)] focus:outline-none focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-offset-2 sm:w-auto"
		>
			+ New Estimate
		</a>
	</section>

	<section class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
		{#each summaryCards as card}
			<a
				href={card.href}
				class="group flex h-40 flex-col justify-between rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)] transition hover:-translate-y-0.5 hover:bg-white hover:shadow-md"
			>
				<div class="flex items-start justify-between gap-3">
					<div class="flex h-10 w-10 items-center justify-center rounded-lg bg-white/80 text-xl" aria-hidden="true">
						{card.icon}
					</div>
					{#if card.trend}
						<span class={`text-sm font-bold leading-none ${card.trendClass ?? 'text-[var(--text-muted)]'}`} aria-hidden="true">{card.trend}</span>
					{/if}
				</div>
				<div>
					<p class="text-4xl font-semibold leading-none tracking-normal text-[var(--text-strong)]">{card.value}</p>
					<p class="mt-2 whitespace-nowrap text-xs font-medium leading-5 text-[var(--text-muted)]">{card.label}</p>
				</div>
			</a>
		{/each}
	</section>

	<article class="rounded-lg bg-white/88 p-5 shadow-[var(--shell-shadow)]">
		<div class="flex items-start justify-between gap-3">
			<div class="flex items-start gap-3">
				<div class="flex h-12 w-12 shrink-0 items-center justify-center text-2xl">
					👷‍♂️
				</div>
				<div>
					<h2 class="text-base font-semibold leading-12 tracking-normal text-[var(--text-strong)]">Bob's next moves</h2>

				</div>
			</div>
			<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg text-[var(--accent-text)] shadow-sm" title="AI-driven">
				✨
			</span>
		</div>

		<div class="mt-4 grid gap-3 md:grid-cols-2">
			{#each bobMoves as item}
				<a
					href={item.href}
					class="rounded-lg bg-[var(--shell-panel-strong)] px-4 py-3 shadow-sm transition hover:bg-[var(--accent-soft)]"
				>
					<p class="text-sm font-semibold leading-5 text-[var(--text-strong)]">{item.label}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{item.detail}</p>
				</a>
			{/each}
		</div>
	</article>

	<article class="rounded-lg bg-white/88 p-5 shadow-[var(--shell-shadow)]">
		<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
			<div>
				<h2 class="text-base font-semibold leading-6 tracking-normal text-[var(--text-strong)]">Scheduling handoff</h2>
				<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">
					Jobs appear here once payments meet the configured {data.billingSettings.depositPercentRequired}% deposit gate.
				</p>
			</div>
			<a href="/bdr/admin/calendar" class="inline-flex w-fit rounded-md bg-white px-4 py-2.5 text-sm font-semibold text-[var(--text-strong)] shadow-sm transition hover:bg-[var(--shell-panel-strong)]">
				Open calendar
			</a>
		</div>
		<div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
			{#if scheduleReadyJobs.length}
				{#each scheduleReadyJobs.slice(0, 3) as job}
					<a href="/bdr/admin/invoices" class="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 shadow-sm transition hover:bg-emerald-100">
						<p class="text-sm font-semibold text-[var(--text-strong)]">{job.invoiceNumber} · {job.siteName}</p>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{job.customerName} / {job.contactName}</p>
						<p class="mt-3 text-sm font-semibold text-emerald-700">{formatCurrency(job.amountPaid)} collected</p>
					</a>
				{/each}
			{:else}
				<div class="rounded-lg border border-dashed border-[var(--shell-border)] px-4 py-6 text-sm text-[var(--text-muted)] md:col-span-2 xl:col-span-3">
					No deposit-cleared jobs are waiting on the schedule.
				</div>
			{/if}
		</div>
	</article>

	<section class="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_380px]">
		<article class="rounded-lg bg-white/86 p-5 shadow-[var(--shell-shadow)]">
			<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
				<div>
					<h2 class="text-base font-semibold leading-6 tracking-normal text-[var(--text-strong)]">Revenue</h2>
				</div>
				<div class="inline-flex rounded-md bg-white/70 p-1 shadow-sm">
					{#each rangeOptions as option}
						<button
							type="button"
							class={`rounded-md px-3 py-1.5 text-sm font-semibold leading-5 transition ${
								activeRange === option.key
									? 'bg-[var(--accent-solid)] text-white shadow-sm'
									: 'text-[var(--text-muted)] hover:text-[var(--text-strong)]'
							}`}
							onclick={() => (activeRange = option.key)}
						>
							{option.label}
						</button>
					{/each}
				</div>
			</div>

			<div class="mt-4 rounded-lg bg-[var(--shell-panel-strong)]/80 p-4">
				<svg viewBox="0 0 100 100" class="h-40 w-full overflow-visible" preserveAspectRatio="none" aria-label="Revenue line graph">
					{#each [20, 40, 60, 80] as line}
						<line x1="0" y1={line} x2="100" y2={line} stroke="rgba(148,163,184,0.22)" stroke-width="0.6" />
					{/each}
					<polyline fill="none" stroke="#94a3b8" stroke-width="1" points={chartPoints('prior')} />
					<polyline fill="none" stroke="#f97316" stroke-width="1" points={chartPoints('actual')} />
					<polyline fill="none" stroke="#475569" stroke-dasharray="2.5 2.5" stroke-width="1" points={chartPoints('projected')} />
				</svg>
				<div class="mt-3 flex flex-wrap gap-4 text-xs text-[var(--text-muted)]">
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#f97316]"></span>Actual</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#94a3b8]"></span>Prior</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#475569]"></span>Projected</div>
				</div>
			</div>
		</article>

		<article class="rounded-lg bg-white/88 p-5 shadow-[var(--shell-shadow)]">
			<div class="flex items-center justify-between gap-3">
				<div class="flex min-w-0 items-center gap-3">
					<div class="flex h-12 w-12 shrink-0 items-center justify-center text-2xl">
						👷‍♂️
					</div>
					<h2 class="text-base font-semibold leading-6 tracking-normal text-[var(--text-strong)]">Bob's order list</h2>
				</div>
				<span class="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--accent-soft)] text-[var(--accent-text)] shadow-sm" title="AI-driven">
					✨
				</span>
			</div>
			<div class="mt-4 space-y-3">
				{#each orderItems as item}
					{@const completed = isOrderComplete(item.id)}
					<div
						class={`grid grid-cols-[2.5rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg px-3 py-3 shadow-sm transition ${
							completed
								? 'bg-slate-100/80 text-slate-400 opacity-60'
								: 'bg-[var(--shell-panel-strong)]/90 hover:bg-[var(--accent-soft)]'
						}`}
					>
						<span class={`flex h-10 w-10 items-center justify-center rounded-lg bg-white text-xl shadow-sm ${completed ? 'grayscale' : ''}`} aria-hidden="true">{item.icon}</span>
						<div class="min-w-0">
							<p class={`truncate text-sm font-semibold leading-5 ${completed ? 'text-slate-500 line-through' : 'text-[var(--text-strong)]'}`}>{item.item}</p>
							<p class={`truncate text-xs leading-5 ${completed ? 'text-slate-400' : 'text-[var(--text-muted)]'}`}>{item.detail}</p>
						</div>
						<div class="flex items-center gap-2">
							<p class={`text-sm font-semibold leading-5 ${completed ? 'text-slate-500 line-through' : 'text-[var(--text-strong)]'}`}>{item.quantity}</p>
							<button
								type="button"
								class={`rounded-md px-2.5 py-1.5 text-xs font-semibold leading-4 shadow-sm transition ${
									completed
										? 'bg-slate-200 text-slate-500 hover:bg-slate-300'
										: 'bg-white text-[var(--accent-text)] hover:bg-[var(--accent-soft)]'
								}`}
								aria-pressed={completed}
								onclick={() => toggleOrderComplete(item.id)}
							>
								{completed ? 'Done' : 'Complete'}
							</button>
						</div>
					</div>
				{/each}
			</div>
		</article>
	</section>
</div>
