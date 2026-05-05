<script lang="ts">
	import { page } from '$app/state';
	import AdminShell from '$lib/components/admin/AdminShell.svelte';
	import {
		getBdrAdminNav,
		getBdrAdminShellState,
		normalizeBdrAdminPath
	} from '$lib/config/platform';
	import type { Snippet } from 'svelte';

	let { children, data }: { children: Snippet; data: { role: 'owner' | 'office-admin' } } = $props();

	const currentRole = $derived(data.role);
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
