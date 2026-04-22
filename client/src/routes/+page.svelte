<script lang="ts">
	import SurfaceCard from '$lib/components/SurfaceCard.svelte';
	import { bdrDashboardStats, surfaceDefinitions, turnkeyOpsPillars } from '$lib/config/platform';
</script>

<svelte:head>
	<title>TurnKeyOps Platform</title>
	<meta
		name="description"
		content="TurnKeyOps platform surfaces for public marketing, tenant operations, and first-pass BDR execution."
	/>
</svelte:head>

<div class="min-h-screen bg-[radial-gradient(circle_at_top,_rgba(56,189,248,0.16),_transparent_28%),linear-gradient(180deg,_#07101c_0%,_#0f172a_45%,_#111827_100%)] text-white">
	<div class="mx-auto flex min-h-screen max-w-7xl flex-col px-4 py-8 sm:px-6 lg:px-8">
		<header class="grid gap-6 rounded-[2.25rem] border border-white/10 bg-slate-950/50 p-6 shadow-[0_30px_110px_rgba(2,8,23,0.35)] backdrop-blur lg:grid-cols-[1.2fr_0.85fr] lg:p-8">
			<div class="space-y-5">
				<p class="text-[0.72rem] uppercase tracking-[0.28em] text-cyan-200/80">TurnKeyOps Platform Surfaces</p>
				<div class="space-y-4">
					<h1 class="max-w-3xl text-4xl font-semibold tracking-tight text-white sm:text-5xl">
						Separate platform story. Separate BDR brand. One coherent operating system underneath.
					</h1>
					<p class="max-w-2xl text-base leading-7 text-slate-300">
						This first pass replaces the placeholder shell with four intentional surfaces: TurnKeyOps public,
						TurnKeyOps operator console, BDR public, and BDR admin. The structure keeps the platform and tenant
						identities distinct while preserving the multi-surface topology for future tenants.
					</p>
				</div>
			</div>

			<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-1">
				<div class="rounded-[1.7rem] border border-cyan-300/15 bg-cyan-300/8 p-5">
					<p class="text-[0.66rem] uppercase tracking-[0.24em] text-cyan-100/70">Platform stance</p>
					<p class="mt-3 text-xl font-semibold text-white">Core platform vs. tenant implementation</p>
					<p class="mt-2 text-sm leading-6 text-slate-200/90">TurnKeyOps sells the operating model. BDR proves it as the first real vertical implementation.</p>
				</div>
				<div class="rounded-[1.7rem] border border-emerald-300/15 bg-emerald-300/8 p-5">
					<p class="text-[0.66rem] uppercase tracking-[0.24em] text-emerald-100/70">MVP coverage</p>
					<p class="mt-3 text-xl font-semibold text-white">Lead → schedule → contract → invoice</p>
					<p class="mt-2 text-sm leading-6 text-slate-200/90">The working surfaces now reflect the actual BDR MVP blueprint instead of generic dashboard placeholders.</p>
				</div>
			</div>
		</header>

		<section class="mt-8 grid gap-5 lg:grid-cols-2">
			{#each surfaceDefinitions as surface}
				<SurfaceCard {surface} />
			{/each}
		</section>

		<section class="mt-8 grid gap-6 lg:grid-cols-[1fr_0.95fr]">
			<div class="rounded-[2rem] border border-white/10 bg-slate-950/45 p-6 backdrop-blur">
				<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">TurnKeyOps pillars</p>
				<div class="mt-5 grid gap-4 md:grid-cols-3">
					{#each turnkeyOpsPillars as pillar}
						<div class="rounded-[1.4rem] border border-white/8 bg-white/4 p-4">
							<h2 class="text-lg font-semibold text-white">{pillar.title}</h2>
							<p class="mt-2 text-sm leading-6 text-slate-300">{pillar.copy}</p>
						</div>
					{/each}
				</div>
			</div>

			<div class="rounded-[2rem] border border-white/10 bg-slate-950/45 p-6 backdrop-blur">
				<p class="text-[0.68rem] uppercase tracking-[0.24em] text-slate-400">BDR live operating snapshot</p>
				<div class="mt-5 grid gap-4 sm:grid-cols-2">
					{#each bdrDashboardStats as stat}
						<div class="rounded-[1.4rem] border border-white/8 bg-white/4 p-4">
							<p class="text-xs uppercase tracking-[0.2em] text-slate-400">{stat.label}</p>
							<p class="mt-2 text-3xl font-semibold text-white">{stat.value}</p>
							<p class="mt-2 text-sm leading-6 text-slate-300">{stat.detail}</p>
						</div>
					{/each}
				</div>
			</div>
		</section>
	</div>
</div>
