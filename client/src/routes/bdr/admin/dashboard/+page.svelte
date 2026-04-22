<script lang="ts">
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

	const jobMix = [
		{ label: 'Driveways & flatwork', value: 32, color: '#f97316' },
		{ label: 'Foundations & slabs', value: 24, color: '#fb923c' },
		{ label: 'Commercial paving', value: 18, color: '#fdba74' },
		{ label: 'Decorative concrete', value: 14, color: '#fed7aa' },
		{ label: 'Repair & resurfacing', value: 12, color: '#ffedd5' }
	] as const;

	const jobMixGradient = $derived.by(() => {
		let start = 0;
		return jobMix
			.map((slice) => {
				const end = start + slice.value;
				const segment = `${slice.color} ${start}% ${end}%`;
				start = end;
				return segment;
			})
			.join(', ');
	});

	const financeCards = [
		{ label: 'Estimated income', value: 4860000, detail: 'signed and highly probable backlog over the next 12 months' },
		{ label: 'Projected revenue', value: 7340000, detail: 'blended actual + forecasted revenue for the current year' },
		{ label: 'Estimated expenses', value: 5120000, detail: 'labor, materials, fuel, equipment, and overhead forecast' },
		{ label: 'Estimated gross margin', value: 2220000, detail: 'modeled margin before tax and owner distributions' }
	] as const;

	const marketSignals = [
		{ label: 'Diesel futures', value: '$2.87/gal', change: '+1.8%', tone: 'up', detail: 'watch hauling and mixer operating cost pressure' },
		{ label: 'Cement index', value: '214.6', change: '+0.9%', tone: 'up', detail: 'batch pricing still trending upward into summer' },
		{ label: 'Rebar composite', value: '$742/ton', change: '-0.6%', tone: 'down', detail: 'slight easing for reinforced commercial work' },
		{ label: 'Bid pipeline', value: '$12.4M', change: '+14%', tone: 'up', detail: 'active quoted opportunities across municipal and private work' }
	] as const;

	const operationalMetrics = [
		{ label: 'On-time start rate', value: '93%', detail: 'scheduled crews arriving on first committed start date' },
		{ label: 'Avg days to quote', value: '2.6', detail: 'initial lead to issued quote turnaround' },
		{ label: 'Backlog coverage', value: '11.2 wks', detail: 'current committed work at planned production pace' },
		{ label: 'Change-order rate', value: '8.4%', detail: 'jobs requiring scope or pricing adjustments' }
	] as const;
</script>

<svelte:head>
	<title>BDR Admin · Dashboard</title>
</svelte:head>

