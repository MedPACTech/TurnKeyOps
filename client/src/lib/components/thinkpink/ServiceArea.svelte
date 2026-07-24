<script lang="ts">
	import ServiceAreaMap from './ServiceAreaMap.svelte';
	import { counties } from '$lib/tenants/thinkpink/content';
</script>

<section id="area" class="bg-bone-2 px-5 py-20 sm:px-8 lg:py-22">
	<div
		class="mx-auto grid max-w-[1200px] items-center gap-10 lg:grid-cols-[0.9fr_1.1fr] lg:gap-14"
	>
		<div class="flex flex-col gap-5">
			<span class="eyebrow text-pink">Where we work</span>
			<h2 class="display text-ink text-[clamp(2.25rem,6vw,3.25rem)] font-extrabold">
				Proudly serving Central Ohio
			</h2>
			<p class="text-body max-w-[48ch] text-[17px] leading-relaxed text-pretty">
				Based near Columbus, our crews travel roughly 50 miles in every direction. Outside the
				circle? Ask anyway — for bigger jobs, we'll come to you.
			</p>
			<ul class="text-ink grid grid-cols-2 gap-x-6 gap-y-2 text-[15px] font-semibold">
				{#each counties as county (county)}
					<li>{county}</li>
				{/each}
			</ul>
		</div>

		<ServiceAreaMap />
	</div>
</section>

