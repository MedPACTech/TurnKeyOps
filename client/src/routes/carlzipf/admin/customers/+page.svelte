<script lang="ts">
 import { enhance } from '$app/forms';
 import { page } from '$app/state';
 import { customerName } from '../records';
 import type { PageProps } from './$types';
 let { data, form }: PageProps = $props();
 let query = $state(''); let saving = $state(false);
 const filtered = $derived(data.customers.filter(customer => [customerName(customer), customer.companyName, customer.email, customer.phone, customer.address].join(' ').toLowerCase().includes(query.toLowerCase().trim())));
 const selected = $derived(data.customers.find(customer => customer.id === page.url.searchParams.get('customer')));
 const editing = $derived(Boolean(selected));
 const values = $derived(selected ?? data.draft ?? {});
 const fields = [ ['firstName', 'First name / contact name'], ['lastName', 'Last name'], ['companyName', 'Company'], ['email', 'Email'], ['phone', 'Phone'], ['address', 'Street address'], ['city', 'City'], ['state', 'State'], ['zip', 'Postal code'] ] as const;
</script>
<svelte:head><title>Contacts · Carl Zipf</title></svelte:head>
<div class="mx-auto max-w-7xl space-y-5 pb-10">
 <header><p class="text-sm font-semibold text-[var(--accent-text)]">Relationships</p><h1 class="mt-2 text-3xl font-bold">Customers & contacts</h1><p class="mt-2 text-sm text-slate-600">Customer records shared across residential and commercial work.</p></header>
 {#if data.loadError}<p role="alert" class="rounded-lg bg-red-50 p-4 text-red-800">{data.loadError} <a class="underline" href="/carlzipf/admin/customers">Retry</a></p>{/if}
 {#if form?.error}<p role="alert" class="rounded-lg bg-red-50 p-4 text-red-800">{form.error}</p>{/if}
 {#if selected && page.url.searchParams.get('saved') === '1'}<p role="status" class="rounded-lg bg-emerald-50 p-4 text-emerald-800">Customer saved.</p>{/if}
 <div class="flex items-center gap-3"><label class="grow"><span class="sr-only">Search customers</span><input class="w-full rounded-lg border bg-white p-3" placeholder="Search name, company, phone, or address" bind:value={query} /></label><a href="/carlzipf/admin/customers" class="rounded-lg border bg-white p-3 text-sm font-semibold">New customer</a></div>
 <div class="grid gap-5 lg:grid-cols-[.9fr_1.1fr]">
  <section aria-label="Customers" class="h-fit overflow-hidden rounded-xl border bg-white">
   {#each filtered as customer}<a href={`?customer=${customer.id}`} class="block border-b p-5 hover:bg-slate-50" class:bg-slate-100={selected?.id === customer.id} aria-current={selected?.id === customer.id ? 'true' : undefined}><h2 class="font-semibold">{customerName(customer)}</h2>{#if customer.companyName}<p class="mt-1 text-sm">{customer.companyName}</p>{/if}<p class="mt-2 break-all text-sm text-slate-500">{customer.email || customer.phone || 'No contact details supplied'}</p><p class="mt-1 text-sm text-slate-500">{[customer.address, customer.city, customer.state].filter(Boolean).join(', ')}</p></a>
   {:else}{#if !data.loadError}<p class="p-8 text-center text-sm text-slate-500">{data.customers.length ? 'No customers match your search.' : 'No customer records yet. Add a customer to get started.'}</p>{/if}{/each}
  </section>
  {#if !data.loadError}<section class="rounded-xl border bg-white p-6">
   <h2 class="text-xl font-bold">{editing ? 'Edit customer' : 'New customer'}</h2>
   {#if data.draft && !editing}<p class="mt-3 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">Details copied from a request for your review. Check for an existing customer before saving. This creates a customer record; it does not link the request or create a job.</p>{/if}
   {#if page.url.searchParams.has('customer') && !selected}<p role="alert" class="mt-3 text-sm text-red-800">That customer is not available in this workspace.</p>{/if}
   {#key selected?.id ?? page.url.searchParams.get('fromRequest') ?? 'new'}<form method="POST" action={`?/save${selected ? `&customer=${selected.id}` : ''}`} class="mt-5 grid gap-4 sm:grid-cols-2" use:enhance={() => { saving = true; return async ({ update }) => { await update({ reset: false }); saving = false; }; }}>
    <input type="hidden" name="id" value={selected?.id ?? ''} /><input type="hidden" name="dateUpdated" value={selected?.dateUpdated ?? ''} />
    {#each fields as [key, label]}<label class="grid gap-2 text-sm font-semibold">{label}<input name={key} type={key === 'email' ? 'email' : key === 'phone' ? 'tel' : 'text'} maxlength="300" value={values[key] ?? ''} class="min-w-0 rounded border p-3 font-normal" /></label>{/each}
    <label class="grid gap-2 text-sm font-semibold sm:col-span-2">Notes<textarea name="notes" maxlength="4000" rows="4" class="rounded border p-3 font-normal">{values.notes ?? ''}</textarea></label>
    <button type="submit" disabled={saving} class="min-h-11 rounded-lg bg-[var(--accent-text)] px-5 font-semibold text-white disabled:opacity-50 sm:col-span-2">{saving ? 'Saving…' : editing ? 'Save customer' : 'Create customer'}</button>
   </form>{/key}
  </section>{/if}
 </div>
</div>
