<script lang="ts">
 import { untrack, type Snippet } from 'svelte';
 import PersonAvatar from '$lib/components/people/PersonAvatar.svelte';
 import { page } from '$app/state';
 import { enhance } from '$app/forms';
 import type { Person, EmployeeAccessConfiguration } from '$lib/server/people';
 let { people, customerLinks = [], form, canWrite = false, canDelete = false, canManagePortal = false, personDetails, initialFilter = 'all', employeeAccess = { roles: [], teams: [] } }: { people: Person[]; employeeAccess?: EmployeeAccessConfiguration; personDetails?: Snippet<[Person]>; initialFilter?: string; customerLinks?: {id:string;name:string;companyName?:string;customerType?:string}[]; form?: {message?: string; error?: string; inviteUrl?: string; savedId?: string} | null; canWrite?: boolean; canDelete?: boolean; canManagePortal?: boolean } = $props();
 let filter = $state(untrack(() => initialFilter)); let query = $state(''); let selectedId = $state<string | null>(null);
 let confirmDelete = $state(false); let creating = $state(false); let pending = $state(false); let employeeRoleId = $state('');
 let lastRequestedId = $state<string|null>(null);
 $effect(()=>{const id=page.url.searchParams.get('person');if(id && id!==lastRequestedId && people.some(p=>p.id===id)){lastRequestedId=id;open(people.find(p=>p.id===id));}});
 const selected = $derived(people.find(p=>p.id===selectedId));
 $effect(() => { if (form?.savedId && people.some(p=>p.id===form.savedId)) {selectedId=form.savedId;creating=false;} });
 const visible = $derived(people.filter(p => p.profileTypes.includes('employee') && (filter === 'archived' ? !p.isActive : p.isActive) && `${p.firstName} ${p.lastName} ${p.contactEmail ?? ''} ${p.contactPhone ?? ''}`.toLowerCase().includes(query.toLowerCase())));
 function open(p?: Person) { confirmDelete=false; selectedId=p?.id ?? null; creating=!p; employeeRoleId=p?.employeeRoleId ?? (p ? '' : employeeAccess.roles[0]?.id ?? ''); }
 const submit = () => { pending=true; return async ({update}: {update:(options?: {reset?: boolean})=>Promise<void>})=>{try {await update({reset:false});} finally {pending=false;}}; };
