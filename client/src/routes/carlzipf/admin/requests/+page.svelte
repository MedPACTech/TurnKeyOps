<script lang="ts">
 import { enhance } from '$app/forms';
 import { page } from '$app/state';
 import { triageStatuses } from '../records';
 import type { PageProps } from './$types';
 let { data, form }: PageProps = $props();
 let query = $state(''); let jobType = $state(''); let saving = $state(false);
 const filtered = $derived(data.requests.filter(record => (!jobType || record.propertyType.toLowerCase() === jobType) && [record.customerName, record.companyName, record.serviceAddress, record.serviceType, record.email, record.phone].join(' ').toLowerCase().includes(query.toLowerCase().trim())));
 const selected = $derived(data.requests.find(record => record.id === page.url.searchParams.get('request')));
</script>
<svelte:head><title>Requests · Carl Zipf</title></svelte:head>
<div class="mx-auto max-w-7xl space-y-5 pb-10">
 <header><p class="text-sm font-semibold text-[var(--accent-text)]">Carl Zipf Lock Shop</p><h1 class="mt-2 text-3xl font-bold">Request inbox</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Residential and commercial requests, photos, and customer follow-up.</p></header>
 {#if data.loadError}<div role="alert" class="rounded-lg bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]">{data.loadError} <a class="underline" href="/carlzipf/admin/requests">Retry</a></div>{/if}
 {#if form?.error}<p role="alert" class="rounded-lg bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]">{form.error}</p>{/if}
 {#if form?.message}<p role="status" class="rounded-lg bg-[var(--positive-soft)] p-4 text-[var(--positive-text)]">{form.message}</p>{/if}
 <div class="flex flex-wrap gap-3"><label class="grow"><span class="sr-only">Search requests</span><input class="w-full rounded-lg border bg-[var(--surface)] p-3" placeholder="Search customer, address, or service" bind:value={query} /></label><label><span class="sr-only">Job type</span><select class="h-full rounded-lg border bg-[var(--surface)] p-3" bind:value={jobType}><option value="">All job types</option><option value="residential">Residential</option><option value="commercial">Commercial</option></select></label></div>
 <div class="grid gap-5 lg:grid-cols-[.9fr_1.1fr]">
  <section aria-label="Requests" class="h-fit overflow-hidden rounded-xl border bg-[var(--surface)]">
   {#each filtered as record}<a class="block border-b p-5 hover:bg-[var(--surface-subtle)]" class:admin-selected={selected?.id === record.id} href={`?request=${encodeURIComponent(record.id)}`} aria-current={selected?.id === record.id ? 'true' : undefined}><div class="flex justify-between gap-3"><h2 class="font-semibold">{record.customerName}</h2><span class="text-xs capitalize">{record.status.replaceAll('-', ' ')}</span></div><p class="mt-2 text-sm">{record.serviceType} · {record.propertyType}</p><p class="mt-1 text-sm text-[var(--text-muted)]">{record.serviceAddress}</p><p class="mt-2 text-xs text-[var(--text-muted)]">{new Date(record.submittedAtUtc).toLocaleDateString('en-US', { timeZone: 'America/New_York' })} · {record.attachments.length} photos</p></a>
   {:else}{#if !data.loadError}<p class="p-8 text-center text-sm text-[var(--text-muted)]">{data.requests.length ? 'No requests match these filters.' : 'No requests yet. New website submissions will appear here.'}</p>{/if}{/each}
  </section>
  {#if selected}<section aria-label="Request detail" class="rounded-xl border bg-[var(--surface)] p-6">
   <h2 class="text-xl font-bold">{selected.customerName}</h2><p class="mt-2 text-sm text-[var(--text-muted)]">{selected.companyName} {selected.siteName}</p>
   <div class="mt-4 flex flex-wrap gap-4 text-sm">{#if selected.phone}<a class="underline" href={`tel:${selected.phone}`}>{selected.phone}</a>{/if}{#if selected.email}<a class="break-all underline" href={`mailto:${selected.email}`}>{selected.email}</a>{/if}</div>
   <p class="mt-5 text-sm">{selected.serviceAddress}</p><p class="mt-3 whitespace-pre-wrap">{selected.need || selected.message}</p><p class="mt-3 text-sm text-[var(--text-muted)]">Requested timing: {selected.requestedTimeline || 'Not supplied'}</p>
   {#if selected.attachments.length}<h3 class="mt-6 font-semibold">Customer photos</h3><ul class="mt-2 space-y-2">{#each selected.attachments as attachment}<li><a class="text-sm underline" href={`/carlzipf/admin/requests/attachments/${selected.id}/${attachment.id}`}>{attachment.fileName}</a></li>{/each}</ul>{/if}
   {#key selected.id + selected.updatedAtUtc}<form method="POST" action={`?/update&request=${selected.id}`} class="mt-6 grid gap-4 border-t pt-5" use:enhance={() => { saving = true; return async ({ update }) => { await update(); saving = false; }; }}>
    <input type="hidden" name="id" value={selected.id} /><input type="hidden" name="updatedAtUtc" value={selected.updatedAtUtc} />
    <label class="grid gap-2 text-sm font-semibold">Request status<select name="status" class="rounded border p-3 font-normal" value={selected.status}>{#each triageStatuses(selected.status) as status}<option value={status}>{status.replaceAll('-', ' ')}</option>{/each}</select></label>
    <label class="grid gap-2 text-sm font-semibold">Next action<textarea name="nextAction" required maxlength="1000" rows="3" class="rounded border p-3 font-normal">{selected.nextAction}</textarea></label>
    <button disabled={saving} class="admin-primary min-h-11 rounded-lg bg-[var(--cta)] px-5 font-semibold text-white disabled:opacity-50">{saving ? 'Saving…' : 'Save follow-up'}</button>
   </form>{/key}
   <a href={`/carlzipf/admin/estimates?request=${selected.id}`} class="mt-5 block text-sm font-semibold underline">Review linked estimate →</a>
   <a href={`/carlzipf/admin/calendar?request=${selected.id}`} class="mt-3 block text-sm font-semibold underline">{selected.siteVisitSchedule ? 'Review scheduled visit' : 'Schedule assessment visit'} →</a>
   <a href={`/carlzipf/admin/customers?fromRequest=${selected.id}`} class="mt-5 block text-sm font-semibold underline">Review as a new customer →</a><p class="mt-2 text-xs text-[var(--text-muted)]">Review existing contacts first to avoid duplicates. Saving a customer does not schedule a job.</p>
   <details class="mt-6 border-t pt-4"><summary class="cursor-pointer text-sm font-semibold">Request history</summary><ol class="mt-3 space-y-3">{#each selected.timeline as event}<li class="text-sm"><p>{event.label}</p><p class="text-xs text-[var(--text-muted)]">{event.actor} · {new Date(event.occurredAtUtc).toLocaleString('en-US', { timeZone: 'America/New_York' })}</p></li>{/each}</ol></details>
  </section>{:else}<div class="rounded-xl border border-dashed p-10 text-center text-sm text-[var(--text-muted)]">{data.selectedId ? 'This request is not available. Select another request.' : 'Select a request to review details and save follow-up.'}</div>{/if}
 </div>
</div>
