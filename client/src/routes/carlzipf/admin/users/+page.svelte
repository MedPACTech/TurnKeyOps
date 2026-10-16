<script lang="ts">
 import { enhance } from '$app/forms';
 import PeopleManagement from '$lib/components/admin/PeopleManagement.svelte';
 import type { PageProps } from './$types';
 let { data, form }: PageProps = $props();
 const activeUsers = $derived(data.users.filter(user => user.status.toLowerCase() === 'active' && user.userId));
 let saving = $state(false);
</script>
<PeopleManagement initialFilter="employee" canManagePortal={data.role === 'owner' && data.modulePermissions.includes('settings.write')} people={data.people} customerLinks={data.customerLinks} {form} canDelete={data.canDeleteUsers} canWrite={data.modulePermissions.includes('users.write')}>
 {#snippet personDetails(person)}
  {#if person.isActive && person.profileTypes.includes('employee') && activeUsers.some(user => user.membershipId === person.membershipId)}
   <section class="mt-6 border-t border-[var(--border)] pt-5">
    <h3 class="font-bold">Technician work types</h3>
    <p class="mt-2 text-sm text-[var(--text-muted)]">Work this employee can perform as a technician. Leave both unchecked if they do not perform technician work.</p>
    <form method="POST" action="?/capabilities" use:enhance={() => {
     saving = true;
     return async ({ update }) => { try { await update({ reset: false }); } finally { saving = false; } };
    }}>
     <input type="hidden" name="membershipId" value={person.membershipId} />
     <input type="hidden" name="version" value={data.version} />
     <fieldset disabled={saving || !data.modulePermissions.includes('users.write')} class="mt-4 flex flex-wrap items-center gap-5">
      <legend class="sr-only">Technician work types</legend>
      {#each ['residential', 'commercial'] as type}
       <label class="flex min-h-11 items-center gap-2 capitalize"><input type="checkbox" name="capabilities" value={type} checked={data.settings.techCapabilities[person.membershipId!]?.some(item => item === type) ?? false} />{type}</label>
      {/each}
      {#if data.modulePermissions.includes('users.write')}<button class="btn-secondary min-h-11 rounded border px-4 font-semibold" type="submit">{saving ? 'Saving…' : 'Save work types'}</button>{/if}
     </fieldset>
    </form>
   </section>
  {/if}
 {/snippet}
</PeopleManagement>
