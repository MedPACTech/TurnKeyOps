<script lang="ts">
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
			icon: '⚙️',
			detail: 'Estimate constants and admin-controlled rules'
		},
		{
			id: 'website',
			label: 'Website',
			href: '/bdr/admin/website',
			icon: '🌐',
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
					? 'border-transparent bg-[#fff4ea] shadow-sm ring-1 ring-[rgba(249,115,22,0.32)]'
					: 'border-transparent bg-white/80 shadow-sm hover:bg-white'
			}`}
		>
			<span class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white text-lg shadow-sm">
				{tab.icon}
			</span>
			<span class="min-w-0">
				<span class="block text-sm font-semibold text-[var(--text-strong)]">{tab.label}</span>
				<span class="mt-1 block text-xs leading-5 text-[var(--text-muted)]">{tab.detail}</span>
			</span>
		</a>
	{/each}
</div>