</script>
<svelte:head><title>People & access</title></svelte:head>
<section class="people">
 <header><div><p class="eyebrow">Company directory</p><h1>People & access</h1><p>Manage employee details, roles, and app access.</p></div>
 {#if canWrite}<button class="btn-primary" onclick={()=>open()}>Add employee</button>{/if}</header>
 {#if form?.error}<p class="notice error" role="alert">{form.error}</p>{/if}
 {#if form?.message}<p class="notice" role="status">{form.message}</p>{/if}
 {#if form?.inviteUrl}<div class="notice">Share this activation link with the person: <a href={form.inviteUrl}>{form.inviteUrl}</a></div>{/if}
 <div class="workspace"><div class="directory-panel"><div class="filters"><label>Search<input type="search" bind:value={query} placeholder="Name, email, or phone" /></label><label>Status<select bind:value={filter}><option value="all">Active employees</option><option value="archived">Archived employees</option></select></label></div>
 <div class="directory">
 {#each visible as person (person.id)}<button class:selected={selectedId===person.id} class="person" aria-pressed={selectedId===person.id} onclick={()=>open(person)}>
  <div class="person-heading"><PersonAvatar name={`${person.firstName} ${person.lastName}`} identity={person.id}/><strong>{`${person.firstName} ${person.lastName}`.trim() || person.contactEmail || person.contactPhone || 'Unnamed user'}</strong></div>
  <span>{person.isOwner ? 'Owner' : employeeAccess.roles.find(role => role.id === person.employeeRoleId)?.name || person.role || 'No app access'}{person.team ? ` · ${person.team}` : ''}</span>
 </button>{:else}<p class="empty">No employees match this view.</p>{/each}
 </div></div><div class="editor">
 {#if creating || selected}
 {#key selected?.id ?? 'new'}
 <h2>{creating ? 'Add employee' : 'Employee details'}</h2>
 <form method="POST" action="?/savePerson" use:enhance={submit}>
 <input type="hidden" name="existingPermissions" value={JSON.stringify(selected?.modulePermissions ?? null)}/><input type="hidden" name="id" value={selected?.id ?? ''}/><input type="hidden" name="version" value={selected?.version ?? ''}/>
 <fieldset disabled={!canWrite || pending || selected?.isActive === false}>
 <div class="fields"><label>First name<input name="firstName" required maxlength="100" value={selected?.firstName ?? ''}/></label><label>Last name<input name="lastName" maxlength="100" value={selected?.lastName ?? ''}/></label></div>
 {#if creating}<label>Login email or phone<input name="loginIdentifier" required placeholder="person@example.com or +14195551234"/></label><p class="help">An invitation is required before this employee can sign in.</p>{/if}
 <div class="fields"><label>Contact email<input type="email" name="contactEmail" value={selected?.contactEmail ?? ''}/></label><label>Contact phone<input name="contactPhone" maxlength="200" value={selected?.contactPhone ?? ''}/></label></div>
 <p class="help">Contact details are company records. They do not change verified sign-in credentials.</p>
 <label>Job title<input name="title" maxlength="200" value={selected?.title ?? ''}/></label>
 <div class="fields">
 {#if selected?.isOwner || !canDelete}<input type="hidden" name="employeeRoleId" value={selected?.employeeRoleId ?? ''}/>{/if}
 <label>Role<select aria-label="Role" required={creating} name="employeeRoleId" bind:value={employeeRoleId} disabled={selected?.isOwner || !canDelete}>
 <option value="">{selected?.isOwner ? 'Owner — full access' : creating ? 'Select a role' : 'Existing access policy'}</option>
 {#each employeeAccess.roles as role}<option value={role.id}>{role.name}</option>{/each}
 </select></label>
 <label>Team<select aria-label="Team" name="team" value={selected?.team ?? ''}><option value="">No team</option>
 {#if selected?.team && !employeeAccess.teams.includes(selected.team)}<option value={selected.team}>{selected.team}</option>{/if}
 {#each employeeAccess.teams as team}<option value={team}>{team}</option>{/each}
 </select></label></div>
 {#if employeeRoleId}<p class="help">Module access is inherited from {employeeAccess.roles.find(role => role.id === employeeRoleId)?.name || 'the selected role'}.</p>{/if}
 {#if canManagePortal}<p class="help"><a href={`${page.url.pathname.replace(/\/users$/, '')}/settings/people`}>Configure roles, module access, and teams in Settings</a></p>{/if}
 <button class="btn-primary" type="submit">{pending ? 'Saving…':'Save employee'}</button></fieldset></form>
 {#if selected && personDetails}{@render personDetails(selected)}{/if}
 {#if selected && canWrite}
 {#if !selected.isActive}<form method="POST" action="?/restorePerson" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><input type="hidden" name="version" value={selected.version}/><button class="btn-secondary" disabled={pending}>Restore user</button></form>
 {:else}
 {#if !selected.membershipId}<form method="POST" action="?/invitePerson" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><h3>Invite to the app</h3><input type="hidden" name="role" value="staff"/><button class="btn-secondary" disabled={pending}>Create invitation</button></form>{/if}
 {#if canDelete}
 <section class="removal"><h3>Remove company access</h3><p class="help">Deletes this user from this company, including owner access. Business history is retained.</p>
 {#if confirmDelete}<form method="POST" action="?/deletePerson" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><input type="hidden" name="version" value={selected.version}/><p>Delete {`${selected.firstName} ${selected.lastName}`.trim() || 'this user'} from this company?</p><div class="delete-actions"><button class="danger btn-danger" disabled={pending}>Confirm delete</button><button type="button" class="secondary btn-secondary" onclick={()=>confirmDelete=false}>Cancel</button></div></form>
 {:else}<button class="danger btn-danger" onclick={()=>confirmDelete=true}>Delete user</button>{/if}</section>
 {:else if !selected.isOwner}<form method="POST" action="?/archivePerson" use:enhance={submit}><input type="hidden" name="id" value={selected.id}/><input type="hidden" name="version" value={selected.version}/><p class="help">Archiving removes this company's access and keeps the record for recovery.</p><button class="danger btn-danger" disabled={pending}>Archive user</button></form>{/if}
 {/if}{/if}
 {/key}
 {:else}<p class="empty">Select an employee to view their details and access.</p>{/if}
 </div></div>
</section>
<style>
 .person-heading{display:flex;align-items:center;gap:.75rem}
 .people{max-width:1200px;margin:auto;color:var(--text-strong)}header,.filters{display:flex;gap:1.5rem;justify-content:space-between;align-items:center;margin-bottom:1.5rem}h1{font-size:1.8rem;font-weight:750}h2{font-size:1.3rem;font-weight:700;margin-bottom:1rem}h3{font-weight:700;margin:1.5rem 0 .75rem}.eyebrow{text-transform:uppercase;font-size:.7rem;letter-spacing:.15em}p,.help{color:var(--text-muted)}.help{font-size:.8rem;margin:.5rem 0 1rem}.filters{display:flex;flex-direction:column;align-items:stretch;gap:0;margin-bottom:.5rem}.directory-panel{min-width:0}.filters>label:first-child{max-width:none}.workspace{display:grid;align-items:start;grid-template-columns:minmax(220px,1fr) minmax(0,2fr);gap:1.5rem}.directory{overflow:hidden;align-self:start}.directory,.editor{background:var(--surface);border:1px solid var(--border);border-radius:12px}.editor{padding:1.5rem;min-width:0}.removal{border-top:1px solid var(--border);margin-top:1.5rem}.delete-actions{display:flex;gap:.75rem;flex-wrap:wrap}.secondary{background:var(--surface);color:var(--text-base)}.person{display:flex;flex-direction:column;gap:.3rem;width:100%;text-align:left;border:0;border-bottom:1px solid var(--border);border-radius:0;background:var(--surface);color:inherit;padding:1rem}.person span{font-size:.8rem;color:var(--text-muted)}.person.selected span{color:var(--text-base)}.person.selected{background:var(--teal-soft)}.person:focus-visible{outline:2px solid var(--focus-ring);outline-offset:-3px}.fields{display:grid;grid-template-columns:1fr 1fr;gap:1rem}label{display:flex;flex-direction:column;gap:.4rem;font-size:.85rem;margin-bottom:.8rem}input,select{border:1px solid var(--input-border);border-radius:6px;padding:.65rem;min-width:0;background:var(--surface);color:var(--text-strong)}button{border:1px solid var(--input-border);border-radius:6px;padding:.65rem 1rem;background:var(--cta);color:var(--cta-text);font-weight:600}button:disabled,fieldset:disabled{opacity:.6}fieldset{border:0;padding:0;min-width:0}.notice{background:var(--positive-soft);padding:1rem;margin-bottom:1rem;border-radius:8px;overflow-wrap:anywhere}.error{background:var(--critical-soft);color:var(--critical-text)}.empty{padding:1.5rem}.danger{background:var(--surface);color:var(--critical-text);margin-top:.5rem}a{text-decoration:underline}@media(max-width:760px){.workspace,.fields{grid-template-columns:1fr}.filters,header{align-items:stretch;flex-direction:column}.directory{max-height:280px;overflow:auto}}
</style>
