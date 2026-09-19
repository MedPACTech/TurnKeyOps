<script lang="ts">
 import { enhance } from '$app/forms';
 import type { Person } from '$lib/server/people';
 let { people, customerLinks = [], form, canWrite = false }: { people: Person[]; customerLinks?: {id:string;name:string;companyName?:string}[]; form?: {message?: string; error?: string; inviteUrl?: string; savedId?: string} | null; canWrite?: boolean } = $props();
 const modules = ['dashboard','bob','calendar','jobs','requests','estimates','invoices','contacts','users','settings','billing'];
 let filter = $state('all'); let query = $state(''); let selectedId = $state<string | null>(null);
 let creating = $state(false); let pending = $state(false); let permissionMode = $state('default');
 const selected = $derived(people.find(p=>p.id===selectedId));
 $effect(() => { if (form?.savedId && people.some(p=>p.id===form.savedId)) {selectedId=form.savedId;creating=false;} });
 const visible = $derived(people.filter(p => (filter === 'archived' ? !p.isActive : p.isActive && (filter==='all' || p.profileTypes.includes(filter))) && `${p.firstName} ${p.lastName} ${p.contactEmail ?? ''} ${p.contactPhone ?? ''}`.toLowerCase().includes(query.toLowerCase())));
 function open(p?: Person) { selectedId=p?.id ?? null; creating=!p; permissionMode=p?.modulePermissions===null || !p ? 'default':'custom'; }
 const submit = () => { pending=true; return async ({update}: {update:(options?: {reset?: boolean})=>Promise<void>})=>{try {await update({reset:false});} finally {pending=false;}}; };
