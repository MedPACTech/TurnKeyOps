<script lang="ts">
	export type EditableField = {
		label: string;
		value: string;
		help?: string;
		multiline?: boolean;
	};

	export type EditableListItem = {
		label: string;
		value: string;
		detail?: string;
	};

	let {
		eyebrow,
		title,
		description,
		status = 'Draft scaffold',
		fields = [],
		listTitle,
		listItems = [],
		actions = []
	} = $props<{
		eyebrow: string;
		title: string;
		description: string;
		status?: string;
		fields?: EditableField[];
		listTitle?: string;
		listItems?: EditableListItem[];
		actions?: string[];
	}>();
</script>

<article class="rounded-md border border-[var(--shell-border)] bg-[var(--module-bg)] p-4 shadow-[var(--shell-shadow)]">
	<div class="flex flex-wrap items-start justify-between gap-3">
		<div>
			<p class="text-[0.6rem] font-semibold uppercase tracking-[0.22em] text-[var(--accent-text)]">{eyebrow}</p>
			<h4 class="mt-2 text-xl font-semibold text-[var(--text-strong)]">{title}</h4>
			<p class="mt-2 text-sm leading-6 text-[var(--text-muted)]">{description}</p>
		</div>
		<span class="rounded-full border border-[var(--accent-border)] bg-[var(--accent-soft)] px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--accent-text)]">{status}</span>
	</div>

	<div class="mt-5 grid gap-3 lg:grid-cols-2">
		{#each fields as field}
			<label class="block rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
				<span class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{field.label}</span>
				{#if field.multiline}
					<textarea class="mt-2 min-h-28 w-full resize-y rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2 text-sm text-[var(--text-base)] outline-none" value={field.value}></textarea>
				{:else}
					<input class="mt-2 w-full rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-3 py-2 text-sm text-[var(--text-base)] outline-none" value={field.value} />
				{/if}
				{#if field.help}
					<p class="mt-2 text-xs leading-5 text-[var(--text-muted)]">{field.help}</p>
				{/if}
			</label>
		{/each}
	</div>

	{#if listItems.length > 0}
		<div class="mt-5 rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] p-3">
			<div class="flex items-center justify-between gap-3">
				<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{listTitle ?? 'Managed items'}</p>
				<button type="button" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] px-2.5 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--text-base)]">Add item</button>
			</div>

			<div class="mt-3 grid gap-3">
				{#each listItems as item, index}
					<div class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel-strong)] p-3">
						<div class="flex items-start justify-between gap-3">
							<div>
								<p class="text-[0.62rem] font-semibold uppercase tracking-[0.18em] text-[var(--muted)]">{item.label} {index + 1}</p>
								<p class="mt-2 text-sm font-medium text-[var(--text-strong)]">{item.value}</p>
								{#if item.detail}
									<p class="mt-1 text-sm leading-6 text-[var(--text-muted)]">{item.detail}</p>
								{/if}
							</div>
							<button type="button" class="rounded-md border border-[var(--shell-border)] px-2.5 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[var(--text-base)]">Edit</button>
						</div>
					</div>
				{/each}
			</div>
		</div>
	{/if}

	{#if actions.length > 0}
		<div class="mt-5 flex flex-wrap gap-2">
			{#each actions as action}
				<button type="button" class="rounded-md border border-[var(--shell-border)] bg-[var(--shell-panel)] px-3 py-2 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--text-base)]">
					{action}
				</button>
			{/each}
		</div>
	{/if}
</article>
