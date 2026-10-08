<script lang="ts">
 import Commercial from '$lib/components/carlzipf/Commercial.svelte';
 import Seo from '$lib/components/carlzipf/Seo.svelte';
 import type { PageData, ActionData } from './$types';
 let { data, form }: { data: PageData; form: ActionData } = $props();
</script>
<Seo content={data.content} origin={data.canonicalOrigin} base={data.publicBase}  />
<Commercial {data} {form} />
