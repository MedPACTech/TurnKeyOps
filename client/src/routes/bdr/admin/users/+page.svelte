<script lang="ts">
 import PeopleManagement from '$lib/components/admin/PeopleManagement.svelte';
 import TenantUserManagement from '$lib/components/admin/TenantUserManagement.svelte';
 import type { PageProps } from './$types';
 let {data,form}:PageProps=$props();
</script>
<PeopleManagement people={data.people} customerLinks={data.customerLinks} {form} canWrite={data.modulePermissions.includes('users.write')}/>
<details class="mt-8"><summary class="cursor-pointer font-semibold">Invitations and existing access roles</summary><TenantUserManagement tenantName={data.tenantName} users={data.users} {form}/></details>
