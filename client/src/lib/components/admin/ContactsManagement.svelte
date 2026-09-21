<script lang="ts">
 import { enhance } from '$app/forms';
 import { page } from '$app/state';
 import type { Contact, ContactWork } from '$lib/server/contacts';
 let {contacts,customerLinks,selectedId,work,contactSaved=false,canWrite=false,canManagePeople=false,form}: {
  contacts:Contact[];customerLinks:{id:string;name:string;companyName?:string}[];selectedId:string|null;work:ContactWork|null;
  contactSaved?:boolean;canWrite?:boolean;canManagePeople?:boolean;form?:{error?:string}|null
 }=$props();
 let query=$state('');let filter=$state('all');let pending=$state(false);
 const creating=$derived(page.url.searchParams.has('new'));
 const selected=$derived(contacts.find(c=>c.id===selectedId));
 const base=$derived(page.url.pathname.replace(/\/(contact|customers)$/,''));
 const visible=$derived(contacts.filter(c=>(filter==='all'||c.profileTypes.includes(filter))&&`${c.firstName} ${c.lastName} ${c.companyName??''} ${c.contactEmail??''} ${c.contactPhone??''}`.toLowerCase().includes(query.toLowerCase())));
 const name=(c:Contact)=>`${c.firstName} ${c.lastName}`.trim();
 const submit=()=>{pending=true;return async({update}:{update:()=>Promise<void>})=>{try{await update();}finally{pending=false;}};};