<div class="space-y-6">
	<section class="rounded-[1.75rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)] lg:p-6">
		<div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl">
				<p class="text-[0.68rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">Executive dashboard</p>
				<h1 class="mt-2 text-3xl font-semibold tracking-tight text-[var(--text-strong)]">Concrete business snapshot</h1>
				<p class="mt-3 text-sm leading-6 text-[var(--text-muted)]">A clean operator dashboard with business metrics, forecast visibility, and market signals — without rail structures or record-heavy queue layouts.</p>
			</div>

			<div class="inline-flex rounded-full border border-[var(--shell-border)] bg-[var(--shell-panel)] p-1">
				{#each rangeOptions as option}
					<button
						type="button"
						class={`rounded-full px-4 py-2 text-sm font-semibold transition ${
							activeRange === option.key
								? 'bg-[var(--accent-solid)] text-white shadow-[0_10px_25px_rgba(249,115,22,0.25)]'
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

	<section class="grid gap-4 xl:grid-cols-4 md:grid-cols-2">
		<div class="rounded-[1.5rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)]">
			<p class="text-[0.66rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Upcoming jobs</p>
			<p class="mt-3 text-4xl font-semibold tracking-tight text-[var(--text-strong)]">{activeMetrics.upcomingJobs}</p>
			<p class="mt-2 text-sm text-[var(--text-muted)]">Scheduled over the next {activeRange}</p>
		</div>
		<div class="rounded-[1.5rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)]">
			<p class="text-[0.66rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Quotes</p>
			<p class="mt-3 text-4xl font-semibold tracking-tight text-[var(--text-strong)]">{activeMetrics.quotes}</p>
			<p class="mt-2 text-sm text-[var(--text-muted)]">Issued or in review for the selected horizon</p>
		</div>
		<div class="rounded-[1.5rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)]">
			<p class="text-[0.66rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Scheduled yards</p>
			<p class="mt-3 text-4xl font-semibold tracking-tight text-[var(--text-strong)]">{activeMetrics.scheduledYards.toLocaleString()}</p>
			<p class="mt-2 text-sm text-[var(--text-muted)]">Projected concrete volume over the next {activeRange}</p>
		</div>
		<div class="rounded-[1.5rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)]">
			<p class="text-[0.66rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Crew utilization</p>
			<p class="mt-3 text-4xl font-semibold tracking-tight text-[var(--text-strong)]">{activeMetrics.crewUtilization}%</p>
			<p class="mt-2 text-sm text-[var(--text-muted)]">Forecasted productive capacity during this window</p>
		</div>
	</section>

	<section class="grid gap-4 xl:grid-cols-[1.45fr_0.95fr]">
		<div class="rounded-[1.75rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)] lg:p-6">
			<div class="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
				<div>
					<p class="text-[0.68rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Revenue trend</p>
					<h2 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Year-over-year revenue and projection</h2>
				</div>
				<div class="text-sm text-[var(--text-muted)]">Actual vs prior year vs projected</div>
			</div>

			<div class="mt-6 rounded-[1.4rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] p-4">
				<svg viewBox="0 0 100 100" class="h-72 w-full overflow-visible" preserveAspectRatio="none" aria-label="Revenue line graph">
					{#each [20, 40, 60, 80] as line}
						<line x1="0" y1={line} x2="100" y2={line} stroke="rgba(148,163,184,0.18)" stroke-width="0.6" />
					{/each}
					<polyline fill="none" stroke="#94a3b8" stroke-width="1.6" points={chartPoints('prior')} />
					<polyline fill="none" stroke="#f97316" stroke-width="2.2" points={chartPoints('actual')} />
					<polyline fill="none" stroke="#fb923c" stroke-dasharray="2.5 2.5" stroke-width="1.8" points={chartPoints('projected')} />
				</svg>
				<div class="mt-4 flex flex-wrap gap-4 text-sm text-[var(--text-muted)]">
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#f97316]"></span>Current year actual</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#94a3b8]"></span>Prior year</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#fb923c]"></span>Projection</div>
				</div>
				<div class="mt-5 grid grid-cols-6 gap-2 text-xs text-[var(--muted)] md:grid-cols-12">
					{#each revenueSeries as point}
						<div>{point.label}</div>
					{/each}
				</div>
			</div>
		</div>

		<div class="space-y-4">
			<div class="rounded-[1.75rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)] lg:p-6">
				<p class="text-[0.68rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Financial outlook</p>
				<h2 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Income, revenue, and expense view</h2>
				<div class="mt-5 space-y-3">
					{#each financeCards as item}
						<div class="rounded-[1.15rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-4">
							<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{item.label}</p>
							<p class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{formatCurrency(item.value)}</p>
							<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{item.detail}</p>
						</div>
					{/each}
				</div>
			</div>
		</div>
	</section>

	<section class="grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
		<div class="rounded-[1.75rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)] lg:p-6">
			<p class="text-[0.68rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Job mix</p>
			<h2 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Job type distribution</h2>
			<div class="mt-6 flex flex-col items-center gap-6 lg:flex-row lg:items-start">
				<div class="relative h-56 w-56 rounded-full" style={`background: conic-gradient(${jobMixGradient})`}>
					<div class="absolute inset-8 flex items-center justify-center rounded-full bg-[var(--module-bg)] text-center">
						<div>
							<p class="text-[0.62rem] uppercase tracking-[0.18em] text-[var(--muted)]">Top segment</p>
							<p class="mt-2 text-lg font-semibold text-[var(--text-strong)]">Driveways & flatwork</p>
							<p class="mt-1 text-sm text-[var(--text-muted)]">32%</p>
						</div>
					</div>
				</div>
				<div class="w-full space-y-3">
					{#each jobMix as slice}
						<div class="rounded-[1.15rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-3">
							<div class="flex items-center justify-between gap-3">
								<div class="flex items-center gap-3">
									<span class="h-3 w-3 rounded-full" style={`background:${slice.color}`}></span>
									<p class="text-sm font-semibold text-[var(--text-strong)]">{slice.label}</p>
								</div>
								<p class="text-sm font-semibold text-[var(--text-strong)]">{slice.value}%</p>
							</div>
						</div>
					{/each}
				</div>
			</div>
		</div>

		<div class="rounded-[1.75rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)] lg:p-6">
			<p class="text-[0.68rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Operations</p>
			<h2 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Concrete-specific business metrics</h2>
			<div class="mt-5 grid gap-3 sm:grid-cols-2">
				{#each operationalMetrics as metric}
					<div class="rounded-[1.15rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-4">
						<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{metric.label}</p>
						<p class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{metric.value}</p>
						<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{metric.detail}</p>
					</div>
				{/each}
			</div>
		</div>
	</section>

	<section class="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
		<div class="rounded-[1.75rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)] lg:p-6">
			<p class="text-[0.68rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Market watch</p>
			<h2 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Fuel, material, and pipeline signals</h2>
			<div class="mt-5 grid gap-3 sm:grid-cols-2">
				{#each marketSignals as signal}
					<div class="rounded-[1.15rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-4">
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{signal.label}</p>
								<p class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{signal.value}</p>
							</div>
							<span class={`rounded-full px-2.5 py-1 text-xs font-semibold ${signal.tone === 'up' ? 'bg-orange-100 text-orange-700' : 'bg-emerald-100 text-emerald-700'}`}>{signal.change}</span>
						</div>
						<p class="mt-3 text-sm leading-6 text-[var(--text-muted)]">{signal.detail}</p>
					</div>
				{/each}
			</div>
		</div>

		<div class="rounded-[1.75rem] border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)] lg:p-6">
			<p class="text-[0.68rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Sales efficiency</p>
			<h2 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Conversion and contract economics</h2>
			<div class="mt-5 space-y-3">
				<div class="rounded-[1.15rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-4">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Average ticket</p>
					<p class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{formatCurrency(activeMetrics.avgTicket)}</p>
					<p class="mt-2 text-sm text-[var(--text-muted)]">Average awarded value in the selected horizon</p>
				</div>
				<div class="rounded-[1.15rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-4">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Close rate</p>
					<p class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">{activeMetrics.closeRate}%</p>
					<p class="mt-2 text-sm text-[var(--text-muted)]">Won quotes against issued proposals for this range</p>
				</div>
				<div class="rounded-[1.15rem] border border-[var(--shell-border)] bg-[var(--shell-panel)] px-4 py-4">
					<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Data posture</p>
					<p class="mt-2 text-lg font-semibold text-[var(--text-strong)]">Mocked business metrics</p>
					<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">This dashboard is intentionally using mocked business data for the new executive surface while the underlying CRM/ops records continue to evolve.</p>
				</div>
			</div>
		</div>
	</section>
</div>
