<script lang="ts">
	import { services } from '$lib/tenants/thinkpink/content';
</script>

<section id="services" class="bg-ink text-bone px-5 py-20 sm:px-8 lg:py-22">
	<div class="mx-auto flex max-w-[1200px] flex-col gap-12">
		<div class="flex flex-col gap-6 md:flex-row md:items-end md:justify-between md:gap-8">
			<div class="flex flex-col gap-3">
				<span class="eyebrow text-pink-bright">What we do</span>
				<h2 class="display text-[clamp(2.25rem,6vw,3.25rem)] font-extrabold">
					From wild to workable
				</h2>
			</div>
			<p class="text-dark-body max-w-[42ch] text-base leading-relaxed text-pretty">
				One crew, one visit, one clean result. We size every job on site so the quote you get is the
				price you pay.
			</p>
		</div>

		<div class="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
			{#each services as svc (svc.num)}
				<div
					class="bg-ink-soft border-ink-line hover:border-pink flex flex-col gap-3 rounded-[10px] border p-7 transition-colors"
				>
					<span class="display text-pink-bright text-[22px] font-extrabold">{svc.num}</span>
					<h3 class="display text-[26px] font-bold tracking-[0.02em]">{svc.name}</h3>
					<p class="text-dark-body text-[15px] leading-relaxed text-pretty">{svc.desc}</p>
				</div>
			{/each}

			<div class="bg-pink flex flex-col justify-between gap-4 rounded-[10px] p-7">
				<div class="flex flex-col gap-2.5">
					<h3 class="display text-[28px] leading-[1.05] font-extrabold text-white">
						Not sure what your land needs?
					</h3>
					<p class="text-pink-pale text-[15px] leading-relaxed">
						Send photos with your quote request and we'll tell you exactly what it takes.
					</p>
				</div>
				<a
					href="#quote"
					class="bg-bone text-pink hover:bg-ink self-start rounded-md px-[22px] py-3 font-bold transition-colors hover:text-white"
				>
					Request a Quote
				</a>
			</div>
		</div>
	</div>
</section>

