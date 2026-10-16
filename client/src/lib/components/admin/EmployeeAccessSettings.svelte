<script lang="ts">
 import { enhance } from '$app/forms';
 import { page } from '$app/state';
 import type { EmployeeAccessConfiguration } from '$lib/server/people';
 let { configuration, version, canManage, form }: { configuration: EmployeeAccessConfiguration; version: string; canManage: boolean; form?: { message?: string; error?: string } | null } = $props();
 let roles = $state<EmployeeAccessConfiguration['roles']>([]);
 let teams = $state<string[]>([]);
 let loaded = $state<string | null>(null);
 let saving = $state(false);
 let teamName = $state('');
 $effect(() => { if (loaded !== version) { roles = structuredClone(configuration.roles); teams = [...configuration.teams]; loaded = version; } });
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
 function addTeam() { const name = teamName.trim(); if (name && !teams.some(t => t.toLowerCase() === name.toLowerCase())) { teams.push(name); teamName = ''; } }
</script>
<svelte:head><title>Roles & teams</title></svelte:head>
<section class="settings">
 <a href={page.url.pathname.replace(/\/people$/, '')}>← Settings</a>
 <header><h1>Roles & teams</h1><p>Roles define employee module access. Teams group employees.</p></header>
 {#if form?.message}<p role="status" class="notice">{form.message}</p>{/if}
 {#if form?.error}<p role="alert" class="error">{form.error}</p>{/if}
 {#if !canManage}<p>Only company owners can change roles and teams.</p>{/if}
 <form method="POST" action="?/save" use:enhance={() => { saving = true; return async ({ update }) => { try { await update({ reset: false }); } finally { saving = false; } }; }}>
 <input type="hidden" name="version" value={version}/><input type="hidden" name="configuration" value={JSON.stringify({ roles, teams })}/>
 <fieldset disabled={!canManage || saving}>
 <div class="section-heading"><h2>Roles</h2><button type="button" onclick={() => roles.push({ id: crypto.randomUUID(), name: '', modulePermissions: [] })}>Add role</button></div>
 <p class="help">Changes apply to every employee assigned to the role. Owners retain full access.</p>
 {#each roles as role, index (role.id)}
 <section class="role">
 <label>Role name<input maxlength="100" required bind:value={role.name}/></label>
 <div class="permissions">{#each modules as module}<label>{module.name}<select aria-label={`${role.name || 'New role'} — ${module.name}`} value={level(role.modulePermissions, module.id)} onchange={event => setLevel(index, module.id, event.currentTarget.value)}><option value="none">No access</option><option value="read">View</option>{#if module.id === 'estimates'}<option value="submit">Draft and submit</option>{/if}<option value="write">View and manage</option></select></label>{/each}</div>
 </section>
 {/each}
 <h2>Teams</h2>
 <div class="teams">{#each teams as team, index}<div><span>{team}</span><button type="button" aria-label={`Remove team ${team}`} onclick={() => teams.splice(index, 1)}>Remove</button></div>{:else}<p class="help">No teams configured.</p>{/each}</div>
 <div class="team-add"><label>New team<input maxlength="100" bind:value={teamName} onkeydown={event => { if (event.key === 'Enter') { event.preventDefault(); addTeam(); } }}/></label><button type="button" onclick={addTeam}>Add team</button></div>
 {#if canManage}<button class="btn-primary" type="submit">{saving ? 'Saving…' : 'Save roles and teams'}</button>{/if}
 </fieldset></form>
</section>
<style>
 .settings{max-width:1000px;margin:auto;color:var(--text-strong)}header{margin:1.5rem 0}h1{font-size:1.8rem;font-weight:750}h2{font-size:1.2rem;font-weight:700;margin:1.5rem 0 .75rem}p,.help{color:var(--text-muted)}.help{font-size:.85rem;margin:.5rem 0 1rem}fieldset{border:0;min-width:0;padding:0}.section-heading{display:flex;align-items:center;justify-content:space-between}.role{border-top:1px solid var(--border);padding:1.5rem 0}.role>label{max-width:360px}.permissions{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:1rem}label{display:flex;flex-direction:column;gap:.4rem;font-size:.9rem;margin-bottom:.8rem}input,select{background:var(--surface);color:var(--text-strong);border:1px solid var(--input-border);border-radius:6px;padding:.65rem;min-width:0;min-height:44px}button{min-height:44px;padding:.6rem 1rem;border:1px solid var(--border);border-radius:6px;font-weight:600}button:disabled,fieldset:disabled{opacity:.6}.teams>div{display:flex;justify-content:space-between;align-items:center;border-bottom:1px solid var(--border);padding:.5rem 0}.team-add{display:flex;gap:1rem;align-items:end;margin:1rem 0}.team-add label{margin:0;flex:1}.notice,.error{padding:1rem;margin:1rem 0;background:var(--positive-soft)}.error{background:var(--critical-soft);color:var(--critical-text)}a{text-decoration:underline}@media(max-width:760px){.permissions{grid-template-columns:1fr 1fr}}@media(max-width:480px){.permissions{grid-template-columns:1fr}}
</style>
