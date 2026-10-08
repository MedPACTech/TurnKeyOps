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
	const inAdmin = $derived(activeNav.slug === 'settings' || activeNav.slug === 'users' || activePath === `${adminBase}/website` || activePath.startsWith(`${adminBase}/website/`));
	const adminSections = $derived([
		...(canSettings ? [{label: 'Settings', href: `${adminBase}/settings`, icon: 'settings'}] : []),
		...(canPeople ? [{label: 'People & Access', href: `${adminBase}/users`, icon: 'users'}] : []),
		...(canSettings && data.role === 'owner' ? [{label: 'Customer Portal', href: `${adminBase}/settings/portal`, icon: 'customers'}] : []),
		...(canSettings && tenantSlug === 'bdr' ? [{label: 'Website', href: `${adminBase}/website`, icon: 'content'}] : [])
	]);
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
	sectionNavigation={inAdmin ? {parentSlug: 'settings', label: 'Admin sections', items: adminSections} : undefined}
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
