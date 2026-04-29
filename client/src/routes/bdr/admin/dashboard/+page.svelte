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

	const summaryCards = $derived([
		{
			label: 'Active jobs',
			value: String(activeMetrics.upcomingJobs),
			href: '/bdr/admin/calendar?role=office-admin',
			icon: '🏗️'
		},
		{
			label: 'Pending estimates',
			value: String(data.snapshot.summary.estimateCount),
			href: '/bdr/admin/estimates?role=office-admin',
			icon: '📝'
		},
		{
			label: 'Quote requests',
			value: String(data.requestInbox.length),
			href: '/bdr/admin/requests?role=office-admin',
			icon: '📥'
		},
		{
			label: 'Open invoices',
			value: String(data.snapshot.summary.invoiceCount),
			href: '/bdr/admin/invoices?role=office-admin',
			icon: '💰'
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

	const concreteYardsToday = $derived(Math.max(28, Math.round(activeMetrics.scheduledYards / 120)));
	const priorityRequest = $derived(data.requestInbox[0]);

	const bobMoves = $derived([
		priorityRequest
			? {
					label: priorityRequest.contactName ? `Call ${priorityRequest.contactName}` : 'Review newest request',
					detail: priorityRequest.siteName || priorityRequest.serviceAddress || priorityRequest.nextAction,
					href: '/bdr/admin/requests?role=office-admin'
				}
			: {
					label: 'Review request queue',
					detail: `${data.requestInbox.length} requests`,
					href: '/bdr/admin/requests?role=office-admin'
				},
		{
			label: 'Price pending estimates',
			detail: `${data.snapshot.summary.estimateCount} estimates - ${formatCurrency(data.snapshot.summary.estimateValue)}`,
			href: '/bdr/admin/estimates?role=office-admin'
		},
		{
			label: 'Confirm schedule gaps',
			detail: `${activeMetrics.upcomingJobs} active jobs`,
			href: '/bdr/admin/calendar?role=office-admin'
		},
		{
			label: 'Collect open invoices',
			detail: `${data.snapshot.summary.invoiceCount} invoices`,
			href: '/bdr/admin/invoices?role=office-admin'
		}
	]);

	const orderItems = $derived([
		{
			icon: '🚚',
			item: 'Concrete',
			quantity: `${concreteYardsToday} yd`,
			detail: "Today's pours",
			href: '/bdr/admin/calendar?role=office-admin'
		},
		{
			icon: '🧱',
			item: 'Forms and rebar',
			quantity: '6 kits',
			detail: 'Patio prep',
			href: '/bdr/admin/requests?role=office-admin'
		},
		{
			icon: '🛻',
			item: 'Pump truck',
			quantity: '1 slot',
			detail: 'Access window',
			href: '/bdr/admin/calendar?role=office-admin'
		},
		{
			icon: '✨',
			item: 'Finish kit',
			quantity: '4 sets',
			detail: 'Cure compound',
			href: '/bdr/admin/estimates?role=office-admin'
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
			href="/bdr/admin/estimates?role=office-admin"
			class="inline-flex items-center justify-center rounded-md bg-[var(--accent-solid)] px-4 py-2.5 text-sm font-semibold leading-5 text-white shadow-sm transition hover:bg-[var(--accent-solid-hover)] focus:outline-none focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-offset-2 sm:w-auto"
		>
			+ New Estimate
		</a>
	</section>

	<section class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
		{#each summaryCards as card}
			<a
				href={card.href}
				class="group flex aspect-square flex-col justify-between rounded-lg bg-white/90 p-5 shadow-[var(--shell-shadow)] transition hover:-translate-y-0.5 hover:bg-white hover:shadow-md"
			>
				<div class="flex h-12 w-12 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-2xl shadow-sm" aria-hidden="true">
					{card.icon}
				</div>
				<div>
					<p class="text-4xl font-semibold leading-none tracking-normal text-[var(--text-strong)]">{card.value}</p>
					<p class="mt-3 text-sm font-medium leading-5 text-[var(--text-muted)]">{card.label}</p>
				</div>
			</a>
		{/each}
	</section>

	<article class="rounded-lg bg-white/88 p-5 shadow-[var(--shell-shadow)]">
		<div class="flex items-start justify-between gap-3">
			<div class="flex items-start gap-3">
				<div class="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-2xl shadow-sm">
					👷‍♂️
				</div>
				<div>
					<h2 class="text-base font-semibold leading-6 tracking-normal text-[var(--text-strong)]">Bob</h2>
					<p class="text-sm leading-5 text-[var(--text-muted)]">Next moves</p>
				</div>
			</div>
			<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[var(--accent-soft)] text-lg text-[var(--accent-text)] shadow-sm" title="AI-driven">
				✦
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

		<article class="rounded-lg bg-white/88 p-5 shadow-[var(--shell-shadow)]">
			<div class="flex items-center justify-between gap-3">
				<h2 class="text-base font-semibold leading-6 tracking-normal text-[var(--text-strong)]">Bob&apos;s order list</h2>
				<span class="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--accent-soft)] text-[var(--accent-text)] shadow-sm" title="AI-driven">
					✦
				</span>
			</div>
			<div class="mt-4 space-y-3">
				{#each orderItems as item}
					<a href={item.href} class="grid grid-cols-[2.5rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg bg-[var(--shell-panel-strong)]/90 px-3 py-3 shadow-sm transition hover:bg-[var(--accent-soft)]">
						<span class="flex h-10 w-10 items-center justify-center rounded-lg bg-white text-xl shadow-sm" aria-hidden="true">{item.icon}</span>
						<div class="min-w-0">
							<p class="truncate text-sm font-semibold leading-5 text-[var(--text-strong)]">{item.item}</p>
							<p class="truncate text-xs leading-5 text-[var(--text-muted)]">{item.detail}</p>
						</div>
						<p class="text-sm font-semibold leading-5 text-[var(--text-strong)]">{item.quantity}</p>
					</a>
				{/each}
			</div>
		</article>
	</section>
</div>
