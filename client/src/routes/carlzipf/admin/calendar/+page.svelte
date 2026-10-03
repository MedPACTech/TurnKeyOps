<script lang="ts">
	import { page } from '$app/state';
	import { enhance } from '$app/forms';
	import type { PageProps } from './$types';
	let { data, form }: PageProps = $props();
	let saving = $state(false);
	const visits = $derived(data.requests.filter((request) => request.siteVisitSchedule).sort((a, b) =>
		`${a.siteVisitSchedule?.visitDate}${a.siteVisitSchedule?.windowStart}`.localeCompare(`${b.siteVisitSchedule?.visitDate}${b.siteVisitSchedule?.windowStart}`)));
	const ready = $derived(data.requests.filter((request) => ['qualified', 'contacted'].includes(request.status) && !request.siteVisitSchedule));
	const selected = $derived(data.requests.find((request) => request.id === page.url.searchParams.get('request')));
	const jobType = $derived(selected?.propertyType?.toLowerCase());
	const eligible = $derived(data.technicians.filter((tech) => jobType === 'residential' || jobType === 'commercial' ? tech.jobTypes.includes(jobType) : false));
	const canSchedule = $derived(selected && ['qualified', 'contacted', 'inspection-scheduled'].includes(selected.status));
	const date = (value: string) => new Date(`${value}T12:00:00`).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
</script>

<svelte:head><title>Calendar · Carl Zipf</title></svelte:head>
<div class="mx-auto max-w-6xl space-y-6 pb-10">
	<header><p class="text-sm font-semibold text-[var(--accent-text)]">Carl Zipf Lock Shop</p><h1 class="mt-2 text-3xl font-bold">Calendar</h1><p class="mt-2 text-sm text-slate-600">One assessment schedule for residential and commercial work. Technician choices follow their assigned job types.</p></header>
	{#if data.loadError}<p role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">{data.loadError} <a class="underline" href="/carlzipf/admin/calendar">Retry</a></p>{/if}
	{#if form?.error}<p role="alert" class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">{form.error}</p>{/if}
	{#if form?.message}<p role="status" class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">{form.message}</p>{/if}
	<div class="grid gap-5 lg:grid-cols-[1fr_21rem]">
		<section class="space-y-3" aria-label="Scheduled assessments">
			<h2 class="text-lg font-bold">Scheduled assessments <span class="ml-2 text-sm font-normal text-slate-500">{visits.length}</span></h2>
			{#each visits as request}{@const visit = request.siteVisitSchedule!}
				<a href={`?request=${encodeURIComponent(request.id)}`} class="block rounded-xl border bg-white p-5 shadow-sm hover:border-[var(--accent-border)]">
					<div class="flex flex-wrap items-start justify-between gap-2"><h3 class="font-bold">{request.siteName || request.customerName}</h3><span class="rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-semibold capitalize text-[var(--accent-text)]">{request.propertyType}</span></div>
					<p class="mt-2 text-sm font-semibold">{date(visit.visitDate)} · {visit.windowStart}–{visit.windowEnd}</p>
					<p class="mt-1 text-sm text-slate-600">{visit.assignedFieldResource} · {request.serviceType || request.projectType}</p>
					<p class="mt-1 text-xs text-slate-500">{request.serviceAddress}</p>
				</a>
			{:else}<p class="rounded-xl border border-dashed bg-white p-8 text-sm text-slate-600">No assessments scheduled yet.</p>{/each}
		</section>
		<aside class="space-y-5">
			<section class="rounded-xl border bg-white p-5"><h2 class="font-bold">Ready to schedule <span class="ml-1 text-sm font-normal text-slate-500">{ready.length}</span></h2><p class="mt-1 text-sm text-slate-600">Qualified and contacted requests awaiting an assessment.</p><div class="mt-4 divide-y">{#each ready as request}<a class="block py-3" href={`?request=${encodeURIComponent(request.id)}`}><p class="font-semibold">{request.siteName || request.customerName}</p><p class="mt-1 text-xs capitalize text-slate-500">{request.propertyType} · {request.serviceType || request.projectType}</p></a>{:else}<p class="py-4 text-sm text-slate-500">Nothing waiting. Qualify a request before booking.</p>{/each}</div><a class="mt-3 inline-block text-sm font-semibold underline" href="/carlzipf/admin/requests">Open request inbox →</a></section>
			{#if selected}<section class="rounded-xl border bg-white p-5"><h2 class="font-bold">{selected.siteVisitSchedule ? 'Reschedule assessment' : 'Schedule assessment'}</h2><p class="mt-1 text-sm text-slate-600">{selected.customerName} · <span class="capitalize">{selected.propertyType}</span></p>
				{#if !canSchedule}<p class="mt-4 text-sm text-amber-900">Qualify or contact this request in the <a class="underline" href={`/carlzipf/admin/requests?request=${encodeURIComponent(selected.id)}`}>request inbox</a> before scheduling.</p>
				{:else if !eligible.length}<p class="mt-4 text-sm text-amber-900">No active technician has this job type. Assign Residential or Commercial in <a class="underline" href="/carlzipf/admin/users">Users</a>.</p>
				{:else}<form method="POST" action="?/schedule" class="mt-4 grid gap-3" use:enhance={() => { saving = true; return async ({ update }) => { await update(); saving = false; }; }}>
					<input type="hidden" name="requestId" value={selected.id} /><input type="hidden" name="updatedAtUtc" value={selected.updatedAtUtc} />
					<label class="grid gap-1 text-sm font-semibold">Date<input class="rounded border p-3 font-normal" type="date" name="visitDate" required value={selected.siteVisitSchedule?.visitDate ?? ''} /></label>
					<div class="grid grid-cols-2 gap-3"><label class="grid gap-1 text-sm font-semibold">Start<input class="rounded border p-3 font-normal" type="time" name="windowStart" required value={selected.siteVisitSchedule?.windowStart ?? '09:00'} /></label><label class="grid gap-1 text-sm font-semibold">End<input class="rounded border p-3 font-normal" type="time" name="windowEnd" required value={selected.siteVisitSchedule?.windowEnd ?? '10:30'} /></label></div>
					<label class="grid gap-1 text-sm font-semibold">Technician<select class="rounded border p-3 font-normal" name="membershipId" required><option value="">Choose a technician</option>{#each eligible as tech}<option value={tech.membershipId} selected={selected.siteVisitSchedule?.assignedFieldResource === tech.label}>{tech.label}</option>{/each}</select></label>
					<label class="grid gap-1 text-sm font-semibold">Visit notes<textarea class="rounded border p-3 font-normal" name="notes" rows="3" maxlength="1000">{selected.siteVisitSchedule?.notes ?? ''}</textarea></label>
					<button class="min-h-11 rounded-lg bg-[var(--accent-text)] px-4 font-semibold text-white disabled:opacity-50" disabled={saving}>{saving ? 'Saving…' : selected.siteVisitSchedule ? 'Update assessment' : 'Schedule assessment'}</button>
					<p class="text-xs text-slate-500">Checks this shared request calendar for technician overlaps. Booking here does not send a customer message.</p>
				</form>{/if}
			</section>{/if}
		</aside>
	</div>
</div>
