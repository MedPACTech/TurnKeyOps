<script lang="ts">
	import TenantUserManagement from '$lib/components/admin/TenantUserManagement.svelte';
	import type { PageProps } from './$types';

	let { data, form }: PageProps = $props();
</script>

<TenantUserManagement tenantName={data.tenantName} users={data.users} {form} />
