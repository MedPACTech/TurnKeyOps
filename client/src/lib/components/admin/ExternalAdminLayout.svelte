<script lang="ts">
	import { page } from '$app/state';
	import type { Snippet } from 'svelte';
	import AdminShell from '$lib/components/admin/AdminShell.svelte';
	import {
		getExternalAdminActiveNav,
		getExternalAdminConfig,
		normalizeExternalAdminPath
	} from '$lib/config/external-admin';
	import type { TenantSlug } from '$lib/config/tenants';
	import type { BobVoiceId } from '$lib/bob-voice';
	import type { BdrAdminRole } from '$lib/config/platform';

	let {
		children,
		data,
		tenantSlug
	}: {
		children: Snippet;
		data: {
			role: BdrAdminRole;
			bobVoice: BobVoiceId;
			adminSession?: { email?: string };
		};
		tenantSlug: TenantSlug;
	} = $props();

	const config = $derived(getExternalAdminConfig(tenantSlug));
	const activePath = $derived(normalizeExternalAdminPath(config, page.url.pathname));
	const activeNav = $derived(getExternalAdminActiveNav(config, activePath));
</script>

<svelte:head><title>{config.workspaceLabel} · TurnKeyOps</title></svelte:head>

<AdminShell
	role={data.role}
	{activePath}
	{activeNav}
	initialBobVoice={data.bobVoice}
	navItems={config.navigation}
	tenantName={config.tenant.name}
	workspaceLabel={config.workspaceLabel}
	workspaceSummary={config.workspaceSummary}
	homeHref={config.homeHref}
	publicHref={config.publicHref}
	operatorEmail={data.adminSession?.email ?? ''}
	theme={config.theme}
>
	{@render children()}
</AdminShell>
