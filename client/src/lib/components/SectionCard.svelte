<script lang="ts">
	let {
		title,
		eyebrow = '',
		copy = '',
		children
	} = $props<{ title: string; eyebrow?: string; copy?: string; children?: () => unknown }>();
</script>

<div class="rounded-[1.6rem] border border-white/10 bg-white/5 p-5 backdrop-blur">
	{#if eyebrow}
		<p class="text-[0.66rem] uppercase tracking-[0.24em] text-slate-400">{eyebrow}</p>
	{/if}
	<h3 class="mt-2 text-lg font-semibold text-white">{title}</h3>
	{#if copy}
		<p class="mt-2 text-sm leading-6 text-slate-300">{copy}</p>
	{/if}
	{#if children}
		{@render children()}
	{/if}
</div>
