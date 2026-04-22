<script lang="ts">
	import type { Snippet } from 'svelte';

	type Metric = {
		label: string;
		value: string;
		detail?: string;
	};

	let {
		kicker = '',
		title = '',
		description = '',
		metrics = [],
		contextLabel = 'Context',
		focusLabel = 'Focus',
		context,
		focus,
		work,
		drawer,
		drawerOpen = false,
		drawerTitle = '',
		closeDrawer = () => {}
	} = $props<{
		kicker?: string;
		title?: string;
		description?: string;
		metrics?: Metric[];
		contextLabel?: string;
		focusLabel?: string;
		context?: Snippet;
		focus?: Snippet;
		work: Snippet;
		drawer?: Snippet;
		drawerOpen?: boolean;
		drawerTitle?: string;
		closeDrawer?: () => void;
	}>();

	const workspaceColumnsClass = $derived.by(() => {
		if (context && focus) return 'xl:grid-cols-[260px_320px_minmax(0,1fr)]';
		if (focus) return 'xl:grid-cols-[320px_minmax(0,1fr)]';
		if (context) return 'xl:grid-cols-[260px_minmax(0,1fr)]';
		return '';
	});
</script>

<section>
	<div class={`grid gap-4 ${workspaceColumnsClass}`}>
		{#if context}
			<aside class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{contextLabel}</p>
				<div class="mt-4">
					{@render context()}
				</div>
			</aside>
		{/if}

		{#if focus}
			<aside class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{focusLabel}</p>
				<div class="mt-4">
					{@render focus()}
				</div>
			</aside>
		{/if}

		<div class="min-w-0 rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
			{@render work()}
		</div>
	</div>
</section>

{#if drawer && drawerOpen}
	<button
		type="button"
		class="fixed inset-0 z-40 bg-slate-950/35"
		aria-label="Close details drawer"
		onclick={closeDrawer}
	></button>
	<aside class="fixed inset-y-14 right-0 z-50 flex w-full max-w-xl flex-col border-l border-[var(--shell-border)] bg-[var(--drawer-bg)] shadow-[0_28px_90px_rgba(15,23,42,0.22)]">
		<div class="flex items-center justify-between border-b border-[var(--shell-border)] px-5 py-4">
			<div>
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.2em] text-[var(--muted)]">Details</p>
				<h4 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">{drawerTitle}</h4>
			</div>
			<button
				type="button"
				class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2 text-sm font-medium text-[var(--text-strong)] transition hover:bg-[var(--shell-panel-strong)]"
				onclick={closeDrawer}
			>
				Close
			</button>
		</div>
		<div class="flex-1 overflow-y-auto p-5">
			{@render drawer()}
		</div>
	</aside>
{/if}
