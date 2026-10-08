<script lang="ts">
 import CustomerManagement from '$lib/components/admin/CustomerManagement.svelte';
 import type { PageProps } from './$types';
 let {data,form}:PageProps=$props();
</script>
<CustomerManagement {data} {form}/>
