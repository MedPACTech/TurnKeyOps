<script lang="ts">
 import { enhance } from '$app/forms';
 import { goto } from '$app/navigation';
 import { page } from '$app/state';
 import { quoteRequestStatuses, quoteRequestStatusMeta } from '$lib/quote-requests';
 import { triageStatuses } from '../records';
 import type { PageProps } from './$types';
 import type { QuoteRequestStatus } from '$lib/quote-requests';
 let { data, form }: PageProps = $props();
 let query = $state('');
 let jobType = $state('');
 let statusFilter = $state('');
 let saving = $state(false);
 let drawer: HTMLDialogElement;
 const filtered = $derived(data.requests.filter(record => (!jobType || record.propertyType.toLowerCase() === jobType) && (!statusFilter || record.status === statusFilter) && [record.customerName, record.companyName, record.serviceAddress, record.serviceType, record.email, record.phone].join(' ').toLowerCase().includes(query.toLowerCase().trim())));
 const view = $derived(page.url.searchParams.get('view') === 'card' ? 'card' : 'table');
 const stages: { label: string; hint: string; statuses: QuoteRequestStatus[] }[] = [
  { label: 'New', hint: 'Make first contact', statuses: ['new'] },
  { label: 'Connect', hint: 'Confirm the need', statuses: ['in-review', 'needs-info', 'qualified', 'contacted'] },
  { label: 'Prepare', hint: 'Assess and price', statuses: ['inspection-scheduled', 'estimate-drafted'] },
  { label: 'Decide', hint: 'Follow up on the estimate', statuses: ['estimate-sent'] },
  { label: 'Won', hint: 'Customer accepted', statuses: ['won'] },
  { label: 'Closed', hint: 'No longer active', statuses: ['closed'] }
 ];
 const active = $derived(filtered.filter(record => !['won', 'closed'].includes(record.status)));
 const upNext = $derived([...active].sort((a, b) => {
  const priority = { emergency: 2, priority: 1, standard: 0 };
  return (priority[b.priority] ?? 0) - (priority[a.priority] ?? 0)
   || Number(b.status === 'new') - Number(a.status === 'new')
   || a.submittedAtUtc.localeCompare(b.submittedAtUtc);
 })[0]);
 function leadUrl(id: string) {
  const url = new URL(page.url);
  url.searchParams.set('request', id);
  return url.pathname + url.search;
 }
 function openLead(event: MouseEvent & { currentTarget: EventTarget & HTMLAnchorElement }) {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  void goto(event.currentTarget.href, { keepFocus: true, noScroll: true });
 }
 function setView(next: 'table' | 'card') {
  const url = new URL(page.url);
  url.searchParams.set('view', next);
  void goto(url, { keepFocus: true, noScroll: true });
 }
 const selected = $derived(data.requests.find(record => record.id === page.url.searchParams.get('request')));
 const statusLabel = (status: string) => quoteRequestStatusMeta[status as keyof typeof quoteRequestStatusMeta]?.label ?? status.replaceAll('-', ' ');
 const statusTone = (status: string) => quoteRequestStatusMeta[status as keyof typeof quoteRequestStatusMeta]?.tone ?? 'slate';
 const sourceLabel = (source: string) => ({ 'public-site': 'Website', office: 'Office', referral: 'Referral' }[source] ?? (source || 'Not recorded'));
 const date = (value: string) => new Date(value).toLocaleDateString('en-US', { timeZone: 'America/New_York', month: 'short', day: 'numeric', year: 'numeric' });
 function closeDrawer() {
  const url = new URL(page.url);
  url.searchParams.delete('request');
  void goto(url, { keepFocus: true, noScroll: true });
 }
 $effect(() => {
  if (selected && drawer) {
   if (!drawer.open) drawer.showModal();
   const overflow = document.body.style.overflow;
   document.body.style.overflow = 'hidden';
   return () => { document.body.style.overflow = overflow; };
  }
  drawer?.close();
 });
</script>

