<script lang="ts">
 import LeadSettings from '$lib/components/leads/LeadSettings.svelte';
 let {data,form}=$props();
</script>
<LeadSettings {data} {form}/>
