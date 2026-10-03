<script lang="ts">
	import BobWorkspace from '../../../bdr/admin/bob/+page.svelte';
	let { data, form } = $props();
	const bobData = $derived(data as never);
</script>

<BobWorkspace data={bobData} {form} params={{}} />
