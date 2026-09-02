<script lang="ts">
	import { formatCurrency } from '$lib/utils/format';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();
	const summaryCards = $derived([
		{ label: 'Active jobs', value: String(data.metrics.activeJobs), href: '/bdr/admin/jobs', icon: '🏗️' },
		{ label: 'Pending estimates', value: String(data.metrics.pendingEstimates), href: '/bdr/admin/estimates', icon: '📝' },
		{ label: 'Quote requests', value: String(data.requestInbox.length), href: '/bdr/admin/requests', icon: '📥' },
		{ label: 'Ready to schedule', value: String(data.scheduleReadyJobs.length), href: '/bdr/admin/calendar', icon: '📅' },
		{ label: 'Open invoices', value: String(data.metrics.openInvoices), href: '/bdr/admin/invoices', icon: '⚠️' },
		{ label: 'Collected this month', value: formatCurrency(data.metrics.collectedThisMonth), href: '/bdr/admin/invoices', icon: '💰' }
	]);
	const activeJobs = $derived(data.jobs.filter((job) => ['scheduled', 'in-progress', 'on-hold'].includes(job.status)).slice(0, 5));
	const openInvoices = $derived(data.invoices.filter((invoice) => invoice.balanceDue > 0.01).slice(0, 5));
</script>

<svelte:head><title>BDR Admin · Dashboard</title></svelte:head>

<div class="space-y-5">
	<header class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
		<div><p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-text)]">Live operations</p><h1 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Dashboard</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Durable quote, billing, and production records for this tenant.</p></div>
		<a href="/bdr/admin/estimates" class="inline-flex min-h-11 items-center justify-center rounded-md bg-[var(--accent-solid)] px-4 text-sm font-semibold text-white">New estimate</a>
	</header>

	{#if data.integrationState.errors.length}
		<section class="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900" role="alert">
			<p class="font-semibold">Some live operational data could not be loaded.</p>
			<ul class="mt-2 list-disc space-y-1 pl-5">{#each data.integrationState.errors as message}<li>{message}</li>{/each}</ul>
			<a href="/bdr/admin/dashboard" class="mt-3 inline-flex font-semibold underline">Retry dashboard</a>
		</section>
	{/if}

	<section class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
		{#each summaryCards as card}
			<a href={card.href} class="flex min-h-36 flex-col justify-between rounded-lg bg-white p-4 shadow-[var(--shell-shadow)] hover:bg-[var(--shell-panel)]">
				<span class="text-xl" aria-hidden="true">{card.icon}</span><div><p class="text-3xl font-semibold text-[var(--text-strong)]">{card.value}</p><p class="mt-2 text-xs font-medium text-[var(--text-muted)]">{card.label}</p></div>
			</a>
		{/each}
	</section>

	<section class="grid gap-4 xl:grid-cols-2">
		<article class="rounded-lg bg-white p-5 shadow-[var(--shell-shadow)]">
			<div class="flex items-center justify-between"><div><p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">Production</p><h2 class="mt-1 text-lg font-semibold">Scheduled work</h2></div><a href="/bdr/admin/jobs" class="text-sm font-semibold text-[var(--accent-text)]">Open jobs</a></div>
			<div class="mt-4 divide-y divide-[var(--shell-border)]">
				{#each activeJobs as job}<a href={`/bdr/admin/jobs?job=${encodeURIComponent(job.id)}`} class="grid gap-1 py-3 sm:grid-cols-[1fr_auto]"><div><p class="font-semibold">{job.siteName || job.customerName}</p><p class="text-sm text-[var(--text-muted)]">{job.scheduledDate} · {job.windowStart}–{job.windowEnd} · {job.crew || 'Crew not assigned'}</p></div><span class="text-xs font-semibold uppercase text-[var(--accent-text)]">{job.status.replace('-', ' ')}</span></a>{:else}<p class="py-8 text-center text-sm text-[var(--text-muted)]">No active production jobs.</p>{/each}
			</div>
		</article>
		<article class="rounded-lg bg-white p-5 shadow-[var(--shell-shadow)]">
			<div class="flex items-center justify-between"><div><p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">Billing</p><h2 class="mt-1 text-lg font-semibold">Open balances · {formatCurrency(data.metrics.openBalance)}</h2></div><a href="/bdr/admin/invoices" class="text-sm font-semibold text-[var(--accent-text)]">Open invoices</a></div>
			<div class="mt-4 divide-y divide-[var(--shell-border)]">
				{#each openInvoices as invoice}<a href="/bdr/admin/invoices" class="grid gap-1 py-3 sm:grid-cols-[1fr_auto]"><div><p class="font-semibold">{invoice.invoiceNumber} · {invoice.customerName}</p><p class="text-sm text-[var(--text-muted)]">{invoice.siteName || invoice.serviceSummary}</p></div><span class="font-semibold">{formatCurrency(invoice.balanceDue)}</span></a>{:else}<p class="py-8 text-center text-sm text-[var(--text-muted)]">No open invoice balances.</p>{/each}
			</div>
		</article>
	</section>

	<footer class="text-xs text-[var(--text-muted)]">Last refreshed {new Date(data.integrationState.loadedAtUtc).toLocaleString()} · request source {data.requestSource}</footer>
</div>
