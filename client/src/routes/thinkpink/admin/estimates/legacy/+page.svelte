<script lang="ts">
	let { data } = $props();
</script>

<header class="border-b border-[var(--border)] pb-5">
	<p class="text-xs font-bold uppercase tracking-[0.18em] text-[var(--teal-text)]">Revenue</p>
	<h1 class="mt-2 text-3xl font-black tracking-tight">Land clearing estimates</h1>
	<p class="mt-2 max-w-2xl text-sm text-[var(--text-muted)]">Property assessments that are ready for scope, equipment, disposal, restoration, and pricing decisions.</p>
</header>

<section class="mt-5 divide-y divide-[var(--border)] border-y border-[var(--border)] bg-[var(--surface)]">
	{#each data.requests as request}
		<article class="grid gap-4 px-4 py-5 lg:grid-cols-[1fr_1fr_auto]">
			<div>
				<p class="font-bold">{request.customerName}</p>
				<p class="mt-1 text-sm text-[var(--text-muted)]">{request.serviceAddress}</p>
			</div>
			<div class="text-sm">
				<p class="font-semibold">{request.serviceType}</p>
				<p class="mt-1 text-[var(--text-muted)]">{request.propertyType} · {request.requestedTimeline}</p>
			</div>
			<span class="self-start rounded bg-[var(--teal-soft)] px-2 py-1 text-xs font-bold capitalize text-[var(--teal-text)]">{request.status.replaceAll('-', ' ')}</span>
		</article>
	{:else}
		<div class="px-4 py-12 text-center">
			<p class="font-semibold">No estimates in progress</p>
			<p class="mt-1 text-sm text-[var(--text-muted)]">Qualify a request and complete its property assessment to start an estimate.</p>
		</div>
	{/each}
</section>
