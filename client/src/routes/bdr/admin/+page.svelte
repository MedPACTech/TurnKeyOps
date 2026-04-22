<script>
	import { goto } from '$app/navigation';
	import { page } from '$app/state';

	$effect(() => {
		const role = page.url.searchParams.get('role');
		goto(`/bdr/admin/dashboard${role ? `?role=${role}` : ''}`, { replaceState: true });
	});
</script>
