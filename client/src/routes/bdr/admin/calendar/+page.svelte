<script lang="ts">
	import { CalendarDays, CloudOff, Hammer, MapPin } from 'lucide-svelte';
	import { formatCurrency } from '$lib/utils/format';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();
	const dateLabel = (value: string) => new Date(`${value}T12:00:00`).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
	const timeLabel = (value: string) => {
		const [hours, minutes] = value.split(':').map(Number);
		return Number.isFinite(hours) && Number.isFinite(minutes)
			? new Date(2026, 0, 1, hours, minutes).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })
			: value;
	};
</script>

<svelte:head><title>BDR Admin · Calendar</title></svelte:head>

<div class="space-y-5">
	<header><p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-text)]">Live scheduling</p><h1 class="mt-2 text-2xl font-semibold text-[var(--text-strong)]">Calendar</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Site visits and production jobs loaded from the durable TurnKey API.</p></header>

	{#if data.integrationState.errors.length}
		<section class="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900" role="alert"><p class="font-semibold">Part of the live schedule is unavailable.</p><ul class="mt-2 list-disc space-y-1 pl-5">{#each data.integrationState.errors as message}<li>{message}</li>{/each}</ul><a href="/bdr/admin/calendar" class="mt-3 inline-flex font-semibold underline">Retry schedule</a></section>
	{/if}

	<section class="rounded-lg border border-sky-200 bg-sky-50 p-4 text-sm text-sky-900">
		<div class="flex gap-3"><CloudOff class="mt-0.5 h-5 w-5 shrink-0" /><div><p class="font-semibold">Weather is not connected to an approved live provider.</p><p class="mt-1 leading-6">No forecast or temperature is shown here. Confirm conditions through the operating team’s approved source before scheduling weather-sensitive work.</p></div></div>
	</section>

	{#if data.scheduledRequest}
		<section class="rounded-lg border border-[var(--accent-border)] bg-[var(--accent-soft)] p-4"><p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--accent-text)]">Requested schedule context</p><h2 class="mt-2 font-semibold">{data.scheduledRequest.siteName || data.scheduledRequest.customerName}</h2><p class="mt-1 text-sm text-[var(--text-muted)]">{data.scheduledRequest.serviceAddress}</p><p class="mt-3 text-sm">{data.scheduledRequest.siteVisitSchedule ? 'This visit is present in the live schedule below.' : data.scheduledRequestQualification?.isQualified ? 'Qualified for scheduling. Book it from the request workspace.' : 'Qualification is incomplete; scheduling remains unavailable.'}</p></section>
	{/if}

	<section class="grid gap-4 xl:grid-cols-[1fr_22rem]">
		<div class="space-y-4">
			<article class="rounded-lg bg-white p-5 shadow-[var(--shell-shadow)]"><div class="flex items-center justify-between"><div><p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">Customer schedule</p><h2 class="mt-1 text-lg font-semibold">Site visits</h2></div><span class="rounded-full bg-[var(--shell-panel)] px-3 py-1 text-xs font-semibold">{data.scheduledVisitRequests.length}</span></div><div class="mt-4 divide-y divide-[var(--shell-border)]">{#each data.scheduledVisitRequests as request}{@const schedule = request.siteVisitSchedule!}<a href={`/bdr/admin/requests?request=${encodeURIComponent(request.id)}`} class="grid gap-3 py-4 sm:grid-cols-[auto_1fr_auto] sm:items-center"><CalendarDays class="h-5 w-5 text-[var(--accent-text)]" /><div><p class="font-semibold">{request.siteName || request.contactName || request.customerName}</p><p class="mt-1 text-sm text-[var(--text-muted)]">{dateLabel(schedule.visitDate)} · {timeLabel(schedule.windowStart)}–{timeLabel(schedule.windowEnd)} · {schedule.assignedFieldResource}</p><p class="mt-1 flex items-center gap-1 text-xs text-[var(--text-muted)]"><MapPin class="h-3.5 w-3.5" />{request.serviceAddress}</p></div><span class="text-xs font-semibold text-[var(--accent-text)]">Open request</span></a>{:else}<p class="py-10 text-center text-sm text-[var(--text-muted)]">No site visits are scheduled.</p>{/each}</div></article>

			<article class="rounded-lg bg-white p-5 shadow-[var(--shell-shadow)]"><div class="flex items-center justify-between"><div><p class="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--text-muted)]">Production schedule</p><h2 class="mt-1 text-lg font-semibold">Jobs</h2></div><span class="rounded-full bg-[var(--shell-panel)] px-3 py-1 text-xs font-semibold">{data.scheduledJobs.length}</span></div><div class="mt-4 divide-y divide-[var(--shell-border)]">{#each data.scheduledJobs as job}<a href={`/bdr/admin/jobs?job=${encodeURIComponent(job.id)}`} class="grid gap-3 py-4 sm:grid-cols-[auto_1fr_auto] sm:items-center"><Hammer class="h-5 w-5 text-[var(--accent-text)]" /><div><p class="font-semibold">{job.siteName || job.customerName}</p><p class="mt-1 text-sm text-[var(--text-muted)]">{dateLabel(job.scheduledDate)} · {timeLabel(job.windowStart)}–{timeLabel(job.windowEnd)} · {job.crew || 'Crew not assigned'}</p><p class="mt-1 text-xs text-[var(--text-muted)]">{job.invoiceNumber} · {formatCurrency(job.amount)} · {job.status.replace('-', ' ')}</p></div><span class="text-xs font-semibold text-[var(--accent-text)]">Open job</span></a>{:else}<p class="py-10 text-center text-sm text-[var(--text-muted)]">No production jobs are scheduled.</p>{/each}</div></article>
		</div>

		<aside class="h-fit rounded-lg bg-white p-5 shadow-[var(--shell-shadow)]"><div class="flex items-center justify-between"><h2 class="font-semibold">Ready to schedule</h2><span class="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">{data.scheduleReadyJobs.length}</span></div><p class="mt-1 text-sm text-[var(--text-muted)]">Invoices that passed the durable job-release gate.</p><div class="mt-4 space-y-3">{#each data.scheduleReadyJobs as job}<a href="/bdr/admin/jobs" class="block rounded-lg border border-[var(--shell-border)] p-3"><p class="font-semibold">{job.invoiceNumber}</p><p class="mt-1 text-xs text-[var(--text-muted)]">{job.siteName || job.customerName}</p><p class="mt-2 text-xs font-semibold text-emerald-700">{formatCurrency(job.amountPaid)} collected</p></a>{:else}<p class="rounded-lg border border-dashed border-[var(--shell-border)] p-5 text-center text-sm text-[var(--text-muted)]">Nothing waiting.</p>{/each}</div></aside>
	</section>

	<footer class="text-xs text-[var(--text-muted)]">Last refreshed {new Date(data.integrationState.loadedAtUtc).toLocaleString()} · request source {data.source}</footer>
</div>
