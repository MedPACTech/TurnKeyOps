<script lang="ts">
	let { data } = $props();
	const stats = $derived([
		{ label: 'New requests', value: data.metrics.newRequests, detail: 'Need first review and customer follow-up.' },
		{ label: 'Assessment ready', value: data.metrics.assessmentReady, detail: 'Have enough property context to schedule.' },
		{ label: 'Visits scheduled', value: data.metrics.visitsScheduled, detail: 'Property walks waiting on field completion.' },
		{ label: 'Active estimates', value: data.metrics.activeEstimates, detail: 'Drafted or sent clearing proposals.' }
	]);
</script>

<header class="flex flex-wrap items-end justify-between gap-4 border-b border-[#eadde3] pb-5">
	<div>
		<p class="text-xs font-bold uppercase tracking-[0.18em] text-[#d40f80]">Operations</p>
		<h1 class="mt-2 text-3xl font-black tracking-tight">Land clearing command board</h1>
		<p class="mt-2 max-w-2xl text-sm leading-6 text-[#6e5f66]">Intake, property assessments, estimates, mobilization, clearing, disposal, and restoration for Think Pink.</p>
	</div>
	<a href="/thinkpink/admin/requests" class="rounded-md bg-[#d40f80] px-4 py-3 text-sm font-bold text-white">Review requests</a>
</header>

<section class="mt-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
	{#each stats as stat}
		<article class="border-t-2 border-[#d40f80] bg-white px-4 py-5">
			<p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a7681]">{stat.label}</p>
			<p class="mt-2 text-3xl font-black">{stat.value}</p>
			<p class="mt-2 text-sm leading-5 text-[#6e5f66]">{stat.detail}</p>
		</article>
	{/each}
</section>

<section class="mt-7 grid gap-6 lg:grid-cols-[1.25fr_0.75fr]">
	<div>
		<div class="flex items-center justify-between">
			<h2 class="text-lg font-bold">Latest property requests</h2>
			<span class="text-xs text-[#8a7681]">Source: {data.source}</span>
		</div>
		<div class="mt-3 divide-y divide-[#eadde3] border-y border-[#eadde3] bg-white">
			{#each data.requests.slice(0, 5) as request}
				<a href="/thinkpink/admin/requests" class="grid gap-2 px-4 py-4 hover:bg-[#fbd7ea]/25 sm:grid-cols-[1fr_auto]">
					<div>
						<p class="font-bold">{request.customerName}</p>
						<p class="mt-1 text-sm text-[#6e5f66]">{request.serviceType} · {request.propertyType}</p>
					</div>
					<p class="text-sm font-semibold text-[#a50c64]">{request.status.replaceAll('-', ' ')}</p>
				</a>
			{:else}
				<p class="px-4 py-10 text-center text-sm text-[#6e5f66]">No Think Pink requests yet. New public-site submissions will appear here.</p>
			{/each}
		</div>
	</div>
	<aside class="border-l-2 border-[#d40f80] bg-white p-5">
		<p class="text-xs font-bold uppercase tracking-[0.16em] text-[#d40f80]">Trade profile</p>
		<h2 class="mt-2 text-lg font-bold">Land clearing defaults</h2>
		<ul class="mt-4 space-y-2 text-sm leading-5 text-[#6e5f66]">
			<li>Acreage and vegetation density</li>
			<li>Tree count, diameter, and stump handling</li>
			<li>Terrain, equipment access, and utility risk</li>
			<li>Chipping, hauling, burning, and disposal</li>
			<li>Grading, seeding, erosion control, and restoration</li>
		</ul>
	</aside>
</section>
