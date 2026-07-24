<script lang="ts">
	import { CalendarDays, Clock3, MapPin, Plus } from 'lucide-svelte';
	import type { PageProps } from './$types';
	let { data }: PageProps = $props();
	const dateLabel = (value: string) =>
		new Date(`${value}T12:00:00`).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
	const timeLabel = (value: string) =>
		new Date(`2026-01-01T${value}:00`).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
</script>

<svelte:head><title>Calendar · Think Pink</title></svelte:head>
<div class="mx-auto max-w-6xl space-y-6 pb-10">
	<header>
		<p class="text-xs font-bold uppercase tracking-[.18em] text-[var(--accent-text)]">Operations</p>
		<h1 class="mt-2 text-3xl font-black text-[var(--text-strong)]">Calendar</h1>
		<p class="mt-2 text-sm text-[var(--text-muted)]">Live property assessments and scheduled site visits for Think Pink.</p>
	</header>
	<div class="grid gap-5 lg:grid-cols-[1fr_20rem]">
		<section class="space-y-3">
			{#each data.visits as visit}
				<article class="rounded-xl bg-white p-5 shadow-[var(--shell-shadow)]">
					<div class="flex gap-4">
						<div class="flex h-11 w-11 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-[var(--accent-text)]"><CalendarDays class="h-5 w-5" /></div>
						<div class="min-w-0 flex-1">
							<div class="flex flex-wrap items-start justify-between gap-2">
								<h2 class="font-bold text-[var(--text-strong)]">{visit.customer}</h2>
								<span class="rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-bold text-[var(--accent-text)]">{visit.status}</span>
							</div>
							<p class="mt-2 text-sm font-semibold">{dateLabel(visit.visitDate)} · {timeLabel(visit.windowStart)}–{timeLabel(visit.windowEnd)}</p>
							<p class="mt-2 flex gap-2 text-sm text-[var(--text-muted)]"><MapPin class="h-4 w-4" />{visit.address}</p>
							<p class="mt-1 flex gap-2 text-sm text-[var(--text-muted)]"><Clock3 class="h-4 w-4" />{visit.assignedFieldResource || 'Field estimator'} · {visit.service}</p>
						</div>
					</div>
				</article>
			{:else}
				<div class="rounded-xl border border-dashed border-[var(--shell-border)] bg-white p-10 text-center text-sm text-[var(--text-muted)]">No property assessments are scheduled yet.</div>
			{/each}
		</section>
		<aside class="rounded-xl bg-white p-5 shadow-[var(--shell-shadow)]">
			<h2 class="font-bold text-[var(--text-strong)]">Ready to schedule</h2>
			<p class="mt-1 text-sm text-[var(--text-muted)]">Qualified requests without a property assessment.</p>
			<div class="mt-4 space-y-3">
				{#each data.unscheduled as request}
					<a href={`/thinkpink/admin/requests?request=${encodeURIComponent(request.id)}`} class="block rounded-lg border border-[var(--shell-border)] p-3 hover:border-[var(--accent-border)]">
						<p class="font-semibold">{request.siteName || request.contactName}</p>
						<p class="mt-1 text-xs text-[var(--text-muted)]">{request.serviceType || request.projectType}</p>
					</a>
				{:else}<p class="text-sm text-[var(--text-muted)]">Nothing waiting.</p>{/each}
			</div>
			<a href="/thinkpink/admin/requests" class="mt-5 flex min-h-11 items-center justify-center gap-2 rounded-lg bg-[var(--accent-text)] px-4 text-sm font-bold text-white"><Plus class="h-4 w-4" />Open requests</a>
		</aside>
	</div>
</div>
