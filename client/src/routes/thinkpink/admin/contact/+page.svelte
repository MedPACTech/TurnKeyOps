<script lang="ts">
 import PeopleManagement from '$lib/components/admin/PeopleManagement.svelte';
 import type { PageProps } from './$types';
 let {data,form}:PageProps=$props();
</script>
<PeopleManagement people={data.people} customerLinks={data.customerLinks} {form} canWrite={data.modulePermissions.includes('users.write')}/>
