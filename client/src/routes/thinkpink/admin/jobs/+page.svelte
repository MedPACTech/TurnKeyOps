<script lang="ts">
	let { data } = $props();
	const stages = ['Mobilization', 'Clearing', 'Disposal', 'Restoration', 'Closeout'];
</script>

<header class="border-b border-[#eadde3] pb-5">
	<p class="text-xs font-bold uppercase tracking-[0.18em] text-[#d40f80]">Production</p>
	<h1 class="mt-2 text-3xl font-black tracking-tight">Clearing jobs</h1>
	<p class="mt-2 text-sm text-[#6e5f66]">Awarded work organized around the land-clearing production sequence.</p>
</header>

<div class="mt-5 grid gap-3 lg:grid-cols-5">
	{#each stages as stage}
		<section class="min-h-48 border-t-2 border-[#d40f80] bg-white p-4">
			<h2 class="text-sm font-bold">{stage}</h2>
			<p class="mt-1 text-xs text-[#8a7681]">0 active</p>
			<p class="mt-8 text-center text-xs text-[#9c8e95]">No jobs in this stage</p>
		</section>
	{/each}
</div>

{#if data.requests.length}
	<section class="mt-6 border-y border-[#eadde3] bg-white px-4 py-5">
		<h2 class="font-bold">Awarded requests waiting for production setup</h2>
		{#each data.requests as request}
			<p class="mt-3 text-sm">{request.customerName} · {request.serviceType} · {request.serviceAddress}</p>
		{/each}
	</section>
{/if}
