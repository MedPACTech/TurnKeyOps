<script lang="ts">
 import { untrack, type Snippet } from 'svelte';
 import { personRoleIds, personTeams } from '$lib/employee-roles';
 import AdminDrawer from './AdminDrawer.svelte';
 import PersonAvatar from '$lib/components/people/PersonAvatar.svelte';
 import { page } from '$app/state';
 import { enhance } from '$app/forms';
 import type { Person, EmployeeAccessConfiguration } from '$lib/server/people';
 let { people, customerLinks = [], form, canWrite = false, canDelete = false, canManagePortal = false, personDetails, initialFilter = 'all', employeeAccess = { roles: [], teams: [] } }: { people: Person[]; employeeAccess?: EmployeeAccessConfiguration; personDetails?: Snippet<[Person]>; initialFilter?: string; customerLinks?: {id:string;name:string;companyName?:string;customerType?:string}[]; form?: {message?: string; error?: string; inviteUrl?: string; savedId?: string} | null; canWrite?: boolean; canDelete?: boolean; canManagePortal?: boolean } = $props();
 let filter = $state(untrack(() => initialFilter)); let query = $state(''); let selectedId = $state<string | null>(null);
 let confirmDelete = $state(false); let creating = $state(false); let pending = $state(false);
 let lastRequestedId = $state<string|null>(null);
 $effect(()=>{const id=page.url.searchParams.get('person');if(id && id!==lastRequestedId && people.some(p=>p.id===id)){lastRequestedId=id;open(people.find(p=>p.id===id));}});
 const selected = $derived(people.find(p=>p.id===selectedId));
 let assignedRoleIds = $state<string[]>([]);
 let assignedTeams = $state<string[]>([]);
 const rolePermissions = $derived(personRoleIds(selected).length ? [...new Set(employeeAccess.roles.filter(role=>personRoleIds(selected).includes(role.id)).flatMap(role=>role.modulePermissions))] : selected?.effectivePermissions ?? []);
 $effect(() => { if (form?.savedId && people.some(p=>p.id===form.savedId)) {selectedId=form.savedId;creating=false;} });
 const visible = $derived(people.filter(p => p.profileTypes.includes('employee') && (filter === 'archived' ? !p.isActive : p.isActive) && `${p.firstName} ${p.lastName} ${p.contactEmail ?? ''} ${p.contactPhone ?? ''}`.toLowerCase().includes(query.toLowerCase())));
 function open(p?: Person) { confirmDelete=false; selectedId=p?.id ?? null; creating=!p; assignedRoleIds=[...personRoleIds(p)]; assignedTeams=[...personTeams(p)]; }
 const submit = () => { pending=true; return async ({update}: {update:(options?: {reset?: boolean})=>Promise<void>})=>{try {await update({reset:false});} finally {pending=false;}}; };
