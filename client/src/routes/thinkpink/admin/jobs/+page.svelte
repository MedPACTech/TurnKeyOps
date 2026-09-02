<script lang="ts">
	let { data } = $props();
	const stages = ['scheduled', 'in-progress', 'on-hold', 'completed', 'cancelled'];
</script>

<header class="border-b border-[#eadde3] pb-5">
	<p class="text-xs font-bold uppercase tracking-[0.18em] text-[#d40f80]">Production</p>
	<h1 class="mt-2 text-3xl font-black tracking-tight">Clearing jobs</h1>
	<p class="mt-2 text-sm text-[#6e5f66]">Tenant-isolated jobs loaded from the durable production API.</p>
</header>

{#if data.error}<div class="mt-5 border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900" role="alert">{data.error} <a href="/thinkpink/admin/jobs" class="font-bold underline">Retry</a></div>{/if}

<div class="mt-5 grid gap-3 lg:grid-cols-5">
	{#each stages as stage}
		<section class="min-h-48 border-t-2 border-[#d40f80] bg-white p-4">
			<h2 class="text-sm font-bold capitalize">{stage.replace('-', ' ')}</h2>
			<p class="mt-1 text-xs text-[#8a7681]">{data.jobs.filter((job) => job.status === stage).length} jobs</p>
			{#each data.jobs.filter((job) => job.status === stage) as job}
				<a href={`/thinkpink/admin/jobs?job=${encodeURIComponent(job.id)}`} class="mt-4 block border-t border-[#eadde3] pt-3 text-sm"><strong>{job.customerName}</strong><span class="mt-1 block text-xs text-[#6e5f66]">{job.scheduledDate} · {job.crew}</span></a>
			{:else}<p class="mt-8 text-center text-xs text-[#9c8e95]">No jobs in this stage</p>{/each}
		</section>
	{/each}
</div>
