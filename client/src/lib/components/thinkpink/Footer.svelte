<script lang="ts">
	import { navLinks, site } from '$lib/tenants/thinkpink/content';

	const links = [...navLinks, { href: '#quote', label: 'Free Quote' }];
</script>

<footer class="bg-ink text-bone px-5 py-12 sm:px-8">
	<div
		class="mx-auto flex max-w-[1200px] flex-wrap items-center justify-between gap-8"
	>
		<div class="flex flex-col gap-1.5">
			<span class="display text-2xl font-black">
				Think <span class="text-pink-bright">Pink</span> Land Clearing
			</span>
			<span class="text-dark-body text-sm">
				{site.tagline} ·
				<a href={site.phoneHref} class="text-pink-bright hover:underline">{site.phone}</a>
			</span>
		</div>

		<nav class="flex flex-wrap gap-6 text-sm font-semibold">
			{#each links as link (link.href)}
				<a href={link.href} class="text-dark-body-2 hover:text-pink-bright transition-colors">
					{link.label}
				</a>
			{/each}
		</nav>

		<span class="text-muted-2 text-[13px]">© 2026 {site.legalName}</span>
	</div>
</footer>

