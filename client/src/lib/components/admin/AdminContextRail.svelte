<script lang="ts">
	import AdminIcon from '$lib/components/admin/AdminIcon.svelte';
	type AdminContextTab = {
		id: 'defaults' | 'website';
		label: string;
		href: string;
		icon: string;
		detail: string;
	};

	let { active = 'defaults' }: { active?: AdminContextTab['id'] } = $props();

	const tabs: AdminContextTab[] = [
		{
			id: 'defaults',
			label: 'Defaults',
			href: '/bdr/admin/settings',
			icon: 'settings',
			detail: 'Estimate constants and admin-controlled rules'
		},
		{
			id: 'website',
			label: 'Website',
			href: '/bdr/admin/website',
			icon: 'content',
			detail: 'Public-site content, assets, and quote flow'
		}
	];
</script>

<div class="space-y-2">
	{#each tabs as tab}
		<a
			href={tab.href}
			aria-current={active === tab.id ? 'page' : undefined}
			class={`group flex items-start gap-3 rounded-lg border px-3 py-3 text-left transition ${
				active === tab.id
					? 'border-transparent bg-[var(--teal-soft)] shadow-sm ring-1 ring-[var(--teal-border)]'
					: 'border-transparent bg-[var(--surface)] shadow-sm hover:bg-[var(--surface)]'
			}`}
		>
			<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-[var(--surface)] text-lg shadow-sm">
				<AdminIcon name={tab.icon} />
			</span>
			<span class="min-w-0">
				<span class="block text-sm font-semibold text-[var(--text-strong)]">{tab.label}</span>
				<span class="mt-1 block text-xs leading-5 text-[var(--text-muted)]">{tab.detail}</span>
			</span>
		</a>
	{/each}
</div>
