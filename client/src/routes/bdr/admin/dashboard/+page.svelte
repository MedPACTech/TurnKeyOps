<script lang="ts">
	import { CalendarDays, FileText, Receipt, TrendingUp, Users } from 'lucide-svelte';
	import { formatCurrency } from '$lib/utils/format';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	type RangeKey = 'week' | 'month' | 'year';
	let activeRange = $state<RangeKey>('month');

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

	const summaryCards = $derived([
		{
			label: 'Open opportunities',
			value: data.metrics[0]?.value ?? String(data.snapshot.summary.leadCount),
			detail: data.metrics[0]?.detail ?? 'Current scaffold leads',
			icon: Users
		},
		{
			label: 'Active estimates',
			value: data.metrics[1]?.value ?? String(data.snapshot.summary.estimateCount),
			detail: data.metrics[1]?.detail ?? 'Estimate queue value',
			icon: FileText
		},
		{
			label: 'Quote requests',
			value: data.metrics[2]?.value ?? String(data.requestInbox.length),
			detail: data.metrics[2]?.detail ?? 'Incoming request inbox',
			icon: Receipt
		},
		{
			label: 'Crew utilization',
			value: `${activeMetrics.crewUtilization}%`,
			detail: `Forecasted capacity over the selected ${activeRange}`,
			icon: TrendingUp
		}
	]);

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

	const operations = $derived([
		{ label: 'Upcoming jobs', value: String(activeMetrics.upcomingJobs), detail: `Scheduled over the next ${activeRange}` },
		{ label: 'Quotes', value: String(activeMetrics.quotes), detail: 'Issued or in review' },
		{ label: 'Scheduled yards', value: activeMetrics.scheduledYards.toLocaleString(), detail: 'Projected concrete volume' },
		{ label: 'Average ticket', value: formatCurrency(activeMetrics.avgTicket), detail: 'Blended estimate size' },
		{ label: 'Close rate', value: `${activeMetrics.closeRate}%`, detail: 'Quote-to-job conversion' }
	]);

	const nextMoves = [
		'Confirm crew availability for high-value jobs in the next 14 days.',
		'Review new quote requests before end of day.',
		'Push ready estimates into the schedule lane.'
	];
</script>

<svelte:head>
	<title>BDR Admin · Dashboard</title>
</svelte:head>

<div class="space-y-5">
	<section class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
		<div class="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl">
				<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">Dashboard</p>
				<h1 class="mt-2 text-2xl font-bold tracking-tight text-[var(--text-strong)]">Concrete business snapshot</h1>
				<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">A focused operations view for pipeline, scheduling, invoicing, and quote momentum.</p>
			</div>

			<div class="inline-flex self-start rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-1 lg:self-auto">
				{#each rangeOptions as option}
					<button
						type="button"
						class={`rounded-md px-4 py-2 text-sm font-semibold transition ${
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
	</section>

	<section class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
		{#each summaryCards as card}
			{@const Icon = card.icon}
			<article class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
				<div class="flex items-start justify-between gap-3">
					<div>
						<p class="text-xs font-medium text-[var(--text-muted)]">{card.label}</p>
						<p class="mt-2 text-3xl font-bold tracking-tight text-[var(--text-strong)]">{card.value}</p>
					</div>
					<div class="flex h-10 w-10 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-[var(--accent-text)]">
						<Icon class="h-5 w-5" aria-hidden="true" />
					</div>
				</div>
				<p class="mt-3 text-sm leading-5 text-[var(--text-muted)]">{card.detail}</p>
			</article>
		{/each}
	</section>

	<section class="grid gap-4 xl:grid-cols-[minmax(0,1.45fr)_minmax(320px,0.75fr)]">
		<article class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
			<div class="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
				<div>
					<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Revenue trend</p>
					<h2 class="mt-1 text-xl font-bold text-[var(--text-strong)]">Year-over-year revenue and projection</h2>
				</div>
				<div class="text-sm text-[var(--text-muted)]">Actual vs prior year vs projected</div>
			</div>

			<div class="mt-5 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
				<svg viewBox="0 0 100 100" class="h-72 w-full overflow-visible" preserveAspectRatio="none" aria-label="Revenue line graph">
					{#each [20, 40, 60, 80] as line}
						<line x1="0" y1={line} x2="100" y2={line} stroke="rgba(148,163,184,0.24)" stroke-width="0.6" />
					{/each}
					<polyline fill="none" stroke="#94a3b8" stroke-width="1.6" points={chartPoints('prior')} />
					<polyline fill="none" stroke="#4050e6" stroke-width="2.2" points={chartPoints('actual')} />
					<polyline fill="none" stroke="#22c55e" stroke-dasharray="2.5 2.5" stroke-width="1.8" points={chartPoints('projected')} />
				</svg>
				<div class="mt-4 flex flex-wrap gap-4 text-sm text-[var(--text-muted)]">
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#4050e6]"></span>Current year actual</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#94a3b8]"></span>Prior year</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#22c55e]"></span>Projection</div>
				</div>
			</div>
		</article>

		<article class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
			<div class="flex items-center gap-3">
				<div class="flex h-10 w-10 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-[var(--accent-text)]">
					<CalendarDays class="h-5 w-5" aria-hidden="true" />
				</div>
				<div>
					<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Operations</p>
					<h2 class="text-xl font-bold text-[var(--text-strong)]">Current range</h2>
				</div>
			</div>

			<div class="mt-5 space-y-3">
				{#each operations as metric}
					<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
						<p class="text-xs font-medium text-[var(--text-muted)]">{metric.label}</p>
						<div class="mt-1 flex items-end justify-between gap-3">
							<p class="text-2xl font-bold text-[var(--text-strong)]">{metric.value}</p>
						</div>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{metric.detail}</p>
					</div>
				{/each}
			</div>
		</article>
	</section>

	<section class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
		<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Next moves</p>
		<div class="mt-4 grid gap-3 md:grid-cols-3">
			{#each nextMoves as move, index}
				<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
					<p class="text-xs font-semibold text-[var(--accent-text)]">0{index + 1}</p>
					<p class="mt-2 text-sm leading-6 text-[var(--text-base)]">{move}</p>
				</div>
			{/each}
		</div>
	</section>
</div>
