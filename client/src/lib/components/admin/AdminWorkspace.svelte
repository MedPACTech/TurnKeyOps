<script lang="ts">
	import type { Snippet } from 'svelte';
	import { X } from 'lucide-svelte';

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

<section class="space-y-5">
	<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-5 shadow-[var(--shell-shadow)]">
		<div class="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-4xl">
				{#if kicker}
					<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">{kicker}</p>
				{/if}
				{#if title}
					<h1 class="mt-2 text-2xl font-bold tracking-tight text-[var(--text-strong)]">{title}</h1>
				{/if}
				{#if description}
					<p class="mt-2 max-w-3xl text-sm leading-6 text-[var(--text-muted)]">{description}</p>
				{/if}
			</div>
		</div>

		{#if metrics.length}
			<div class="mt-5 grid gap-3 md:grid-cols-3">
				{#each metrics as metric}
					<div class="rounded-lg border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-4 py-3">
						<p class="text-xs font-medium text-[var(--text-muted)]">{metric.label}</p>
						<p class="mt-1 text-2xl font-bold text-[var(--text-strong)]">{metric.value}</p>
						{#if metric.detail}
							<p class="mt-1 text-xs leading-5 text-[var(--text-muted)]">{metric.detail}</p>
						{/if}
					</div>
				{/each}
			</div>
		{/if}
	</div>

	<div class={`grid gap-4 ${workspaceColumnsClass}`}>
		{#if context}
			<aside class="rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
				<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{contextLabel}</p>
				<div class="mt-4">
					{@render context()}
				</div>
			</aside>
		{/if}

		{#if focus}
			<aside class="rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
				<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{focusLabel}</p>
				<div class="mt-4">
					{@render focus()}
				</div>
			</aside>
		{/if}

		<div class="min-w-0 rounded-lg border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
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
	<aside class="fixed inset-y-0 right-0 z-50 flex w-full max-w-xl flex-col border-l border-[var(--shell-border)] bg-[var(--drawer-bg)] shadow-xl">
		<div class="flex items-center justify-between border-b border-[var(--shell-border)] px-5 py-4">
			<div>
				<p class="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">Details</p>
				<h4 class="mt-1 text-lg font-semibold text-[var(--text-strong)]">{drawerTitle}</h4>
			</div>
			<button
				type="button"
				class="inline-flex h-10 w-10 items-center justify-center rounded-lg border border-[var(--shell-border)] bg-white text-[var(--text-base)] transition hover:bg-[var(--shell-panel-strong)]"
				aria-label="Close details"
				onclick={closeDrawer}
			>
				<X class="h-5 w-5" aria-hidden="true" />
			</button>
		</div>
		<div class="flex-1 overflow-y-auto p-5">
			{@render drawer()}
		</div>
	</aside>
{/if}
