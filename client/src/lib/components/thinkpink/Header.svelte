<script lang="ts">
	import Logo from './Logo.svelte';
	import { navLinks, site } from '$lib/tenants/thinkpink/content';

	let open = $state(false);
</script>

<header
	class="border-line bg-bone/92 sticky top-0 z-50 border-b backdrop-blur-md"
	id="top-header"
>
	<div class="mx-auto flex h-[76px] max-w-[1200px] items-center justify-between gap-8 px-5 sm:px-8">
		<a href="#top" class="flex items-center" aria-label="Think Pink Land Clearing — home">
			<Logo />
		</a>

		<nav class="hidden items-center gap-7 text-[15px] font-semibold lg:flex">
			{#each navLinks as link (link.href)}
				<a href={link.href} class="text-ink hover:text-pink transition-colors">{link.label}</a>
			{/each}
			<a href={site.phoneHref} class="text-muted hover:text-pink font-bold transition-colors">
				{site.phone}
			</a>
			<a
				href="#quote"
				class="bg-pink hover:bg-pink-dark rounded-md px-[22px] py-[11px] font-bold tracking-[0.02em] text-white transition-colors"
			>
				Get a Free Quote
			</a>
		</nav>

		<div class="flex items-center gap-3 lg:hidden">
			<a
				href={site.phoneHref}
				class="bg-pink hover:bg-pink-dark rounded-md px-4 py-2.5 text-sm font-bold text-white transition-colors"
			>
				Call
			</a>
			<button
				type="button"
				class="border-line-2 text-ink flex h-10 w-10 items-center justify-center rounded-md border"
				aria-expanded={open}
				aria-controls="mobile-nav"
				aria-label={open ? 'Close menu' : 'Open menu'}
				onclick={() => (open = !open)}
			>
				<svg viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="2">
					{#if open}
						<path d="M6 6l12 12M18 6L6 18" stroke-linecap="round" />
					{:else}
						<path d="M4 7h16M4 12h16M4 17h16" stroke-linecap="round" />
					{/if}
				</svg>
			</button>
		</div>
	</div>

	{#if open}
		<nav
			id="mobile-nav"
			class="border-line bg-bone flex flex-col gap-1 border-t px-5 py-4 text-base font-semibold lg:hidden"
		>
			{#each navLinks as link (link.href)}
				<a href={link.href} class="text-ink hover:text-pink py-2" onclick={() => (open = false)}>
					{link.label}
				</a>
			{/each}
			<a
				href="#quote"
				class="bg-pink mt-2 rounded-md px-5 py-3 text-center font-bold text-white"
				onclick={() => (open = false)}
			>
				Get a Free Quote
			</a>
		</nav>
	{/if}
</header>

