<script lang="ts">
	import { page } from '$app/state';
	import AdminShell from '$lib/components/admin/AdminShell.svelte';
	import {
		getBdrAdminNav,
		getBdrAdminShellState,
		normalizeBdrAdminPath,
		normalizeBdrAdminRole
	} from '$lib/config/platform';
	import type { Snippet } from 'svelte';

	let { children }: { children: Snippet } = $props();

	const currentRole = $derived(normalizeBdrAdminRole(page.url.searchParams.get('role')));
	const activePath = $derived(normalizeBdrAdminPath(page.url.pathname));
	const activeNav = $derived(getBdrAdminNav(activePath));
	const shellState = $derived(getBdrAdminShellState(activePath));
</script>

<AdminShell
	role={currentRole}
	{activePath}
	{activeNav}
	title={shellState.title}
	description={shellState.description}
	context={shellState.context}
	focus={shellState.focus}
	canvas={shellState.canvas}
>
	{@render children()}
</AdminShell>
