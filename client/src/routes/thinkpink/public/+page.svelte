<script lang="ts">
	import StormBanner from '$lib/components/thinkpink/StormBanner.svelte';
	import Header from '$lib/components/thinkpink/Header.svelte';
	import Hero from '$lib/components/thinkpink/Hero.svelte';
	import Services from '$lib/components/thinkpink/Services.svelte';
	import Work from '$lib/components/thinkpink/Work.svelte';
	import ServiceArea from '$lib/components/thinkpink/ServiceArea.svelte';
	import QuoteForm from '$lib/components/thinkpink/QuoteForm.svelte';
	import Footer from '$lib/components/thinkpink/Footer.svelte';
	import { site } from '$lib/tenants/thinkpink/content';
	import type { ActionData } from './$types';

	let { form }: { form: ActionData } = $props();

	const steps = [
		'Send the form — photos help a lot.',
		'We call within one business day to set a visit.',
		'Walk the land with us, get your quote same day.'
	];
</script>

<svelte:head>
	<title>Think Pink Land Clearing — Land Clearing & Forestry Mulching in Central Ohio</title>
	<meta
		name="description"
		content="Land clearing, forestry mulching, stump grinding, and storm cleanup for Central Ohio landowners, builders, and farms. Free on-site quotes. Licensed & insured."
	/>
</svelte:head>

{#if site.stormBanner}
	<StormBanner />
{/if}

<Header />

<main>
	<Hero />
	<Services />
	<Work />
	<ServiceArea />

	<section
		id="quote"
		class="mx-auto grid max-w-[1200px] items-start gap-10 px-5 py-20 sm:px-8 lg:grid-cols-[0.85fr_1.15fr] lg:gap-14 lg:py-22"
	>
		<div class="flex flex-col gap-5 lg:sticky lg:top-[108px]">
			<span class="eyebrow text-pink">Free quote</span>
			<h2 class="display text-ink text-[clamp(2.25rem,6vw,3.25rem)] font-extrabold">
				Let's look at your land
			</h2>
			<p class="text-body text-[17px] leading-relaxed text-pretty">
				Tell us about the property and we'll schedule a free site visit — usually within a few days.
				You'll get a firm quote on the spot, no obligation.
			</p>
			<ol class="mt-2 flex flex-col gap-3.5">
				{#each steps as step, i (step)}
					<li class="flex items-baseline gap-3">
						<span class="display text-pink text-[22px] font-extrabold">{i + 1}</span>
						<span class="text-body text-[15px]">{step}</span>
					</li>
				{/each}
			</ol>
		</div>

		<QuoteForm {form} />
	</section>
</main>

<Footer />

