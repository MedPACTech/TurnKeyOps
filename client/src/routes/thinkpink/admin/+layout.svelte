<script lang="ts">
	import { page } from '$app/state';
	import type { Snippet } from 'svelte';

	let { children, data }: { children: Snippet; data: { role: string } } = $props();
	const navigation = [
		{ label: 'Dashboard', href: '/thinkpink/admin/dashboard' },
		{ label: 'Requests', href: '/thinkpink/admin/requests' },
		{ label: 'Estimates', href: '/thinkpink/admin/estimates' },
		{ label: 'Jobs', href: '/thinkpink/admin/jobs' },
		{ label: 'Trade defaults', href: '/thinkpink/admin/settings' },
		{ label: 'Public site', href: '/thinkpink/public' }
	];
</script>

<svelte:head><title>Think Pink Admin · TurnKeyOps</title></svelte:head>

<div class="min-h-screen bg-[#faf7f8] text-[#1c1418] lg:grid lg:grid-cols-[248px_1fr]">
	<aside class="border-b border-[#eadde3] bg-[#1c1418] px-5 py-5 text-white lg:min-h-screen lg:border-r lg:border-b-0">
		<div class="flex items-center justify-between gap-4 lg:block">
			<a href="/thinkpink/admin/dashboard" class="font-black tracking-tight">
				<span class="text-xl">Think <span class="text-[#ff4fb2]">Pink</span></span>
				<span class="block text-[0.62rem] uppercase tracking-[0.18em] text-white/55">Land Clearing · Admin</span>
			</a>
			<span class="rounded border border-white/15 px-2 py-1 text-xs text-white/60">{data.role}</span>
		</div>
		<nav class="mt-5 flex gap-2 overflow-x-auto lg:flex-col" aria-label="Think Pink admin navigation">
			{#each navigation as item}
				<a
					href={item.href}
					class={`whitespace-nowrap rounded-md px-3 py-2.5 text-sm font-semibold transition ${
						page.url.pathname === item.href ? 'bg-[#d40f80] text-white' : 'text-white/65 hover:bg-white/10 hover:text-white'
					}`}
				>{item.label}</a>
			{/each}
		</nav>
		<a href="/turnkeyops/admin/tenants" class="mt-6 hidden text-xs text-white/45 hover:text-white lg:block">Open TurnKeyOps Internal Admin</a>
	</aside>
	<main class="min-w-0 px-4 py-6 sm:px-7 lg:px-10 lg:py-9">
		{@render children()}
	</main>
</div>
