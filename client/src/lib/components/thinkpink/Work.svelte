<script lang="ts">
	import { projects } from '$lib/tenants/thinkpink/content';
</script>

<section id="work" class="mx-auto max-w-[1200px] px-5 py-20 sm:px-8 lg:py-22">
	<div class="flex flex-col gap-12">
		<div class="flex flex-col gap-3">
			<span class="eyebrow text-pink">Our work</span>
			<h2 class="display text-ink text-[clamp(2.25rem,6vw,3.25rem)] font-extrabold">
				Before &amp; after
			</h2>
		</div>

		<div class="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
			{#each projects as proj (proj.title)}
				<figure class="flex flex-col gap-3">
					<div
						class="border-line-2 relative flex aspect-4/3 items-center justify-center overflow-hidden rounded-[10px] border"
						style="background: repeating-linear-gradient(-45deg, #F0E4EA 0 14px, #E9DAE1 14px 28px)"
					>
						{#if proj.before && proj.after}
							<div class="grid h-full w-full grid-cols-2">
								<img src={proj.before} alt="{proj.title} — before" class="h-full w-full object-cover" />
								<img src={proj.after} alt="{proj.title} — after" class="h-full w-full object-cover" />
							</div>
						{:else}
							<span
								class="text-muted-2 bg-bone rounded border border-dashed border-[#C9B4BF] px-2.5 py-1.5 font-mono text-xs"
							>
								{proj.placeholder}
							</span>
						{/if}
						<span
							class="bg-ink absolute top-3 left-3 rounded px-2.5 py-[5px] text-[11px] font-bold tracking-[0.12em] text-white uppercase"
						>
							Before
						</span>
						<span
							class="bg-pink absolute top-3 right-3 rounded px-2.5 py-[5px] text-[11px] font-bold tracking-[0.12em] text-white uppercase"
						>
							After
						</span>
					</div>
					<figcaption class="flex flex-col gap-0.5">
						<span class="text-ink text-base font-bold">{proj.title}</span>
						<span class="text-muted text-sm">{proj.meta}</span>
					</figcaption>
				</figure>
			{/each}
		</div>
	</div>
</section>

