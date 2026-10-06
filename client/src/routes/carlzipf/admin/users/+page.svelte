<script lang="ts">
 import PeopleManagement from '$lib/components/admin/PeopleManagement.svelte';
 import type { PageProps } from './$types';
 let { data, form }: PageProps = $props();
 const activeUsers = $derived(data.users.filter(user => user.status.toLowerCase() === 'active' && user.userId));
</script>
<PeopleManagement people={data.people} customerLinks={data.customerLinks} {form} canDelete={data.canDeleteUsers} canWrite={data.modulePermissions.includes('users.write')}/>
<section class="mx-auto mb-10 max-w-7xl rounded-xl bg-[var(--surface)] p-6 shadow-sm">
 <h2 class="text-xl font-bold">Tech job types</h2>
 <p class="mt-2 text-sm text-[var(--text-muted)]">Choose residential, commercial, or both. An unchecked user has no tech capability. Everyone shares the same staffing pool.</p>
 <p class="mt-2 text-sm text-[var(--warning-text)]">Capabilities are saved now; assignment and quoting enforcement will follow when those workflows are connected.</p>
 {#each activeUsers as user}
  <form method="POST" action="?/capabilities" class="mt-5 flex flex-wrap items-center gap-5 border-t pt-5">
   <input type="hidden" name="membershipId" value={user.membershipId} /><input type="hidden" name="version" value={data.version} />
   <p class="min-w-48 font-semibold">{user.email || user.phone || user.userId}</p>
   {#each ['residential', 'commercial'] as type}
    <label class="flex items-center gap-2 capitalize"><input type="checkbox" name="capabilities" value={type} checked={data.settings.techCapabilities[user.membershipId]?.some(item => item === type) ?? false} />{type}</label>
   {/each}
   <button class="min-h-11 rounded border px-4 font-semibold" type="submit">Save capabilities</button>
  </form>
 {:else}<p class="mt-5 text-sm text-[var(--text-muted)]">Invite a user above. Capabilities become available after the invitation is accepted.</p>{/each}
</section>
