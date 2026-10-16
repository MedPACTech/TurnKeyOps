<script lang="ts">
 import PeopleManagement from '$lib/components/admin/PeopleManagement.svelte';
 import type { PageProps } from './$types';
 let {data,form}:PageProps=$props();
</script>
<PeopleManagement canManagePortal={data.role === 'owner' && data.modulePermissions.includes('settings.write')} employeeAccess={data.employeeAccess} people={data.people} customerLinks={data.customerLinks} {form} canDelete={data.canDeleteUsers} canWrite={data.modulePermissions.includes('users.write')}/>
