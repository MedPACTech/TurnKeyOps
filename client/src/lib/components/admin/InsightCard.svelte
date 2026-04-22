<script lang="ts">
	let {
		title,
		description,
		items
	} = $props<{
		title: string;
		description: string;
		items: string[];
	}>();
</script>

<article class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
	<h4 class="text-base font-semibold text-[var(--text-strong)]">{title}</h4>
	<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{description}</p>
	<ul class="mt-4 space-y-2 text-sm text-[var(--text-base)]">
		{#each items as item}
			<li class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2.5">{item}</li>
		{/each}
	</ul>
</article>
