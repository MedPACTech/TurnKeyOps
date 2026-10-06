<script lang="ts">
 import LeadsWorkspace from '$lib/components/leads/LeadsWorkspace.svelte';
 let { data, form } = $props();
</script>
<LeadsWorkspace {data} {form} />