</script>
<svelte:head><title>People & access</title></svelte:head>
<section class="people">
 <header><div><p class="eyebrow">Company directory</p><h1>People & access</h1><p>Manage employee details, roles, and app access.</p></div>
 {#if canWrite}<button class="btn-primary" onclick={()=>open()}>Add employee</button>{/if}</header>
 {#if !creating && !selected}
 {#if form?.error}<p class="notice error" role="alert">{form.error}</p>{/if}
 {#if form?.message}<p class="notice" role="status">{form.message}</p>{/if}
 {#if form?.inviteUrl}<div class="notice">Share this activation link with the person: <a href={form.inviteUrl}>{form.inviteUrl}</a></div>{/if}
 {/if}
 <div class="filters"><label>Search<input type="search" bind:value={query} placeholder="Name, email, or phone" /></label><label>Status<select bind:value={filter}><option value="all">Active employees</option><option value="archived">Archived employees</option></select></label></div>
 <div class="table-scroll"><table><caption class="sr-only">Employee directory</caption><thead><tr><th>Employee</th><th>Contact</th><th>Job title</th><th>Team</th><th>Role</th><th>App access</th></tr></thead><tbody>
 {#each visible as person (person.id)}<tr class:selected={selectedId===person.id}>
 <td><button class="person" onclick={()=>open(person)}><PersonAvatar name={`${person.firstName} ${person.lastName}`} identity={person.id}/><strong>{`${person.firstName} ${person.lastName}`.trim() || person.contactEmail || person.contactPhone || 'Unnamed user'}</strong></button></td>
 <td>{person.contactEmail || '—'}{#if person.contactPhone}<small>{person.contactPhone}</small>{/if}</td><td>{person.title || '—'}</td><td>{personTeams(person).join(', ') || '—'}</td><td>{person.isOwner ? 'Owner' : employeeAccess.roles.filter(role=>personRoleIds(person).includes(role.id)).map(role=>role.name).join(', ') || (person.membershipId ? person.role || 'Existing access policy' : 'Unassigned')}</td><td>{!person.isActive ? 'Archived' : person.membershipId ? 'Active' : 'Not activated'}</td>
 </tr>{:else}<tr><td colspan="6" class="empty">No employees match this view.</td></tr>{/each}
 </tbody></table></div>
 {#if creating || selected}
 <AdminDrawer title={creating ? 'Add employee' : 'Employee details'} width="44rem" close={()=>{selectedId=null;creating=false;}}><div class="editor">
 {#if form?.error}<p class="notice error" role="alert">{form.error}</p>{/if}
 {#if form?.message}<p class="notice" role="status">{form.message}</p>{/if}
 {#if form?.inviteUrl}<div class="notice">Share this activation link with the person: <a href={form.inviteUrl}>{form.inviteUrl}</a></div>{/if}

 {#key selected?.id ?? 'new'}

 <form method="POST" action="?/savePerson" use:enhance={submit}>
 <input type="hidden" name="existingPermissions" value={JSON.stringify(selected ? selected.modulePermissions : [])}/><input type="hidden" name="id" value={selected?.id ?? ''}/><input type="hidden" name="version" value={selected?.version ?? ''}/>
 <fieldset disabled={!canWrite || pending || selected?.isActive === false}>
 <div class="fields"><label>First name<input name="firstName" required maxlength="100" value={selected?.firstName ?? ''}/></label><label>Last name<input name="lastName" maxlength="100" value={selected?.lastName ?? ''}/></label></div>
 {#if creating}<label>Login email or phone<input name="loginIdentifier" required placeholder="person@example.com or +14195551234"/></label><p class="help">Assign a role in Settings to grant module access, then create an invitation.</p>{/if}
 <div class="fields"><label>Contact email<input type="email" name="contactEmail" value={selected?.contactEmail ?? ''}/></label><label>Contact phone<input name="contactPhone" maxlength="200" value={selected?.contactPhone ?? ''}/></label></div>
 <p class="help">Contact details are company records. They do not change verified sign-in credentials.</p>
 <label>Job title<input name="title" maxlength="200" value={selected?.title ?? ''}/></label>
 <div class="fields">
 {#if !canDelete}{#each personRoleIds(selected) as id}<input type="hidden" name="employeeRoleIds" value={id}/>{/each}{/if}
 <div class="roles"><h3>Roles</h3>{#each employeeAccess.roles as role}<label class="role-choice"><input type="checkbox" name="employeeRoleIds" value={role.id} bind:group={assignedRoleIds} disabled={!canDelete}/>{role.name}</label>{/each}<p class="help">Module access combines permissions from all assigned roles.</p></div>
 <div class="teams"><h3>Teams</h3>{#each [...new Set([...employeeAccess.teams,...personTeams(selected)])] as team}<label class="role-choice"><input type="checkbox" name="teams" value={team} bind:group={assignedTeams}/>{team}</label>{:else}<p class="help">No teams configured.</p>{/each}</div></div>
 {#if selected}<section class="access"><h3>Role & module access</h3><p>{selected.isOwner ? 'Owner — full access' : employeeAccess.roles.filter(role=>personRoleIds(selected).includes(role.id)).map(role=>role.name).join(', ') || (selected.membershipId ? selected.role || 'Existing access policy' : 'Unassigned')}</p><p class="help">{selected.isOwner ? 'Owners have access to every module.' : personRoleIds(selected).length ? 'Module access is inherited from the assigned role.' : selected.membershipId ? 'This employee uses an existing access policy. Assign a role in Settings to manage module access.' : 'No module access. Assign a role in Settings.'}</p><ul>{#each (selected.isOwner ? [] : rolePermissions.filter(permission=>permission.endsWith('.read'))) as permission}<li>{permission.split('.')[0] === 'bob' ? 'Ask Bob / Today' : permission.split('.')[0]}{permission === 'estimates.read' && rolePermissions.includes('estimates.submit') && !rolePermissions.includes('estimates.manage') ? ' · Draft and submit' : rolePermissions.includes(`${permission.split('.')[0]}.write`) ? ' · Manage' : ' · View'}</li>{/each}</ul></section>{/if}
 {#if canManagePortal}<p class="help"><a href={`${page.url.pathname.replace(/\/users$/, '')}/settings/people`}>Manage roles, assignments, and teams in Settings</a></p>{/if}
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

 </div></AdminDrawer>{/if}
</section>
<style>
 .roles{grid-column:1 / -1}.role-choice{display:flex;flex-direction:row;align-items:center;gap:.5rem}.role-choice input{width:1rem;height:1rem}
 .people{width:100%;color:var(--text-strong)}header{display:flex;justify-content:space-between;align-items:center;gap:1rem;margin-bottom:2rem}h1{font-size:1.8rem;font-weight:750}h3{font-weight:700;margin:1.5rem 0 .75rem}.eyebrow{text-transform:uppercase;font-size:.7rem;letter-spacing:.15em}p,.help{color:var(--text-muted)}.help{font-size:.85rem;margin:.5rem 0 1rem}.filters{display:flex;gap:1rem;margin-bottom:1rem}.filters label:first-child{flex:1;max-width:32rem}.table-scroll{overflow-x:auto;border:1px solid var(--border);border-radius:8px;background:var(--surface)}table{width:100%;border-collapse:collapse;text-align:left}th{font-size:.8rem;color:var(--text-muted);font-weight:600}th,td{padding:1rem;border-bottom:1px solid var(--border);white-space:nowrap}tbody tr:last-child td{border-bottom:0}small{display:block;color:var(--text-muted);margin-top:.25rem}.person{display:flex;align-items:center;gap:.75rem;background:transparent;color:var(--text-strong);border:0;padding:0;text-align:left}.person:hover strong{text-decoration:underline}.selected{background:var(--teal-soft)}.fields{display:grid;grid-template-columns:1fr 1fr;gap:1rem}label{display:flex;flex-direction:column;gap:.4rem;font-size:.85rem;margin-bottom:.8rem}input,select{border:1px solid var(--input-border);border-radius:6px;padding:.65rem;min-width:0;background:var(--surface);color:var(--text-strong)}button{border:1px solid var(--input-border);border-radius:6px;padding:.65rem 1rem;background:var(--cta);color:var(--cta-text);font-weight:600}button:disabled,fieldset:disabled{opacity:.6}fieldset{border:0;padding:0;min-width:0}.notice{background:var(--positive-soft);padding:1rem;margin-bottom:1rem;border-radius:8px;overflow-wrap:anywhere}.error{background:var(--critical-soft);color:var(--critical-text)}.empty{padding:1.5rem}.removal,.access{border-top:1px solid var(--border);margin-top:1rem}.danger{background:var(--surface);color:var(--critical-text);margin-top:.5rem}.delete-actions{display:flex;gap:.75rem}.secondary{background:var(--surface);color:var(--text-base)}a{text-decoration:underline}li{text-transform:capitalize;font-size:.85rem;margin:.35rem 0}button:focus-visible{outline:2px solid var(--focus-ring);outline-offset:3px}@media(max-width:640px){.fields{grid-template-columns:1fr}header,.filters{align-items:stretch;flex-direction:column}}
</style>
