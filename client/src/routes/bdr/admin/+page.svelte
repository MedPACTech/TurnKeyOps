<script>
	import { goto } from '$app/navigation';

	$effect(() => {
		goto('/bdr/admin/bob', { replaceState: true });
	});
</script>
