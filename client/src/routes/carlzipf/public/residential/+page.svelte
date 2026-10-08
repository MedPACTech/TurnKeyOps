<script lang="ts">
 import Residential from '$lib/components/carlzipf/Residential.svelte';
 import Seo from '$lib/components/carlzipf/Seo.svelte';
 import type { PageData, ActionData } from './$types';
 let { data, form }: { data: PageData; form: ActionData } = $props();
</script>
<Seo content={data.content} origin={data.canonicalOrigin} base={data.publicBase} residential />
<Residential {data} {form} />
