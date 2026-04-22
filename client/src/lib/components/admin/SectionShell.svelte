<script lang="ts">
	let {
		kicker,
		heading,
		description,
		metrics,
		children
	} = $props<{
		kicker: string;
		heading: string;
		description: string;
		metrics: { label: string; value: string; detail: string }[];
		children: () => unknown;
	}>();
</script>

<section class="space-y-4">
	<div class="grid gap-3 xl:grid-cols-[minmax(0,1.2fr)_minmax(340px,0.8fr)]">
		<div class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
			<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">{kicker}</p>
			<h3 class="mt-2 text-2xl font-semibold tracking-tight text-[var(--text-strong)]">{heading}</h3>
			<p class="mt-3 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{description}</p>
		</div>

		<div class="grid gap-3 sm:grid-cols-3 xl:grid-cols-3">
			{#each metrics as metric}
				<div class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] px-4 py-3 shadow-[var(--shell-shadow)]">
					<p class="text-[0.56rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{metric.label}</p>
					<p class="mt-2 text-xl font-semibold text-[var(--text-strong)]">{metric.value}</p>
					<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{metric.detail}</p>
				</div>
			{/each}
		</div>
	</div>

	<div class="grid gap-3 xl:grid-cols-2">
		{@render children()}
	</div>
</section>
