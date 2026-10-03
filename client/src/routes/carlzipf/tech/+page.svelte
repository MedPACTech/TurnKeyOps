<script lang="ts">
	import FieldWorkspace from '$lib/components/locksmith/FieldWorkspace.svelte';
	import { locksmithCatalog } from '$lib/locksmith';
	let { data } = $props();
</script>

{#key `${data.techContext.tenantId}:${data.techContext.userId}:${data.techContext.capabilities.join(',')}`}
	<FieldWorkspace context={data.techContext} catalog={data.pricingContext?.catalog.length ? data.pricingContext.catalog : locksmithCatalog} pricingContext={data.pricingContext} pricingError={data.pricingError} />
{/key}
