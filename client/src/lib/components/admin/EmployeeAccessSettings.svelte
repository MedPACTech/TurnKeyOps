<script lang="ts">
 import AdminDrawer from './AdminDrawer.svelte';
 import { personRoleIds, personTeams } from '$lib/employee-roles';
 import { enhance } from '$app/forms';
 import { page } from '$app/state';
 import type { EmployeeAccessConfiguration, Person } from '$lib/server/people';
 let { configuration, version, canManage, employees = [], form }: { employees?: Person[]; configuration: EmployeeAccessConfiguration; version: string; canManage: boolean; form?: { message?: string; error?: string } | null } = $props();
 let roles = $state<EmployeeAccessConfiguration['roles']>([]);

 let loaded = $state<string | null>(null);
 let saving = $state(false);
 let teamName = $state('');
 let teamDrawer = $state<'edit'|'assignments'|null>(null);
 let selectedTeam = $state('');
 let teamEmployeeId = $state('');
 const teamAssigned = $derived(employees.filter(person=>personTeams(person).includes(selectedTeam)));
 const teamAvailable = $derived(employees.filter(person=>!personTeams(person).includes(selectedTeam)));
 function openTeam(name: string, mode: 'edit'|'assignments') { selectedTeam=name;teamName=name;teamEmployeeId='';teamDrawer=mode; }
 const saveTeam = () => { saving=true;return async ({update,result}:{update:(options?:{reset?:boolean})=>Promise<void>;result:{type:string}})=>{try {await update({reset:false});if(result.type==='success')teamDrawer=null;}finally{saving=false;}}; };
 let drawer = $state<'edit' | 'assignments' | null>(null);
 let selectedRoleId = $state('');
 const selectedRole = $derived(roles.find(role => role.id === selectedRoleId));
 const assigned = $derived(employees.filter(person => personRoleIds(person).includes(selectedRoleId)));
 const available = $derived(employees.filter(person => !personRoleIds(person).includes(selectedRoleId)));
 function openRole(id: string, mode: 'edit' | 'assignments') { roles = structuredClone(configuration.roles); selectedRoleId = id; drawer = mode; }
 function createRole() { roles = structuredClone(configuration.roles); const role = { id: crypto.randomUUID(), name: '', modulePermissions: [] }; roles.push(role); selectedRoleId = role.id; drawer = 'edit'; }
 const save = () => { saving = true; return async ({update,result}: {update:(options?:{reset?:boolean})=>Promise<void>;result:{type:string}}) => { try { await update({reset:false}); if(result.type === 'success') drawer = null; } finally { saving = false; } }; };
 $effect(() => { if (loaded !== version) { roles = structuredClone(configuration.roles); loaded = version; } });
 const modules = [{id:'bob',name:'Ask Bob / Today'},{id:'calendar',name:'Calendar'},{id:'jobs',name:'Jobs'},{id:'inventory',name:'Inventory'},{id:'estimates',name:'Estimates'},{id:'contacts',name:'Contacts'},{id:'purchasing',name:'Purchasing'},{id:'leads',name:'Leads'},{id:'requests',name:'Requests'},{id:'invoices',name:'Invoices'},{id:'finance',name:'Finance'},{id:'payroll',name:'Payroll'},{id:'dashboard',name:'Dashboard'},{id:'users',name:'People & Access'},{id:'settings',name:'Settings'},{id:'billing',name:'Billing'}];
 const level = (permissions: string[], module: string) => permissions.includes(`${module}.write`) ? module === 'estimates' && !permissions.includes('estimates.manage') ? 'submit' : 'write' : permissions.includes(`${module}.read`) ? 'read' : 'none';
 function setLevel(index: number, module: string, value: string) {
  const permissions = roles[index].modulePermissions.filter(p => !p.startsWith(`${module}.`));
  if (value !== 'none') permissions.push(`${module}.read`);
  if (value === 'write' || value === 'submit') permissions.push(`${module}.write`);
  if (module === 'estimates' && (value === 'submit' || value === 'write')) permissions.push('estimates.submit');
  if (module === 'estimates' && value === 'write') permissions.push('estimates.manage');
  roles[index].modulePermissions = permissions;
 }

