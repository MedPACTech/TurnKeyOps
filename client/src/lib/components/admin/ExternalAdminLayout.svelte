<script lang="ts">
	import { page } from '$app/state';
    import { hasModuleAccess } from '$lib/module-access';
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
            modulePermissions?: string[];
			bobVoice: BobVoiceId;
			adminSession?: { email?: string };
		};
		tenantSlug: TenantSlug;
	} = $props();

	const config = $derived(getExternalAdminConfig(tenantSlug));
	const activePath = $derived(normalizeExternalAdminPath(config, page.url.pathname));
	const activeNav = $derived(getExternalAdminActiveNav(config, activePath));
	const canSettings = $derived(!data.modulePermissions || hasModuleAccess(data.modulePermissions, 'settings'));
	const canPeople = $derived(!data.modulePermissions || hasModuleAccess(data.modulePermissions, 'users'));
	const adminBase = $derived(`/${tenantSlug}/admin`);
	const inAdmin = $derived(activeNav.slug === 'settings' || activeNav.slug === 'users');
	const navigation = $derived(config.navigation
		.filter(item => item.slug !== 'users' && (item.slug === 'settings'
			? canSettings || canPeople
			: !data.modulePermissions || hasModuleAccess(data.modulePermissions, item.slug === 'customers' ? 'contacts' : item.slug)))
		.map(item => item.slug === 'settings' && !canSettings ? {...item, href: `${adminBase}/users`} : item));
</script>

<svelte:head><title>{config.workspaceLabel} · TurnKeyOps</title></svelte:head>

<AdminShell
	role={data.role}
	{activePath}
	activeNav={inAdmin ? {...activeNav, slug: 'settings'} : activeNav}
	initialBobVoice={data.bobVoice}
	navItems={navigation}
	tenantName={config.tenant.name}
	workspaceLabel={config.workspaceLabel}
	workspaceSummary={config.workspaceSummary}
	homeHref={config.homeHref}
	publicHref={config.publicHref}
	operatorEmail={data.adminSession?.email ?? ''}
	theme={config.theme}
>
	{#if inAdmin}
		<nav aria-label="Admin sections" class="admin-sections">
			{#if canSettings}<a href={`${adminBase}/settings`} aria-current={activeNav.slug === 'settings' ? 'page' : undefined}>Settings</a>{/if}
			{#if canPeople}<a href={`${adminBase}/users`} aria-current={activeNav.slug === 'users' ? 'page' : undefined}>People &amp; Access</a>{/if}
		</nav>
	{/if}
	{@render children()}
</AdminShell>

<style>
	.admin-sections { display: flex; flex-wrap: wrap; gap: .5rem; padding: .75rem 1.5rem; border-bottom: 1px solid var(--border); }
	.admin-sections a { display: inline-flex; align-items: center; min-height: 44px; padding: .5rem .9rem; border-radius: .4rem; color: var(--text-muted); text-decoration: none; }
	.admin-sections a[aria-current="page"] { color: var(--text-strong); background: var(--surface-elevated); font-weight: 600; }
	.admin-sections a:focus-visible { outline: 3px solid var(--teal-text); outline-offset: 3px; }
</style>
