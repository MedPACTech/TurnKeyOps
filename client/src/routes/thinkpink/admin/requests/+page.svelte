<script lang="ts">
	let { data } = $props();
	let query = $state('');
	const filtered = $derived(
		data.requests.filter((request) =>
			[request.customerName, request.serviceAddress, request.serviceType, request.propertyType]
				.join(' ')
				.toLowerCase()
				.includes(query.trim().toLowerCase())
		)
	);
</script>

<header class="border-b border-[#eadde3] pb-5">
	<p class="text-xs font-bold uppercase tracking-[0.18em] text-[#d40f80]">Property intake</p>
	<h1 class="mt-2 text-3xl font-black tracking-tight">Requests</h1>
	<p class="mt-2 text-sm text-[#6e5f66]">Public-site leads and property assessment requests for Think Pink only.</p>
</header>

<div class="mt-5">
	<label class="sr-only" for="request-search">Search requests</label>
	<input id="request-search" bind:value={query} placeholder="Search owner, address, acreage, or service" class="w-full border border-[#dccbd4] bg-white px-4 py-3 text-sm outline-none focus:border-[#d40f80]" />
</div>

<section class="mt-4 divide-y divide-[#eadde3] border-y border-[#eadde3] bg-white">
	{#each filtered as request}
		<article class="grid gap-4 px-4 py-5 lg:grid-cols-[1fr_0.7fr_0.5fr]">
			<div>
				<div class="flex flex-wrap items-center gap-2">
					<h2 class="font-bold">{request.customerName}</h2>
					<span class="rounded bg-[#fbd7ea] px-2 py-1 text-[0.68rem] font-bold uppercase tracking-[0.1em] text-[#a50c64]">{request.status.replaceAll('-', ' ')}</span>
				</div>
				<p class="mt-1 text-sm text-[#6e5f66]">{request.serviceAddress}</p>
				<p class="mt-2 text-sm">{request.need}</p>
			</div>
			<div class="text-sm">
				<p class="font-semibold">{request.serviceType}</p>
				<p class="mt-1 text-[#6e5f66]">{request.propertyType}</p>
				<p class="mt-1 text-[#6e5f66]">{request.requestedTimeline}</p>
			</div>
			<div class="text-sm lg:text-right">
				<p class="font-semibold">{request.phone}</p>
				<p class="mt-1 text-[#6e5f66]">{request.email || 'Email not supplied'}</p>
				<p class="mt-2 text-xs text-[#8a7681]">{request.attachments.length} photo{request.attachments.length === 1 ? '' : 's'}</p>
			</div>
		</article>
	{:else}
		<p class="px-4 py-12 text-center text-sm text-[#6e5f66]">No matching Think Pink requests.</p>
	{/each}
</section>
