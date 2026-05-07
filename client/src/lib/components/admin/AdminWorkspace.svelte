<script lang="ts">
	import type { Snippet } from 'svelte';
	import { X } from 'lucide-svelte';

	type Metric = {
		label: string;
		value: string;
		detail?: string;
		icon?: string;
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
	const metricGridClass = $derived(metrics.length >= 4 ? 'xl:grid-cols-4' : 'xl:grid-cols-3');
</script>

<section class="space-y-5">
	<div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
		<div>
			{#if title}
				<h1 class="text-2xl font-semibold leading-8 tracking-normal text-[var(--text-strong)]">{title}</h1>
			{:else if kicker}
				<h1 class="text-2xl font-semibold leading-8 tracking-normal text-[var(--text-strong)]">{kicker}</h1>
			{/if}
			{#if description}
				<p class="sr-only">{description}</p>
			{/if}
		</div>
	</div>

	{#if metrics.length}
		<div class={`grid gap-3 sm:grid-cols-2 ${metricGridClass}`}>
			{#each metrics as metric}
				<div class="flex h-32 flex-col justify-between rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
					<div class="flex items-start justify-between gap-3">
						{#if metric.icon}
							<span class="flex h-9 w-9 items-center justify-center rounded-lg bg-white/80 text-xl shadow-sm" aria-hidden="true">{metric.icon}</span>
						{/if}
					</div>
					<div>
						<p class="text-3xl font-semibold leading-none tracking-normal text-[var(--text-strong)]">{metric.value}</p>
						<p class="mt-2 text-sm font-medium leading-5 text-[var(--text-muted)]">{metric.label}</p>
					</div>
				</div>
			{/each}
		</div>
	{/if}

	<div class={`grid gap-4 ${workspaceColumnsClass}`}>
		{#if context}
			<aside class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
				<p class="text-base font-semibold leading-6 text-[var(--text-strong)]">{contextLabel}</p>
				<div class="mt-4">
					{@render context()}
				</div>
			</aside>
		{/if}

		{#if focus}
			<aside class="rounded-lg bg-white/90 p-4 shadow-[var(--shell-shadow)]">
				<p class="text-base font-semibold leading-6 text-[var(--text-strong)]">{focusLabel}</p>
				<div class="mt-4">
					{@render focus()}
				</div>
			</aside>
		{/if}

		<div class="min-w-0">
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
