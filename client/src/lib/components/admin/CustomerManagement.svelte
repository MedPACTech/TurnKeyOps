<script lang="ts">
 import { House, Building2 } from 'lucide-svelte';
 import ContactPortalAccess from '$lib/components/portal/ContactPortalAccess.svelte';
 import { enhance } from '$app/forms';
 import { page } from '$app/state';
 import { customerName } from '$lib/customer-records';
 import type { CustomerRecord } from '$lib/customer-records';
 import type { Contact } from '$lib/server/contacts';
 let {data,form}: {data:{customers:CustomerRecord[];draft:Partial<CustomerRecord>|null;loadError:string;canManagePortal:boolean;portalContacts:Contact[];portalContactsError:string};form?:{error?:string}|null}=$props();
 const base=$derived(page.url.pathname);
 const tenantBase=$derived(`/${page.url.pathname.split('/')[1]}/admin`);
 let typeFilter=$state('all'), customerType=$state('');
 $effect(()=>{customerType=selected?.customerType??'';});
 let portalPerson = $state('');
 const linkedContacts = $derived(data.portalContacts.filter(p=>p.customerId===selected?.id && p.profileTypes.includes('customer')));
 const selectedPortalContact = $derived(linkedContacts.find(p=>p.id===portalPerson) ?? linkedContacts[0]);
 let query = $state(''); let saving = $state(false);
 const filtered = $derived(data.customers.filter(customer => (typeFilter==='all'||(customer.customerType??'unclassified')===typeFilter) && [customerName(customer), customer.companyName, customer.email, customer.phone, customer.address].join(' ').toLowerCase().includes(query.toLowerCase().trim())));
 const selected = $derived(data.customers.find(customer => customer.id === page.url.searchParams.get('customer')));
 const editing = $derived(Boolean(selected));
 const values = $derived(selected ?? data.draft ?? {});
 const fields = [ ['firstName', 'First name / contact name'], ['lastName', 'Last name'], ['companyName', 'Company'], ['email', 'Email'], ['phone', 'Phone'], ['address', 'Street address'], ['city', 'City'], ['state', 'State'], ['zip', 'Postal code'] ] as const;
