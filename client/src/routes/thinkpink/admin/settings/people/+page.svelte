<script lang="ts">
 import EmployeeAccessSettings from '$lib/components/admin/EmployeeAccessSettings.svelte';
 import type { PageProps } from './$types';
 let { data, form }: PageProps = $props();
</script>
<EmployeeAccessSettings employees={data.employees} configuration={data.employeeAccess} version={data.version} canManage={data.canManage} {form}/>
