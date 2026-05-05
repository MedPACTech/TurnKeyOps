<script>
	import { goto } from '$app/navigation';

	$effect(() => {
		goto('/bdr/admin/dashboard', { replaceState: true });
	});
</script>
