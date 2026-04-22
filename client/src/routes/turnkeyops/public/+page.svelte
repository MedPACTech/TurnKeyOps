<script lang="ts">
	import SectionCard from '$lib/components/SectionCard.svelte';
	import { turnkeyOpsPillars } from '$lib/config/platform';

	const audience = [
		'Service businesses graduating from spreadsheets and disconnected tools',
		'Operators who need one system from lead intake to collections',
		'Implementation teams rolling out repeatable vertical workflows'
	];

	const rollout = [
		'Core platform capabilities stay reusable across tenants',
		'Vertical workflows define the operating model for a trade or niche',
		'BDR-specific customizations stay isolated as tenant configuration, not product sprawl'
	];
</script>

<svelte:head>
	<title>TurnKeyOps Platform</title>
	<meta name="description" content="TurnKeyOps public site for the service-business operating platform." />
</svelte:head>

<div class="min-h-screen bg-[radial-gradient(circle_at_top,_rgba(34,211,238,0.16),_transparent_24%),linear-gradient(180deg,_#06111d_0%,_#0b1726_45%,_#101827_100%)] text-white">
	<div class="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
		<header class="grid gap-6 rounded-[2.25rem] border border-white/10 bg-slate-950/55 p-6 shadow-[0_30px_110px_rgba(15,23,42,0.35)] backdrop-blur lg:grid-cols-[1.2fr_0.8fr] lg:p-8">
			<div class="space-y-5">
				<p class="text-[0.72rem] uppercase tracking-[0.28em] text-cyan-200/80">TurnKeyOps.ai</p>
				<h1 class="max-w-3xl text-4xl font-semibold tracking-tight text-white sm:text-5xl">
					The operating platform for service businesses that need fewer handoffs and better follow-through.
				</h1>
				<p class="max-w-2xl text-base leading-7 text-slate-300">
					TurnKeyOps is the product company. BDR is the first tenant implementation. This page now tells the
					platform story clearly instead of collapsing into contractor branding or placeholder text.
				</p>
				<div class="flex flex-wrap gap-3">
					<a href="/turnkeyops/admin" class="rounded-full bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200">View operator console</a>
					<a href="/bdr/public" class="rounded-full border border-white/14 px-5 py-3 text-sm font-semibold text-white transition hover:border-cyan-300/45 hover:bg-white/6">See tenant example</a>
				</div>
			</div>

			<div class="rounded-[1.8rem] border border-cyan-300/14 bg-cyan-300/8 p-6">
				<p class="text-[0.66rem] uppercase tracking-[0.24em] text-cyan-100/75">What this product does</p>
				<div class="mt-4 space-y-4">
					{#each audience as item}
						<div class="rounded-[1.2rem] border border-white/8 bg-white/4 px-4 py-3 text-sm leading-6 text-slate-100">{item}</div>
					{/each}
				</div>
			</div>
		</header>

		<section class="mt-8 grid gap-6 lg:grid-cols-[1fr_0.95fr]">
			<div class="rounded-[2rem] border border-white/10 bg-slate-950/45 p-6 backdrop-blur">
				<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">Product pillars</p>
				<div class="mt-5 grid gap-4 md:grid-cols-3">
					{#each turnkeyOpsPillars as pillar}
						<div class="rounded-[1.5rem] border border-white/8 bg-white/4 p-5">
							<h2 class="text-xl font-semibold text-white">{pillar.title}</h2>
							<p class="mt-2 text-sm leading-6 text-slate-300">{pillar.copy}</p>
						</div>
					{/each}
				</div>
			</div>

			<SectionCard title="Platform discipline" eyebrow="Architecture stance" copy="This is the product framing Christian wanted: keep platform, vertical, and tenant-specific work clearly separated.">
				<ul class="mt-4 grid gap-3">
					{#each rollout as item}
						<li class="rounded-[1.1rem] border border-cyan-300/14 bg-cyan-300/6 px-4 py-3 text-sm leading-6 text-slate-100">{item}</li>
					{/each}
				</ul>
			</SectionCard>
		</section>
	</div>
</div>
