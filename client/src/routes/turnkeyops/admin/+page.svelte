<script>
	import { goto } from '$app/navigation';

	$effect(() => {
		goto('/turnkeyops/admin/dashboard', { replaceState: true });
	});
</script>