<svelte:head><title>Leads · Carl Zipf</title></svelte:head>
<div class="mx-auto max-w-7xl space-y-5 pb-10">
 <header><p class="text-sm font-semibold text-[var(--accent-text)]">Carl Zipf Lock Shop</p><h1 class="mt-2 text-3xl font-bold">Leads</h1><p class="mt-2 text-sm text-slate-600">Potential jobs, from first conversation to next step.</p></header>
 {#if data.loadError}<div role="alert" class="rounded-lg bg-red-50 p-4 text-red-800">{data.loadError} <a class="underline" href="/carlzipf/admin/requests">Retry</a></div>{/if}
 {#if !selected && form?.error}<p role="alert" class="rounded-lg bg-red-50 p-4 text-red-800">{form.error}</p>{/if}
 {#if !selected && form?.message}<p role="status" class="rounded-lg bg-emerald-50 p-4 text-emerald-800">{form.message}</p>{/if}
 <div class="flex flex-wrap gap-3">
  <label class="min-w-48 grow"><span class="sr-only">Search leads</span><input class="w-full rounded-lg border bg-white p-3" placeholder="Search customer, address, or service" bind:value={query} /></label>
  <label><span class="sr-only">Lead status filter</span><select class="min-h-12 rounded-lg border bg-white p-3" bind:value={statusFilter}><option value="">All statuses</option>{#each quoteRequestStatuses as status}<option value={status}>{statusLabel(status)}</option>{/each}</select></label>
  <label><span class="sr-only">Job type</span><select class="min-h-12 rounded-lg border bg-white p-3" bind:value={jobType}><option value="">All job types</option><option value="residential">Residential</option><option value="commercial">Commercial</option></select></label>
 </div>
 <div class="flex flex-wrap items-center justify-between gap-3">
  <p class="text-sm text-slate-500" role="status">{filtered.length} {filtered.length === 1 ? 'lead' : 'leads'}{filtered.length !== data.requests.length ? ` of ${data.requests.length}` : ''}</p>
  <div role="group" aria-label="Lead view" class="flex gap-1 rounded-lg border bg-white p-1">
   <button type="button" aria-pressed={view === 'table'} class="view-button min-h-11 rounded-md px-4 text-sm font-semibold" onclick={() => setView('table')}>Table</button>
   <button type="button" aria-pressed={view === 'card'} class="view-button min-h-11 rounded-md px-4 text-sm font-semibold" onclick={() => setView('card')}>Card</button>
  </div>
 </div>
 {#if !data.loadError}
  <dl class="flex flex-wrap gap-x-8 gap-y-3 text-sm">
   <div><dt class="text-slate-500">Open</dt><dd class="mt-1 text-xl font-semibold">{active.length}</dd></div>
   <div><dt class="text-slate-500">Unassigned</dt><dd class="mt-1 text-xl font-semibold">{active.filter(record => !record.assignedTo?.trim()).length}</dd></div>
   <div><dt class="text-slate-500">Won</dt><dd class="mt-1 text-xl font-semibold">{filtered.filter(record => record.status === 'won').length}</dd></div>
  </dl>
 {/if}
 {#if filtered.length}
  {#if view === 'table'}
   {#if upNext}
    <section aria-labelledby="up-next-title" class="lead-row rounded-r-lg bg-[var(--accent-soft)] p-5" data-tone={statusTone(upNext.status)}>
     <div class="flex flex-wrap items-center justify-between gap-4">
      <div><p class="text-xs font-semibold uppercase tracking-wide text-[var(--accent-text)]">Up next</p><h2 id="up-next-title" class="mt-2 text-lg font-semibold">{upNext.customerName || upNext.companyName || 'Unnamed lead'}</h2><p class="mt-1 text-sm">{upNext.nextAction || 'Review the lead and set a follow-up'}</p><p class="mt-2 text-xs text-slate-600">{upNext.priority === 'emergency' ? 'Emergency priority' : upNext.priority === 'priority' ? 'Priority lead' : upNext.status === 'new' ? 'New lead awaiting review' : 'Oldest active lead'} · {upNext.assignedTo || 'Unassigned'}</p></div>
      <a href={leadUrl(upNext.id)} onclick={openLead} aria-haspopup="dialog" class="inline-flex min-h-11 items-center rounded-lg bg-[var(--accent-text)] px-4 text-sm font-semibold text-white">Review lead →</a>
     </div>
    </section>
   {/if}
   <section aria-label="Lead table" class="overflow-x-auto rounded-xl border bg-white">
    <table class="w-full min-w-[760px] text-left text-sm">
     <caption class="sr-only">Leads with status, owner, next action, and received date</caption>
     <thead class="border-b bg-slate-50 text-xs text-slate-600"><tr><th scope="col" class="px-5 py-3">Lead / source</th><th scope="col" class="px-4 py-3">Status</th><th scope="col" class="px-4 py-3">Owner</th><th scope="col" class="px-4 py-3">Next action</th><th scope="col" class="px-4 py-3">Received</th></tr></thead>
     <tbody>{#each filtered as record (record.id)}
      <tr class="border-b last:border-0 hover:bg-slate-50" class:bg-slate-100={selected?.id === record.id}>
       <th scope="row" class="lead-row max-w-72 px-5 py-4 font-normal" data-tone={statusTone(record.status)}><a href={leadUrl(record.id)} onclick={openLead} aria-haspopup="dialog" class="inline-block py-1 font-semibold underline decoration-slate-300 underline-offset-4">{record.customerName || record.companyName || 'Unnamed lead'}</a><p class="mt-1 text-xs text-slate-600">{record.serviceType || 'Service not specified'}</p><p class="mt-1 text-xs text-slate-500">{sourceLabel(record.source)} · {record.propertyType}</p></th>
       <td class="px-4 py-4"><span class="status-label" data-tone={statusTone(record.status)}>{statusLabel(record.status)}</span>{#if record.priority !== 'standard'}<p class="mt-2 text-xs font-semibold capitalize text-amber-800">{record.priority}</p>{/if}</td>
       <td class="px-4 py-4">{record.assignedTo || 'Unassigned'}</td>
       <td class="max-w-72 px-4 py-4"><p class="line-clamp-2">{record.nextAction || 'Set a follow-up'}</p></td>
       <td class="whitespace-nowrap px-4 py-4 text-xs text-slate-500">{date(record.submittedAtUtc)}</td>
      </tr>
     {/each}</tbody>
    </table>
   </section>
  {:else}
   <section aria-label="Lead card board" class="grid items-start gap-5 sm:grid-cols-2 xl:grid-cols-3">
    {#each stages as stage}
     {@const leads = filtered.filter(record => stage.statuses.includes(record.status))}
     <section aria-label={`${stage.label} leads`} class="min-w-0">
      <header class="mb-3 border-b pb-3"><div class="flex items-center justify-between gap-2"><h2 class="font-semibold">{stage.label}</h2><span class="text-sm text-slate-500">{leads.length}</span></div><p class="mt-1 text-xs text-slate-500">{stage.hint}</p></header>
      <div class="space-y-3">{#each leads as record (record.id)}
       <a href={leadUrl(record.id)} onclick={openLead} aria-haspopup="dialog" class="lead-row block rounded-r-lg border border-slate-200 bg-white p-4 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-slate-900" data-tone={statusTone(record.status)} class:bg-slate-100={selected?.id === record.id}>
        <div class="flex flex-wrap items-start justify-between gap-2"><h3 class="font-semibold">{record.customerName || record.companyName || 'Unnamed lead'}</h3><span class="status-label" data-tone={statusTone(record.status)}>{statusLabel(record.status)}</span></div>
        <p class="mt-2 text-sm">{record.serviceType || 'Service not specified'}</p><p class="mt-1 text-xs text-slate-500">{record.serviceAddress || 'Address not supplied'}</p>
        <p class="mt-3 text-xs text-slate-600">{sourceLabel(record.source)} · {record.assignedTo || 'Unassigned'}</p>
        {#if record.priority !== 'standard'}<p class="mt-2 text-xs font-semibold capitalize text-amber-800">{record.priority}</p>{/if}
        <p class="mt-3 text-xs font-semibold text-slate-500">Next action</p><p class="mt-1 line-clamp-2 text-sm">{record.nextAction || 'Set a follow-up'}</p><p class="mt-3 text-xs text-slate-500">Received {date(record.submittedAtUtc)}</p>
       </a>
      {:else}<p class="py-5 text-sm text-slate-500">No leads in this stage.</p>{/each}</div>
     </section>
    {/each}
   </section>
  {/if}
 {:else if !data.loadError}
  <p class="rounded-xl border bg-white p-8 text-center text-sm text-slate-500">{data.requests.length ? 'No leads match these filters.' : 'No leads yet. New website submissions will appear here.'}</p>
 {/if}
 {#if data.selectedId && !selected && !data.loadError}<p role="status" class="text-sm text-slate-600">This lead is not available. Select another lead.</p>{/if}
</div>

<dialog bind:this={drawer} aria-labelledby="lead-title" oncancel={(event) => { event.preventDefault(); closeDrawer(); }} class="lead-drawer">
 {#if selected}
  <header class="sticky top-0 z-10 border-b bg-white px-6 py-5">
   <div class="flex items-start justify-between gap-4"><div><p class="text-xs font-semibold uppercase tracking-wide text-slate-500">Lead details</p><h2 id="lead-title" class="mt-2 text-xl font-bold">{selected.customerName || selected.companyName || 'Unnamed lead'}</h2></div><button type="button" onclick={closeDrawer} class="min-h-11 rounded-lg border px-4 text-sm font-semibold">Close</button></div>
   <div class="mt-3 flex flex-wrap items-center gap-3"><span class="status-label" data-tone={statusTone(selected.status)}>{statusLabel(selected.status)}</span><span class="text-xs text-slate-500">{sourceLabel(selected.source)} · {date(selected.submittedAtUtc)}</span></div>
  </header>
  <div class="space-y-6 px-6 py-6">
   {#if form?.error}<p role="alert" class="rounded-lg bg-red-50 p-4 text-red-800">{form.error}</p>{/if}
   {#if form?.message}<p role="status" class="rounded-lg bg-emerald-50 p-4 text-emerald-800">{form.message}</p>{/if}
   <section aria-labelledby="contact-title"><h3 id="contact-title" class="font-semibold">Contact</h3>{#if selected.companyName}<p class="mt-2 text-sm">{selected.companyName}</p>{/if}<div class="mt-2 flex flex-wrap gap-x-5 gap-y-3 text-sm">{#if selected.phone}<a class="underline" href={`tel:${selected.phone}`}>{selected.phone}</a>{/if}{#if selected.email}<a class="break-all underline" href={`mailto:${selected.email}`}>{selected.email}</a>{/if}{#if !selected.phone && !selected.email}<p class="text-slate-500">No contact details supplied.</p>{/if}</div></section>
   <section aria-labelledby="need-title" class="border-t pt-5"><h3 id="need-title" class="font-semibold">Job need</h3><p class="mt-2 text-sm">{selected.serviceType} · {selected.propertyType}</p>{#if selected.siteName}<p class="mt-2 text-sm">{selected.siteName}</p>{/if}<p class="mt-1 text-sm text-slate-600">{selected.serviceAddress || 'Address not supplied'}</p><p class="mt-4 whitespace-pre-wrap text-sm">{selected.need || selected.message || 'No details supplied.'}</p><dl class="mt-4 grid grid-cols-2 gap-4 text-sm"><div><dt class="text-slate-500">Requested timing</dt><dd class="mt-1">{selected.requestedTimeline || 'Not supplied'}</dd></div><div><dt class="text-slate-500">Assigned to</dt><dd class="mt-1">{selected.assignedTo || 'Unassigned'}</dd></div></dl></section>
   <section aria-labelledby="followup-title" class="border-t pt-5"><h3 id="followup-title" class="font-semibold">Follow-up</h3>
    {#key selected.id + selected.updatedAtUtc}<form method="POST" action={`?/update&request=${encodeURIComponent(selected.id)}&view=${view}`} class="mt-4 grid gap-4" use:enhance={() => { saving = true; return async ({ update }) => { try { await update({ reset: false }); } finally { saving = false; } }; }}>
     <input type="hidden" name="id" value={selected.id} /><input type="hidden" name="updatedAtUtc" value={selected.updatedAtUtc} />
     <label class="grid gap-2 text-sm font-semibold">Lead status<select name="status" class="rounded-lg border bg-white p-3 font-normal" value={selected.status}>{#each triageStatuses(selected.status) as status}<option value={status}>{statusLabel(status)}</option>{/each}</select></label>
     <label class="grid gap-2 text-sm font-semibold">Next action<textarea name="nextAction" required maxlength="1000" rows="3" class="rounded-lg border p-3 font-normal">{selected.nextAction}</textarea></label>
     <button disabled={saving} class="min-h-11 rounded-lg bg-[var(--accent-text)] px-5 font-semibold text-white disabled:opacity-50">{saving ? 'Saving…' : 'Save follow-up'}</button>
    </form>{/key}
   </section>
   {#if selected.attachments.length}<section aria-labelledby="photos-title" class="border-t pt-5"><h3 id="photos-title" class="font-semibold">Photos <span class="font-normal text-slate-500">{selected.attachments.length}</span></h3><ul class="mt-3 space-y-3">{#each selected.attachments as attachment}<li><a class="break-all text-sm underline" href={`/carlzipf/admin/requests/attachments/${selected.id}/${attachment.id}`}>{attachment.fileName}</a></li>{/each}</ul></section>{/if}
   <section aria-labelledby="related-title" class="border-t pt-5"><h3 id="related-title" class="font-semibold">Related work</h3><div class="mt-3 grid gap-4 text-sm font-semibold"><a href={`/carlzipf/admin/estimates?request=${selected.id}`} class="underline">Review linked estimate →</a><a href={`/carlzipf/admin/calendar?request=${selected.id}`} class="underline">{selected.siteVisitSchedule ? 'Review scheduled visit' : 'Schedule assessment visit'} →</a><a href={`/carlzipf/admin/customers?fromRequest=${selected.id}`} class="underline">Review as a new customer →</a></div><p class="mt-3 text-xs text-slate-500">Check existing contacts before adding a customer. Saving a customer does not schedule a job.</p></section>
   <details class="border-t pt-5"><summary class="cursor-pointer font-semibold">Lead history <span class="font-normal text-slate-500">{selected.timeline.length}</span></summary><ol class="mt-4 space-y-4">{#each selected.timeline as event}<li class="text-sm"><p>{event.label}</p>{#if event.note}<p class="mt-1 whitespace-pre-wrap text-slate-600">{event.note}</p>{/if}<p class="mt-1 text-xs text-slate-500">{event.actor} · {new Date(event.occurredAtUtc).toLocaleString('en-US', { timeZone: 'America/New_York' })}</p></li>{:else}<li class="text-sm text-slate-500">No activity recorded yet.</li>{/each}</ol></details>
  </div>
 {/if}
</dialog>

<style>
 [data-tone='amber'] { --status-color: #b45309; --status-bg: #fffbeb; }
 [data-tone='blue'] { --status-color: #1d4ed8; --status-bg: #eff6ff; }
 [data-tone='violet'] { --status-color: #6d28d9; --status-bg: #f5f3ff; }
 [data-tone='emerald'] { --status-color: #047857; --status-bg: #ecfdf5; }
 [data-tone='slate'] { --status-color: #475569; --status-bg: #f1f5f9; }
 .lead-row { border-left: 4px solid var(--status-color, #475569); }
 .view-button[aria-pressed='true'] { background: var(--accent-text); color: white; }
 .status-label { display: inline-block; border-radius: 4px; padding: 3px 8px; font-size: 12px; font-weight: 600; color: var(--status-color, #475569); background: var(--status-bg, #f1f5f9); }
 .lead-drawer { position: fixed; inset: 0 0 0 auto; margin: 0; width: min(100%, 38rem); max-width: 100%; height: 100dvh; max-height: 100dvh; border: 0; border-left: 1px solid #e2e8f0; padding: 0; overflow-y: auto; overscroll-behavior: contain; color: #0f172a; background: white; box-shadow: -12px 0 40px #0f172a1a; }
 .lead-drawer::backdrop { background: #0f172a66; }
</style>
