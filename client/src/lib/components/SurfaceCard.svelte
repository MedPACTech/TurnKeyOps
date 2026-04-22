<script lang="ts">
	import type { SurfaceDefinition } from '$lib/config/platform';

	let { surface } = $props<{ surface: SurfaceDefinition }>();

	const themeClasses: Record<SurfaceDefinition['theme'], string> = {
		platform: 'from-cyan-400/18 via-sky-400/10 to-indigo-500/18',
		tenant: 'from-amber-300/18 via-orange-300/10 to-rose-500/16',
		operations: 'from-emerald-300/18 via-teal-300/10 to-sky-400/16'
	};

	const themeClass = $derived(themeClasses[surface.theme as SurfaceDefinition['theme']]);
</script>

<a
	href={surface.path}
	class="group relative flex min-h-64 flex-col justify-between overflow-hidden rounded-[2rem] border border-white/12 bg-slate-950/65 p-6 shadow-[0_20px_80px_rgba(15,23,42,0.22)] backdrop-blur transition duration-200 hover:-translate-y-1 hover:border-white/20"
>
	<div class={`absolute inset-0 bg-gradient-to-br ${themeClass} opacity-90`}></div>
	<div class="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-white/40 to-transparent"></div>

	<div class="relative space-y-4">
		<div class="flex items-center justify-between gap-4">
			<span
				class={`rounded-full px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.24em] ${
					surface.status === 'active' ? 'bg-white/14 text-white' : 'bg-white/8 text-slate-200'
				}`}
			>
				{surface.status}
			</span>
			<span class="text-right text-xs uppercase tracking-[0.24em] text-slate-300/85">{surface.audience}</span>
		</div>

		<div class="space-y-2">
			<h2 class="max-w-md text-2xl font-semibold text-white">{surface.title}</h2>
			<p class="max-w-xl text-sm leading-6 text-slate-200/90">{surface.description}</p>
		</div>
	</div>

	<div class="relative space-y-4">
		<p class="max-w-lg text-sm leading-6 text-white/85">{surface.highlight}</p>
		<div class="flex items-center justify-between border-t border-white/10 pt-5 text-sm text-slate-100">
			<span>{surface.path}</span>
			<span class="transition duration-200 group-hover:translate-x-1">Open surface</span>
		</div>
	</div>
</a>
