<script lang="ts">
	import AdminIcon from '$lib/components/admin/AdminIcon.svelte';
	import type { Snippet } from 'svelte';
	import AdminDrawer from './AdminDrawer.svelte';

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
				<div class="flex h-32 flex-col justify-between rounded-lg bg-[var(--surface)] p-4 shadow-[var(--shell-shadow)]">
					<div class="flex items-start justify-between gap-3">
						{#if metric.icon}
							<span class="flex h-9 w-9 items-center justify-center rounded-lg bg-[var(--surface)] text-xl shadow-sm" aria-hidden="true"><AdminIcon name={metric.icon} /></span>
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
			<aside class="rounded-lg bg-[var(--surface)] p-4 shadow-[var(--shell-shadow)]">
				<p class="text-base font-semibold leading-6 text-[var(--text-strong)]">{contextLabel}</p>
				<div class="mt-4">
					{@render context()}
				</div>
			</aside>
		{/if}

		{#if focus}
			<aside class="rounded-lg bg-[var(--surface)] p-4 shadow-[var(--shell-shadow)]">
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
  <AdminDrawer title={drawerTitle || 'Details'} close={closeDrawer}>
    {@render drawer()}
  </AdminDrawer>
{/if}