</script>
<svelte:head><title>Contacts</title></svelte:head>
<section class="contacts">
 <header><div><h1>Contacts</h1><p>Customer and vendor relationships, addresses, notes, and linked work.</p></div>{#if canWrite}<a class="primary" href="?new=1">Add contact</a>{/if}</header>
 {#if form?.error}<p class="notice error" role="alert">{form.error}</p>{:else if contactSaved}<p class="notice" role="status">Contact saved.</p>{/if}
 <div class="toolbar"><label>Search contacts<input type="search" bind:value={query} placeholder="Name, company, email, or phone"/></label><label>Relationship<select bind:value={filter}><option value="all">All contacts</option><option value="customer">Customers</option><option value="vendor">Vendors</option></select></label></div>
 <div class="workspace"><nav class="directory" aria-label="Contact directory">
 {#each visible as contact(contact.id)}<a class:selected={contact.id===selectedId&&!creating} href={`?person=${contact.id}`}><strong>{name(contact)}</strong><span>{contact.companyName||contact.contactEmail||contact.contactPhone||'No contact details'}</span><span class="types">{contact.profileTypes.join(' · ')}</span></a>{:else}<p class="empty">No contacts match this view.</p>{/each}
 </nav><section class="editor">
 {#if creating||selected}{#key creating?'new':selected?.id}
 <h2>{creating?'Add contact':name(selected!)}</h2>
 <form method="POST" action="?/saveContact" use:enhance={submit}>
 <input type="hidden" name="id" value={creating?'':selected?.id??''}/><input type="hidden" name="version" value={creating?'':selected?.version??''}/>
 <fieldset disabled={!canWrite||pending}>
 <div class="fields"><label>First name<input name="firstName" required maxlength="100" value={creating?'':selected?.firstName??''}/></label><label>Last name<input name="lastName" maxlength="100" value={creating?'':selected?.lastName??''}/></label></div>
 <fieldset class="relationships"><legend>Relationship</legend>{#each ['customer','vendor'] as type}<label><input type="checkbox" name="profileTypes" value={type} checked={creating?type==='customer':selected?.profileTypes.includes(type)}/>{type==='customer'?'Customer':'Vendor'}</label>{/each}</fieldset>
 <label>Company<input name="companyName" maxlength="200" value={creating?'':selected?.companyName??''}/></label>
 <div class="fields"><label>Email<input name="contactEmail" type="email" value={creating?'':selected?.contactEmail??''}/></label><label>Phone<input name="contactPhone" type="tel" maxlength="200" value={creating?'':selected?.contactPhone??''}/></label></div>
 {#if creating}<p class="help">Enter an email or international phone number to identify this person. Adding a contact does not grant app access.</p>{/if}
 <h3>Address</h3><label>Street address<input name="address" maxlength="200" value={creating?'':selected?.address??''}/></label>
 <div class="fields"><label>City<input name="city" maxlength="200" value={creating?'':selected?.city??''}/></label><label>State / region<input name="state" maxlength="200" value={creating?'':selected?.state??''}/></label><label>Postal code<input name="postalCode" maxlength="200" value={creating?'':selected?.postalCode??''}/></label></div>
 <label>Notes<textarea name="notes" rows="5" maxlength="4000" value={creating?'':selected?.notes??''}></textarea></label>
 <label>Linked customer record<select name="customerId" value={creating?'':selected?.customerId??''}><option value="">No linked customer</option>{#each customerLinks as customer}<option value={customer.id}>{customer.name||customer.companyName||'Unnamed customer'}</option>{/each}</select></label>
 <p class="help">Link an existing customer record to see their jobs and invoices here.</p>
 {#if canWrite}<button class="primary" disabled={pending}>{pending?'Saving…':'Save contact'}</button>{/if}
 </fieldset></form>
 {#if !creating&&selected}
 <section class="work"><h3>Linked work</h3>
 {#if !selected.customerId}<p>No customer record linked yet.</p>
 {:else if work}
 <h4>Jobs and properties</h4>{#if !work.canViewJobs}<p>Your role does not include job access.</p>{:else}<ul>{#each work.jobs as job}<li><a href={`${base}/jobs?job=${job.id}`}>{job.name||'Untitled job'}</a><span>{job.status}</span>{#if job.address}<p>{job.address}</p>{/if}</li>{:else}<li>No linked jobs yet.</li>{/each}</ul>{/if}
 <h4>Invoices</h4>{#if !work.canViewInvoices}<p>Your role does not include invoice access.</p>{:else}<ul>{#each work.invoices as invoice}<li>{invoice.number||'Unnumbered invoice'} <span>{invoice.status}</span></li>{:else}<li>No linked invoices yet.</li>{/each}</ul>{#if work.invoices.length}<a href={`${base}/invoices`}>Open invoices</a>{/if}{/if}
 {/if}
 </section>
 {#if canManagePeople}<a class="access-link" href={`${base}/users?person=${selected.id}`}>Manage this person’s app access</a>{/if}
 {/if}
 {/key}{:else}<div class="empty"><h2>Select a contact</h2><p>Choose a customer or vendor to view their details and linked work.</p></div>{/if}
 </section></div>
</section>
<style>
 .contacts{max-width:1200px;margin:auto;color:var(--text-strong,#17212b)}header,.toolbar{display:flex;gap:1.5rem;justify-content:space-between;align-items:center;margin-bottom:1.5rem}h1{font-size:1.8rem;font-weight:750}h2{font-size:1.3rem;font-weight:700;margin-bottom:1rem}h3{font-weight:700;margin:1.5rem 0 .75rem}h4{font-weight:600;margin:1rem 0 .5rem}p,span{color:var(--text-muted,#667085)}.toolbar>label:first-child{flex:1;max-width:460px}.workspace{display:grid;grid-template-columns:minmax(220px,1fr) minmax(0,2fr);gap:1.5rem;align-items:start}.directory,.editor{background:white;border:1px solid #e4e7ec;border-radius:12px}.directory{overflow:hidden}.directory a{display:flex;flex-direction:column;gap:.35rem;padding:1rem;border-bottom:1px solid #e4e7ec;text-decoration:none;overflow-wrap:anywhere}.directory a:last-child{border-bottom:0}.directory a.selected span{color:#475467}.directory a.selected{background:var(--accent-soft,#fff3e9)}.directory a:focus-visible{outline:2px solid var(--accent-text,#c2410c);outline-offset:-3px}.directory span{font-size:.85rem}.types{text-transform:capitalize}.editor{padding:1.5rem;min-width:0}.fields{display:grid;grid-template-columns:1fr 1fr;gap:1rem}label{display:flex;flex-direction:column;gap:.4rem;font-size:.85rem;margin-bottom:.8rem}input,select,textarea{border:1px solid #ccd2da;border-radius:6px;padding:.65rem;min-width:0;background:white;color:#17212b}fieldset{border:0;padding:0;min-width:0}.relationships{display:flex;flex-wrap:wrap;gap:1rem;margin:1rem 0}.relationships label{flex-direction:row;align-items:center}.primary{display:inline-block;padding:.65rem 1rem;background:var(--accent-text,#c2410c);color:white;border-radius:6px;text-decoration:none;font-weight:600;white-space:nowrap}.help{font-size:.8rem;margin:.5rem 0 1rem}.notice{background:#effaf4;padding:1rem;margin-bottom:1rem;border-radius:8px}.error{background:#fff0f0;color:#9f123c}.empty{padding:1.5rem}.work{border-top:1px solid #e4e7ec;margin-top:1.5rem}li{padding:.6rem 0;border-bottom:1px solid #eee}li span{margin-left:.5rem;font-size:.85rem}a{text-decoration:underline}.access-link{display:block;margin-top:1.5rem}button:disabled{opacity:.6}@media(max-width:760px){.workspace,.fields{grid-template-columns:1fr}.toolbar,header{align-items:stretch;flex-direction:column}.directory{max-height:280px;overflow:auto}}
</style>
