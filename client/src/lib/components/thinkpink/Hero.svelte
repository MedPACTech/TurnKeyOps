<script lang="ts">
	import { site } from '$lib/tenants/thinkpink/content';

	const stats = [
		{ value: 'Free', label: 'On-site quotes' },
		{ value: '1–100+', label: 'Acres handled' },
		{ value: 'Fast', label: 'Scheduling' }
	];

	let heroLoaded = $state(true);
</script>

<section
	id="top"
	class="mx-auto grid max-w-[1200px] items-center gap-10 px-5 pt-14 pb-16 sm:px-8 lg:grid-cols-[1.05fr_0.95fr] lg:gap-14 lg:pt-18 lg:pb-22"
>
	<div class="flex flex-col gap-6">
		<div class="flex items-center gap-2.5">
			<span class="bg-pink h-1 w-[34px] rounded-sm"></span>
			<span class="eyebrow text-muted">{site.tagline}</span>
		</div>

		<h1
			class="display text-[clamp(2.75rem,9vw,4.75rem)] leading-[0.98] font-black tracking-[0.005em] text-pretty"
		>
			Overgrown?<br />We think <span class="text-pink">pink</span>.<br />You'll see green.
		</h1>

		<p class="text-body max-w-[52ch] text-lg leading-relaxed text-pretty sm:text-[19px]">
			Land clearing, forestry mulching, and brush removal for {site.region} landowners, builders, and
			farms. Big machines, tidy results — and yes, they're pink.
		</p>

		<div class="mt-2 flex flex-wrap items-center gap-5">
			<a
				href="#quote"
				class="bg-pink hover:bg-pink-dark rounded-md px-7 py-4 text-[17px] font-bold tracking-[0.02em] text-white transition-colors"
			>
				Schedule a Free Site Visit
			</a>
			<a href={site.phoneHref} class="text-ink hover:text-pink flex flex-col gap-0.5 transition-colors">
				<span class="text-muted text-xs font-bold tracking-[0.12em] uppercase">Or call us</span>
				<span class="text-xl font-bold">{site.phone}</span>
			</a>
		</div>

		<div class="border-line mt-3 flex flex-wrap gap-7 border-t pt-5">
			{#each stats as stat (stat.label)}
				<div class="flex flex-col gap-0.5">
					<span class="display text-[28px] font-extrabold">{stat.value}</span>
					<span class="text-muted text-[13px] font-semibold">{stat.label}</span>
				</div>
			{/each}
		</div>
	</div>

	<div class="relative">
		<div class="border-line-2 aspect-4/5 overflow-hidden rounded-[10px] border">
			{#if heroLoaded}
				<img
					src="/thinkpink/hero.jpg"
					alt="Pink forestry mulcher clearing brush"
					class="block h-full w-full object-cover"
					onerror={() => (heroLoaded = false)}
				/>
			{:else}
				<div
					class="from-pink/15 to-bone-2 flex h-full w-full items-center justify-center bg-gradient-to-br"
				>
					<span class="text-muted-2 font-mono text-xs">hero photo pending</span>
				</div>
			{/if}
		</div>
		<div
			class="bg-ink text-bone absolute bottom-7 -left-2 flex flex-col gap-0.5 rounded-lg px-5 py-4 shadow-[0_12px_32px_rgba(28,20,24,0.25)] sm:-left-[18px]"
		>
			<span class="text-pink-bright text-xs font-bold tracking-[0.12em] uppercase">
				The pink fleet
			</span>
			<span class="text-[15px] font-semibold">You can't miss us on the job site.</span>
		</div>
	</div>
</section>