</script>
<svelte:head><title>Roles & teams</title></svelte:head>
<section class="settings">
 <a href={page.url.pathname.replace(/\/people$/, '')}>← Settings</a>
 <header><h1>Roles & teams</h1><p>Roles define module access. Employees can have more than one role.</p></header>
 {#if !drawer && !teamDrawer}{#if form?.message}<p role="status" class="notice">{form.message}</p>{/if}{#if form?.error}<p role="alert" class="error">{form.error}</p>{/if}{/if}
 {#if !canManage}<p>Only company owners can change roles and teams.</p>{/if}
 <section class="settings-card" aria-labelledby="roles-heading">
 <div class="section-heading"><h2 id="roles-heading">Roles</h2>{#if canManage}<button type="button" class="btn-primary" onclick={createRole}>Add role</button>{/if}</div>
 <div class="table-scroll"><table><caption class="sr-only">Configured roles and assignments</caption><thead><tr><th>Role</th><th>People assigned</th><th class="actions-heading">Actions</th></tr></thead><tbody>
 {#each configuration.roles as role}<tr><th scope="row">{role.name}</th><td>{employees.filter(person=>personRoleIds(person).includes(role.id)).length}</td><td class="actions"><button class="team-secondary" onclick={()=>openRole(role.id,'edit')} aria-label={`Edit ${role.name}`}>Edit</button><button class="team-secondary" onclick={()=>openRole(role.id,'assignments')} aria-label={`Assignments for ${role.name}`}>Assignments</button></td></tr>{:else}<tr><td colspan="3">No roles configured.</td></tr>{/each}
 </tbody></table></div>
 </section>
 <section class="settings-card" aria-labelledby="teams-heading">
 <div class="section-heading"><h2 id="teams-heading">Teams</h2>{#if canManage}<button class="btn-primary" onclick={()=>openTeam('','edit')}>Add team</button>{/if}</div>
 <div class="table-scroll"><table><caption class="sr-only">Configured teams and assignments</caption><thead><tr><th>Team</th><th>People assigned</th><th class="actions-heading">Actions</th></tr></thead><tbody>
 {#each configuration.teams as team}<tr><th scope="row">{team}</th><td>{employees.filter(person=>personTeams(person).includes(team)).length}</td><td class="actions"><button class="team-secondary" aria-label={`Edit team ${team}`} onclick={()=>openTeam(team,'edit')}>Edit</button><button class="team-secondary" aria-label={`Assignments for team ${team}`} onclick={()=>openTeam(team,'assignments')}>Assignments</button></td></tr>{:else}<tr><td colspan="3">No teams configured.</td></tr>{/each}
 </tbody></table></div>
 </section>
 {#if teamDrawer}<AdminDrawer title={teamDrawer==='edit' ? selectedTeam ? 'Edit team' : 'Add team' : `${selectedTeam} assignments`} close={()=>teamDrawer=null} width="44rem">
 {#if form?.message}<p role="status" class="notice">{form.message}</p>{/if}{#if form?.error}<p role="alert" class="error">{form.error}</p>{/if}
 {#if teamDrawer==='edit'}<form method="POST" action="?/saveTeam" use:enhance={saveTeam}>
 <input type="hidden" name="version" value={version}/><input type="hidden" name="previousName" value={selectedTeam}/>
 <fieldset disabled={!canManage || saving}><label>Team name<input name="teamName" required maxlength="100" bind:value={teamName}/></label>
 {#if selectedTeam}<p class="help">Renaming this team keeps its employee assignments.</p>{/if}
 {#if canManage}<button type="submit" class="btn-primary">{saving ? 'Saving…' : 'Save team'}</button>{/if}</fieldset></form>
 {/if}{#if selectedTeam}<h3>Assigned employees ({teamAssigned.length})</h3>
 {#each teamAssigned as person}<div class="assignment"><span>{person.firstName} {person.lastName}</span>{#if canManage}<form method="POST" action="?/assignTeam" use:enhance><input type="hidden" name="id" value={person.id}/><input type="hidden" name="version" value={person.version}/><input type="hidden" name="team" value={selectedTeam}/><input type="hidden" name="operation" value="remove"/><button class="remove-action" aria-label={`Remove ${person.firstName} ${person.lastName} from ${selectedTeam}`}>Remove</button></form>{/if}</div>{:else}<p class="help">No employees assigned.</p>{/each}
 {#if canManage && teamAvailable.length}<form class="team-add" method="POST" action="?/assignTeam" use:enhance>
 <input type="hidden" name="team" value={selectedTeam}/><input type="hidden" name="version" value={teamAvailable.find(person=>person.id===teamEmployeeId)?.version || ''}/>
 <label>Employee<select name="id" required bind:value={teamEmployeeId}><option value="">Select an employee</option>{#each teamAvailable as person}<option value={person.id}>{person.firstName} {person.lastName}{personTeams(person).length ? ` — ${personTeams(person).join(', ')}` : ''}</option>{/each}</select></label><button class="btn-primary" type="submit">Add assignment</button></form>
 <p class="help">Employees can belong to more than one team.</p>{/if}
 <p class="help">Team assignments can also be changed on an employee’s People & Access profile.</p>
 {/if}</AdminDrawer>{/if}
 {#if drawer && selectedRole}<AdminDrawer title={drawer === 'edit' ? (configuration.roles.some(role=>role.id===selectedRoleId) ? 'Edit role' : 'Add role') : `${selectedRole.name} assignments`} close={()=>drawer=null} width="44rem">
 {#if form?.message}<p role="status" class="notice">{form.message}</p>{/if}{#if form?.error}<p role="alert" class="error">{form.error}</p>{/if}
 {#if drawer === 'edit'}
 <form method="POST" action="?/save" use:enhance={save}>
 <input type="hidden" name="version" value={version}/><input type="hidden" name="configuration" value={JSON.stringify({...configuration,roles})}/>
 <fieldset disabled={!canManage || saving}><label>Role name<input maxlength="100" required bind:value={selectedRole.name}/></label>
 <p class="help">Module permissions apply to every employee assigned to this role.</p>
 <div class="permissions">{#each modules as module}<label>{module.name}<select aria-label={`${selectedRole.name || 'New role'} — ${module.name}`} value={level(selectedRole.modulePermissions,module.id)} onchange={event=>setLevel(roles.findIndex(role=>role.id===selectedRoleId),module.id,event.currentTarget.value)}><option value="none">No access</option><option value="read">View</option>{#if module.id === 'estimates'}<option value="submit">Draft and submit</option>{/if}<option value="write">View and manage</option></select></label>{/each}</div>
 {#if canManage}<button class="btn-primary" type="submit">{saving ? 'Saving…' : 'Save role'}</button>{/if}</fieldset></form>
 {:else}
 <h3>Assigned employees ({assigned.length})</h3>
 {#each assigned as person}<div class="assignment"><span>{person.firstName} {person.lastName}</span>{#if canManage}<form method="POST" action="?/assignRole" use:enhance><input type="hidden" name="id" value={person.id}/><input type="hidden" name="version" value={person.version}/><input type="hidden" name="employeeRoleId" value={selectedRoleId}/><input type="hidden" name="operation" value="remove"/><button class="remove-action" aria-label={`Remove ${person.firstName} ${person.lastName} from ${selectedRole.name}`}>Remove</button></form>{/if}</div>{:else}<p class="help">No employees assigned.</p>{/each}
 {#if canManage && available.length}<form class="team-add" method="POST" action="?/assignRole" use:enhance>
 <input type="hidden" name="employeeRoleId" value={selectedRoleId}/>
 <label>Employee<select name="id" required onchange={event=>{const employee=available.find(person=>person.id===event.currentTarget.value); const input=event.currentTarget.form?.elements.namedItem('version') as HTMLInputElement; if(input)input.value=employee?.version || '';}}><option value="">Select an employee</option>{#each available as person}<option value={person.id}>{person.firstName} {person.lastName}</option>{/each}</select></label>
 <input type="hidden" name="version" value=""/><button class="btn-primary" type="submit">Add assignment</button></form>{/if}
 <p class="help">Roles and teams can also be assigned from an employee’s People & Access profile.</p>
 {/if}
 </AdminDrawer>{/if}
</section>
<style>
 .settings-card{margin-top:1.5rem;padding:1.25rem;border:1px solid var(--border);border-radius:8px;background:var(--surface)}.settings-card .section-heading{margin-bottom:1rem}.settings-card h2{margin:0}.settings-card tbody tr:last-child :is(th,td){border-bottom:0}
 .settings{width:100%;color:var(--text-strong)}header{margin:1.5rem 0}h1{font-size:1.8rem;font-weight:750}h2{font-size:1.2rem;font-weight:700;margin:1.5rem 0 .75rem}h3{font-weight:650;margin:1rem 0}p,.help{color:var(--text-muted)}.help{font-size:.85rem;margin:.5rem 0 1rem}.section-heading,.assignment{display:flex;align-items:center;justify-content:space-between;gap:1rem}.assignment{padding:1rem 0;border-bottom:1px solid var(--border)}.table-scroll{overflow-x:auto}table{width:100%;border-collapse:collapse;text-align:left}th,td{padding:1rem;border-bottom:1px solid var(--border)}thead th{font-size:.85rem;color:var(--text-muted)}.actions-heading{text-align:right}.actions{display:flex;gap:.75rem;justify-content:flex-end}.team-secondary{border-color:var(--cta);color:var(--cta);background:color-mix(in srgb,var(--cta) 10%,var(--surface))}.team-secondary:hover{background:color-mix(in srgb,var(--cta) 18%,var(--surface))}.remove-action{border-color:var(--critical-text);color:var(--critical-text);background:var(--critical-soft)}button:focus-visible{outline:2px solid var(--focus-ring);outline-offset:3px}fieldset{border:0;min-width:0;padding:0}.permissions{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:1rem}label{display:flex;flex-direction:column;gap:.4rem;font-size:.9rem;margin-bottom:.8rem}input,select{background:var(--surface);color:var(--text-strong);border:1px solid var(--input-border);border-radius:6px;padding:.65rem;min-width:0;min-height:44px}button{min-height:44px;padding:.6rem 1rem;border:1px solid var(--border);border-radius:6px;font-weight:600}button:disabled,fieldset:disabled{opacity:.6}.team-add{display:flex;gap:1rem;align-items:end;margin:1rem 0}.team-add label{margin:0;flex:1}.notice,.error{padding:1rem;margin:1rem 0;background:var(--positive-soft)}.error{background:var(--critical-soft);color:var(--critical-text)}a{text-decoration:underline}@media(max-width:480px){.settings-card{padding:1rem}.permissions{grid-template-columns:1fr}.team-add{align-items:stretch;flex-direction:column}.actions{gap:.25rem}th,td{padding:.65rem}}
</style>
