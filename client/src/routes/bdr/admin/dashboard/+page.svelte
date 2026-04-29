<script lang="ts">
	import { ArrowRight, Bot, CalendarDays, FileText, Receipt, Users } from 'lucide-svelte';
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
	const rangeLabel = $derived(activeRange === 'week' ? '7 days' : activeRange === 'month' ? '30 days' : '12 months');

	const summaryCards = $derived([
		{
			label: 'Quote requests',
			value: String(data.requestInbox.length),
			detail: `${data.requestMetrics.newCount} new waiting for first response`,
			href: '/bdr/admin/requests?role=office-admin',
			icon: Users
		},
		{
			label: 'Estimate queue',
			value: String(data.snapshot.summary.estimateCount),
			detail: `${formatCurrency(data.snapshot.summary.estimateValue)} in active estimate value`,
			href: '/bdr/admin/estimates?role=office-admin',
			icon: FileText
		},
		{
			label: 'Calendar',
			value: String(activeMetrics.upcomingJobs),
			detail: `Jobs and visits in the next ${rangeLabel}`,
			href: '/bdr/admin/calendar?role=office-admin',
			icon: CalendarDays
		},
		{
			label: 'Invoices',
			value: String(data.snapshot.summary.invoiceCount),
			detail: 'Collections, payment holds, and billing follow-through',
			href: '/bdr/admin/invoices?role=office-admin',
			icon: Receipt
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

	const bobQueue = $derived([
		{
			label: `${data.requestMetrics.newCount} new request(s) need a first response`,
			detail: 'Open the request inbox before leads cool off.',
			href: '/bdr/admin/requests?role=office-admin'
		},
		{
			label: `${data.snapshot.summary.estimateCount} estimate(s) are active`,
			detail: 'Review ready-to-send work and revision follow-up.',
			href: '/bdr/admin/estimates?role=office-admin'
		},
		{
			label: `${data.snapshot.summary.invoiceCount} invoice record(s) need visibility`,
			detail: 'Check collections and payment holds without leaving the office surface.',
			href: '/bdr/admin/invoices?role=office-admin'
		}
	]);
</script>

<svelte:head>
	<title>BDR Admin · Dashboard</title>
</svelte:head>

<div class="space-y-4">
	<section class="flex flex-col gap-4 rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm lg:flex-row lg:items-center lg:justify-between">
		<div>
			<p class="text-sm font-semibold text-[var(--accent-text)]">Dashboard</p>
			<h1 class="mt-1 text-2xl font-semibold leading-8 tracking-normal text-[var(--text-strong)]">Contractor office dashboard</h1>
			<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">Pipeline, schedule, billing, and Bob’s next actions in one clean operating surface.</p>
		</div>
		<a
			href="/bdr/admin/estimates?role=office-admin"
			class="inline-flex items-center justify-center rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold leading-5 text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)] focus:outline-none focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-offset-2"
		>
			+ New Estimate
		</a>
	</section>

	<section class="grid gap-4 xl:grid-cols-[minmax(0,1.45fr)_360px]">
		<div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
		{#each summaryCards as card}
			{@const Icon = card.icon}
			<a
				href={card.href}
				class="rounded-lg border border-[var(--shell-border)] bg-white p-4 shadow-sm transition hover:border-[var(--accent-border)] hover:bg-[var(--accent-soft)]"
			>
				<div class="flex items-start justify-between gap-3">
					<div class="min-w-0">
						<p class="text-sm font-medium leading-5 text-[var(--text-muted)]">{card.label}</p>
						<p class="mt-2 text-3xl font-semibold leading-none tracking-normal text-[var(--text-strong)]">{card.value}</p>
						<p class="mt-2 text-sm leading-5 text-[var(--text-muted)]">{card.detail}</p>
					</div>
					<div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-[var(--accent-soft)] text-[var(--accent-text)]">
						<Icon class="h-4 w-4" aria-hidden="true" />
					</div>
				</div>
				<div class="mt-4 flex items-center gap-1 text-sm font-semibold leading-5 text-[var(--accent-text)]">
					Open
					<ArrowRight class="h-3.5 w-3.5" aria-hidden="true" />
				</div>
			</a>
		{/each}
		</div>

		<article class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
			<div class="flex items-start gap-3">
				<div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-[var(--accent-soft)] text-[var(--accent-text)]">
					<Bot class="h-5 w-5" aria-hidden="true" />
				</div>
				<div>
					<p class="text-sm font-semibold text-[var(--text-muted)]">Bob</p>
					<h2 class="text-base font-semibold leading-6 tracking-normal text-[var(--text-strong)]">Back-office next actions</h2>
					<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">Practical help for follow-up, scheduling, and collections. No chat pitch.</p>
				</div>
			</div>

			<div class="mt-4 space-y-2">
				{#each bobQueue as item}
					<a
						href={item.href}
						class="flex items-start justify-between gap-3 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3 transition hover:border-[var(--accent-border)] hover:bg-[var(--accent-soft)]"
					>
						<div class="min-w-0">
							<p class="text-sm font-semibold text-[var(--text-strong)]">{item.label}</p>
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{item.detail}</p>
						</div>
						<ArrowRight class="mt-0.5 h-4 w-4 shrink-0 text-[var(--accent-text)]" aria-hidden="true" />
					</a>
				{/each}
			</div>
		</article>
	</section>

	<section class="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_320px]">
		<article class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
			<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
				<div>
					<p class="text-sm font-semibold text-[var(--text-muted)]">Revenue trend</p>
					<h2 class="text-base font-semibold leading-6 tracking-normal text-[var(--text-strong)]">Quiet revenue view</h2>
				</div>
				<div class="inline-flex rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-1">
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

			<div class="mt-4 rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-4">
				<svg viewBox="0 0 100 100" class="h-40 w-full overflow-visible" preserveAspectRatio="none" aria-label="Revenue line graph">
					{#each [20, 40, 60, 80] as line}
						<line x1="0" y1={line} x2="100" y2={line} stroke="rgba(148,163,184,0.22)" stroke-width="0.6" />
					{/each}
					<polyline fill="none" stroke="#94a3b8" stroke-width="1.4" points={chartPoints('prior')} />
					<polyline fill="none" stroke="#f97316" stroke-width="1.8" points={chartPoints('actual')} />
					<polyline fill="none" stroke="#475569" stroke-dasharray="2.5 2.5" stroke-width="1.6" points={chartPoints('projected')} />
				</svg>
				<div class="mt-3 flex flex-wrap gap-4 text-xs text-[var(--text-muted)]">
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#f97316]"></span>Actual</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#94a3b8]"></span>Prior</div>
					<div class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full bg-[#475569]"></span>Projected</div>
				</div>
			</div>
		</article>

		<article class="rounded-lg border border-[var(--shell-border)] bg-white p-5 shadow-sm">
			<p class="text-sm font-semibold text-[var(--text-muted)]">Office board</p>
			<div class="mt-4 space-y-3">
				{#each operations as metric}
					<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
						<p class="text-sm font-medium leading-5 text-[var(--text-muted)]">{metric.label}</p>
						<div class="mt-1 flex items-end justify-between gap-3">
							<p class="text-2xl font-semibold leading-none tracking-normal text-[var(--text-strong)]">{metric.value}</p>
						</div>
						<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{metric.detail}</p>
					</div>
				{/each}
			</div>
		</article>
	</section>
</div>