</script>
<svelte:head><title>Customers</title></svelte:head>
<div class="mx-auto max-w-7xl space-y-5 pb-10">
 <header><p class="text-sm font-semibold text-[var(--accent-text)]">Relationships</p><h1 class="mt-2 text-3xl font-bold">Customers</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Customer records shared across residential and commercial work.</p></header>
 {#if data.loadError}<p role="alert" class="rounded-lg bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]">{data.loadError} <a class="underline" href={base}>Retry</a></p>{/if}
 {#if form?.error}<p role="alert" class="rounded-lg bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]">{form.error}</p>{/if}
 {#if selected && page.url.searchParams.get('saved') === '1'}<p role="status" class="rounded-lg bg-[var(--positive-soft)] p-4 text-[var(--positive-text)]">Customer saved.</p>{/if}
 <div class="flex flex-wrap items-end gap-3">
  <div class="min-w-0">
   <p class="mb-2 text-sm font-semibold" id="customer-type-filter">Customer type</p>
   <div role="group" aria-labelledby="customer-type-filter" class="flex flex-wrap gap-1 rounded-lg border bg-[var(--surface-subtle)] p-1">
    {#each [{value:'all',label:'All customers'},{value:'residential',label:'Residential'},{value:'commercial',label:'Commercial'}] as filter}
     <button type="button" aria-pressed={typeFilter===filter.value} onclick={()=>typeFilter=filter.value} class="flex min-h-11 items-center justify-center rounded-md border border-transparent px-3 text-sm font-semibold aria-pressed:border-[var(--accent-text)] aria-pressed:bg-[var(--surface)] aria-pressed:text-[var(--accent-text)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--accent-text)]">{filter.label}</button>
    {/each}
   </div>
  </div>
  <label class="min-w-0 grow"><span class="sr-only">Search customers</span><input class="w-full rounded-lg border bg-[var(--surface)] p-3" placeholder="Search name, company, phone, or address" bind:value={query} /></label>
  <a href={base} class="rounded-lg border bg-[var(--surface)] p-3 text-sm font-semibold">New customer</a>
 </div>
 <div class="grid gap-5 lg:grid-cols-[.9fr_1.1fr]">
  <section aria-label="Customers" class="h-fit overflow-hidden rounded-xl border bg-[var(--surface)]">
   {#each filtered as customer}<a href={`?customer=${customer.id}`} class="block border-b p-5 hover:bg-[var(--surface-subtle)]" class:admin-selected={selected?.id === customer.id} aria-current={selected?.id === customer.id ? 'true' : undefined}><div class="flex items-start justify-between gap-3"><h2 class="min-w-0 break-words font-semibold">{customerName(customer)}</h2>{#if customer.customerType==='residential'}<span class="shrink-0 text-[var(--accent-text)]" role="img" aria-label="Residential customer" title="Residential"><House size={24} aria-hidden="true"/></span>{:else if customer.customerType==='commercial'}<span class="shrink-0 text-[var(--accent-text)]" role="img" aria-label="Commercial customer" title="Commercial"><Building2 size={24} aria-hidden="true"/></span>{/if}</div>{#if customer.customerType}<p class="mt-1 text-xs font-semibold">{customer.customerType==='residential'?'Residential':'Commercial'}</p>{/if}{#if customer.companyName}<p class="mt-1 text-sm">{customer.companyName}</p>{/if}<p class="mt-2 break-all text-sm text-[var(--text-muted)]">{customer.email || customer.phone || 'No contact details supplied'}</p><p class="mt-1 text-sm text-[var(--text-muted)]">{[customer.address, customer.city, customer.state].filter(Boolean).join(', ')}</p></a>
   {:else}{#if !data.loadError}<p class="p-8 text-center text-sm text-[var(--text-muted)]">{data.customers.length ? 'No customers match your search.' : 'No customer records yet. Add a customer to get started.'}</p>{/if}{/each}
  </section>
  {#if !data.loadError}<section class="rounded-xl border bg-[var(--surface)] p-6">
   <h2 class="text-xl font-bold">{editing ? 'Edit customer' : 'New customer'}</h2>
   {#if data.draft && !editing}<p class="mt-3 rounded-lg bg-[var(--warning-soft)] p-3 text-sm text-[var(--warning-text)]">Details copied from a request for your review. Check for an existing customer before saving. This creates a customer record; it does not link the request or create a job.</p>{/if}
   {#if page.url.searchParams.has('customer') && !selected}<p role="alert" class="mt-3 text-sm text-[var(--critical-text)]">That customer is not available in this workspace.</p>{/if}
   {#key selected?.id ?? page.url.searchParams.get('fromRequest') ?? 'new'}<form method="POST" action={`?/save${selected ? `&customer=${selected.id}` : ''}`} class="mt-5 grid gap-4 sm:grid-cols-2" use:enhance={() => { saving = true; return async ({ update }) => { await update({ reset: false }); saving = false; }; }}>
    <input type="hidden" name="id" value={selected?.id ?? ''} /><input type="hidden" name="dateUpdated" value={selected?.dateUpdated ?? ''} />
    <fieldset class="min-w-0 sm:col-span-2">
     <legend class="mb-2 text-sm font-semibold">Customer type</legend>
     <div class="grid grid-cols-2 gap-1 rounded-lg border bg-[var(--surface-subtle)] p-1">
      {#each ['residential', 'commercial'] as type}
       <label class="relative cursor-pointer">
        <input type="radio" name="customerType" value={type} bind:group={customerType} required class="peer absolute inset-0 z-10 m-0 h-full w-full cursor-pointer opacity-0" />
        <span class="flex min-h-11 items-center justify-center rounded-md border border-transparent px-3 text-sm font-semibold peer-checked:border-[var(--accent-text)] peer-checked:bg-[var(--surface)] peer-checked:text-[var(--accent-text)] peer-focus-visible:outline peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-[var(--accent-text)]">{type === 'residential' ? 'Residential' : 'Commercial'}</span>
       </label>
      {/each}
     </div>
    </fieldset>
    {#if customerType==='commercial'}<label class="grid gap-2 text-sm font-semibold sm:col-span-2">Company name<input name="companyName" required maxlength="300" value={values.companyName??''} class="rounded border p-3"/></label>{/if}
    {#each fields.filter(([key])=>key!=='companyName') as [key, label]}<label class="grid gap-2 text-sm font-semibold">{key==='firstName'?(customerType==='commercial'?'Primary contact first name (optional)':'First name / household name'):label}<input name={key} type={key === 'email' ? 'email' : key === 'phone' ? 'tel' : 'text'} maxlength="300" value={values[key] ?? ''} class="min-w-0 rounded border p-3 font-normal" /></label>{/each}
    {#if customerType!=='commercial'}<input type="hidden" name="companyName" value={values.companyName??''}/>{/if}
    <p class="text-sm text-[var(--text-muted)] sm:col-span-2">{customerType==='commercial'?'Company address; individual contacts and site access are managed below.':'Primary address; this customer can still have multiple properties and contacts.'} Customer type does not change job type.</p>
    <label class="grid gap-2 text-sm font-semibold sm:col-span-2">Notes<textarea name="notes" maxlength="4000" rows="4" class="rounded border p-3 font-normal">{values.notes ?? ''}</textarea></label>
    <button type="submit" disabled={saving} class="admin-primary min-h-11 rounded-lg bg-[var(--cta)] px-5 font-semibold text-white disabled:opacity-50 sm:col-span-2">{saving ? 'Saving…' : editing ? 'Save customer' : 'Create customer'}</button>
   </form>{/key}
   {#if data.canManagePortal}
    <section class="mt-6 border-t border-[var(--border)] pt-5" aria-label="Customer portal contacts">
     <h2 class="text-xl font-bold">Customer Portal</h2>
     {#if !selected}<p class="mt-2 text-sm text-[var(--text-muted)]">Save the customer, then add a contact to manage their portal access.</p>
     {:else if data.portalContactsError}<p class="mt-2" role="alert">{data.portalContactsError}</p>
     {:else}
      <p class="mt-2 text-sm text-[var(--text-muted)]">Access is granted to each person linked to this customer.</p>
      {#if linkedContacts.length}
       <label class="mt-4 grid gap-2 text-sm">Customer contact<select class="min-h-11 rounded border bg-[var(--surface)] p-2" value={selectedPortalContact?.id} onchange={event=>portalPerson=event.currentTarget.value}>{#each linkedContacts as person}<option value={person.id}>{`${person.firstName} ${person.lastName}`.trim() || person.contactEmail || person.contactPhone}</option>{/each}</select></label>
       {#if selectedPortalContact}{#key selectedPortalContact.id}<ContactPortalAccess personId={selectedPortalContact.id} customerId={selected.id}/>{/key}{/if}
      {:else if selected.customerType==='residential'}
       <form method="POST" action="?/setupPrimaryContact" class="mt-4 grid gap-3" use:enhance>
        <input type="hidden" name="customerId" value={selected.id}/><input type="hidden" name="dateUpdated" value={selected.dateUpdated??''}/>
        <p class="text-sm">Confirm who will sign in. Next, choose their portal access scope and expiration.</p>
        <label class="grid gap-1 text-sm">Portal contact first name<input class="rounded border p-3" name="firstName" required value={selected.firstName}/></label>
        <label class="grid gap-1 text-sm">Portal contact last name<input class="rounded border p-3" name="lastName" value={selected.lastName}/></label>
        <label class="grid gap-1 text-sm">Portal login email<input class="rounded border p-3" type="email" name="email" value={selected.email??''}/></label>
        <label class="grid gap-1 text-sm">Portal login phone<input class="rounded border p-3" type="tel" name="phone" value={selected.phone??''}/></label>
        <p class="text-sm text-[var(--text-muted)]">Provide an email or phone that can receive a verification code. No employee access is granted.</p>
        <button class="btn-primary" type="submit">Set up portal access</button>
       </form>
      {:else}<p class="mt-3 text-sm">No customer contacts are linked yet. Add a contact with an email or phone to provision portal access.</p>{/if}
      <a class="mt-3 inline-flex min-h-11 items-center underline" href={`${tenantBase}/contact?new=1&customer=${selected.id}`}>{selected.customerType==='residential'?'Add another contact':'Add customer contact'}</a>
     {/if}
    </section>
   {/if}
  </section>{/if}
 </div>
</div>
