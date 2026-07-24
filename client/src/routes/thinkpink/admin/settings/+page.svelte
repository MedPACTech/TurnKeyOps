<script lang="ts">
	import { thinkPinkTenant } from '$lib/config/tenants';
</script>

<header class="border-b border-[#eadde3] pb-5">
	<p class="text-xs font-bold uppercase tracking-[0.18em] text-[#d40f80]">Configuration</p>
	<h1 class="mt-2 text-3xl font-black tracking-tight">Land clearing defaults</h1>
	<p class="mt-2 text-sm text-[#6e5f66]">Trade-level defaults for Think Pink. These do not change BDR’s construction workflow.</p>
</header>

<div class="mt-6 grid gap-4 lg:grid-cols-3">
	<section class="bg-white p-5">
		<h2 class="font-bold">Services</h2>
		<ul class="mt-3 space-y-2 text-sm text-[#6e5f66]">
			{#each thinkPinkTenant.services as item}<li>{item}</li>{/each}
		</ul>
	</section>
	<section class="bg-white p-5">
		<h2 class="font-bold">Estimate inputs</h2>
		<ul class="mt-3 space-y-2 text-sm text-[#6e5f66]">
			{#each thinkPinkTenant.estimateInputs as item}<li>{item}</li>{/each}
		</ul>
	</section>
	<section class="bg-white p-5">
		<h2 class="font-bold">Production stages</h2>
		<ol class="mt-3 space-y-2 text-sm text-[#6e5f66]">
			{#each thinkPinkTenant.jobStages as item, index}<li>{index + 1}. {item}</li>{/each}
		</ol>
	</section>
</div>