</script>
<svelte:head><title>People & access</title></svelte:head>
<section class="people">
 <header><div><p class="eyebrow">Company directory</p><h1>People & access</h1><p>One person, linked business profiles, and a separate choice of app access.</p></div>
 {#if canWrite}<button onclick={()=>open()}>Add user</button>{/if}</header>
 {#if form?.error}<p class="notice error" role="alert">{form.error}</p>{/if}
 {#if form?.message}<p class="notice" role="status">{form.message}</p>{/if}
 {#if form?.inviteUrl}<div class="notice">Share this activation link with the person: <a href={form.inviteUrl}>{form.inviteUrl}</a></div>{/if}
 <div class="filters"><label>Search<input type="search" bind:value={query} placeholder="Name, email, or phone" /></label><label>Profile<select bind:value={filter}><option value="all">All active users</option><option value="employee">Employees</option><option value="customer">Customers</option><option value="vendor">Vendors</option><option value="archived">Archived</option></select></label></div>
 <div class="workspace"><div class="directory">
 {#each visible as person (person.id)}<button class:selected={selectedId===person.id} class="person" onclick={()=>open(person)}>
  <strong>{`${person.firstName} ${person.lastName}`.trim() || person.contactEmail || person.contactPhone || 'Unnamed user'}</strong>
  <span>{person.profileTypes.join(' · ') || 'No business profile yet'}</span><span>{person.isOwner ? 'Owner' : person.role || 'No app access'}</span>
 </button>{:else}<p class="empty">No users match this view.</p>{/each}
 </div><div class="editor">
 {#if creating || selected}
 {#key selected?.id ?? 'new'}
 <h2>{creating ? 'Add user' : 'User details'}</h2>
 <form method="POST" action="?/savePerson" use:enhance={submit}>
 <input type="hidden" name="id" value={selected?.id ?? ''}/><input type="hidden" name="version" value={selected?.version ?? ''}/>
 <fieldset disabled={!canWrite || pending || selected?.isActive === false}>
 <div class="fields"><label>First name<input name="firstName" required maxlength="100" value={selected?.firstName ?? ''}/></label><label>Last name<input name="lastName" maxlength="100" value={selected?.lastName ?? ''}/></label></div>
 {#if creating}<label>Login email or phone<input name="loginIdentifier" required placeholder="person@example.com or +14195551234"/></label><p class="help">Creates or reuses one identity. An invitation is required before company access is granted.</p>{/if}
 <div class="fields"><label>Contact email<input type="email" name="contactEmail" value={selected?.contactEmail ?? ''}/></label><label>Contact phone<input name="contactPhone" maxlength="200" value={selected?.contactPhone ?? ''}/></label></div>
 <p class="help">Contact details are company records. They do not change verified sign-in credentials.</p>
 <fieldset class="profiles"><legend>Business profiles</legend>{#each ['employee','customer','vendor'] as type}<label><input type="checkbox" name="profileTypes" value={type} checked={selected?.profileTypes.includes(type) ?? false}/>{type}</label>{/each}</fieldset>
 <div class="fields"><label>Company<input name="companyName" maxlength="200" value={selected?.companyName ?? ''}/></label><label>Job title<input name="title" maxlength="200" value={selected?.title ?? ''}/></label><label>Team<input name="team" maxlength="200" value={selected?.team ?? ''}/></label></div>
 <label>Linked customer record<select name="customerId" value={selected?.customerId ?? ''}><option value="">No linked customer</option>{#each customerLinks as customer}<option value={customer.id}>{customer.name || customer.companyName || 'Unnamed customer'}</option>{/each}</select></label>
 <h3>Module permissions</h3>
 {#if selected?.isOwner}<p>Owners retain full company access.</p><input type="hidden" name="permissionMode" value="default"/>
 {:else}<label>Permission policy<select name="permissionMode" bind:value={permissionMode}><option value="default">Use role defaults</option><option value="custom">Custom module permissions</option></select></label>
 {#if permissionMode==='custom'}<div class="permissions">{#each modules as module}<label>{module}<select name={`module:${module}`} value={selected?.modulePermissions?.includes(`${module}.write`) ? 'write' : selected?.modulePermissions?.includes(`${module}.read`) ? 'read':'none'}><option value="none">No access</option><option value="read">View</option><option value="write">View and manage</option></select></label>{/each}</div><p class="help">Permissions stay within the access role. Dashboard requires visibility into all operational modules; Bob requires management access to them.</p>{/if}{/if}
 <button type="submit">{pending ? 'Saving…':'Save user'}</button></fieldset></form>
 {#if selected && canWrite}
 {#if !selected.isActive}<form method="POST" action="?/restorePerson" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><input type="hidden" name="version" value={selected.version}/><button disabled={pending}>Restore user</button></form>
 {:else}
 {#if selected.membershipId && !selected.isOwner}<form method="POST" action="?/updatePersonRole" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><h3>Access role</h3><label>Role<select name="role" value={selected.role}><option value="contact">Contact</option>{#if selected.profileTypes.includes('employee')}<option value="staff">Staff</option><option value="member">Member</option><option value="admin">Admin</option><option value="owner">Owner — full company access</option>{/if}</select></label><button disabled={pending}>Save access role</button><p class="help">Only company owners can change access roles.</p></form>{/if}
 {#if !selected.membershipId}<form method="POST" action="?/invitePerson" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><h3>Invite to the app</h3><label>Access role<select name="role"><option value="contact">Contact</option>{#if selected.profileTypes.includes('employee')}<option value="staff">Staff</option><option value="admin">Admin</option>{/if}</select></label><button disabled={pending}>Create invitation</button></form>{/if}
 {#if !selected.isOwner}<form method="POST" action="?/archivePerson" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><input type="hidden" name="version" value={selected.version}/><p class="help">Archiving removes this company's access and keeps the record for recovery.</p><button class="danger" disabled={pending}>Archive user</button></form>{/if}
 {/if}{/if}
 {/key}
 {:else}<p class="empty">Select a person to edit their profiles and access.</p>{/if}
 </div></div>
</section>
<style>
 .people{max-width:1200px;margin:auto;color:var(--text-strong,#17212b)}header,.filters{display:flex;gap:1.5rem;justify-content:space-between;align-items:center;margin-bottom:1.5rem}h1{font-size:1.8rem;font-weight:750}h2{font-size:1.3rem;font-weight:700;margin-bottom:1rem}h3{font-weight:700;margin:1.5rem 0 .75rem}.eyebrow{text-transform:uppercase;font-size:.7rem;letter-spacing:.15em}p,.help{color:var(--text-muted,#667085)}.help{font-size:.8rem;margin:.5rem 0 1rem}.filters>label:first-child{flex:1;max-width:420px}.workspace{display:grid;grid-template-columns:minmax(220px,1fr) minmax(0,2fr);gap:1.5rem}.directory,.editor{background:white;border:1px solid #e4e7ec;border-radius:12px}.editor{padding:1.5rem}.person{display:flex;flex-direction:column;gap:.3rem;width:100%;text-align:left;border:0;border-bottom:1px solid #eee;border-radius:0;background:white;color:inherit;padding:1rem}.person span{font-size:.8rem;color:#667085}.person.selected{background:#fff3e9}.fields,.permissions{display:grid;grid-template-columns:1fr 1fr;gap:1rem}label{display:flex;flex-direction:column;gap:.4rem;font-size:.85rem;margin-bottom:.8rem}input,select{border:1px solid #ccd2da;border-radius:6px;padding:.65rem;min-width:0;background:white;color:#17212b}button{border:1px solid #d1d5db;border-radius:6px;padding:.65rem 1rem;background:var(--accent-text,#c2410c);color:white;font-weight:600}button:disabled,fieldset:disabled{opacity:.6}fieldset{border:0;padding:0;min-width:0}.profiles{display:flex;gap:1rem;margin:1rem 0}.profiles label{flex-direction:row;align-items:center;text-transform:capitalize}.permissions label{text-transform:capitalize}.notice{background:#effaf4;padding:1rem;margin-bottom:1rem;border-radius:8px;overflow-wrap:anywhere}.error{background:#fff0f0;color:#9f1239}.empty{padding:1.5rem}.danger{background:white;color:#be123c;margin-top:.5rem}a{text-decoration:underline}@media(max-width:760px){.workspace,.fields,.permissions{grid-template-columns:1fr}.filters,header{align-items:stretch;flex-direction:column}.directory{max-height:280px;overflow:auto}}
</style>
