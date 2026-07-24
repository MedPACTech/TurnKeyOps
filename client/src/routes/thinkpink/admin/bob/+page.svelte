<script lang="ts">
	import BobWorkspace from '../../../bdr/admin/bob/+page.svelte';
	import type { PageProps } from './$types';

	let { data, form }: PageProps = $props();
	const bobData = $derived(data as never);
</script>

<BobWorkspace data={bobData} {form} params={{}} />
