<script lang="ts">
	import { enhance } from '$app/forms';
	import { page } from '$app/state';
	import IssuedQuoteLink from '$lib/components/locksmith/IssuedQuoteLink.svelte';
	import { measurementFields } from '$lib/locksmith-drafts';
	import type { PageProps } from './$types';
	let { data, form }: PageProps = $props();
	let busy = $state(false);
	const selected = $derived(data.estimates.find((estimate) => estimate.quoteRequestId === page.url.searchParams.get('request')));
	const money = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
</script>

<svelte:head><title>Estimates · Carl Zipf</title><meta name="robots" content="noindex, nofollow" /></svelte:head>
<div class="mx-auto max-w-7xl space-y-5 pb-10">
	<header><p class="text-sm font-semibold text-[var(--accent-text)]">Carl Zipf Lock Shop</p><h1 class="mt-2 text-3xl font-bold">Estimate review</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Review shared field estimates, approve pricing exceptions, and issue a customer review link.</p></header>
	{#if data.loadError}<p class="rounded-lg bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]" role="alert">{data.loadError} <a class="underline" href="/carlzipf/admin/estimates">Retry</a></p>{/if}
	{#if form?.error}<p class="rounded-lg bg-[var(--critical-soft)] p-4 text-[var(--critical-text)]" role="alert">{form.error}</p>{/if}
	{#if form?.message}<p class="rounded-lg bg-[var(--positive-soft)] p-4 text-[var(--positive-text)]" role="status">{form.message}</p>{/if}
	<div class="grid items-start gap-5 lg:grid-cols-[.85fr_1.15fr]">
		<section class="overflow-hidden rounded-xl border bg-[var(--surface)]" aria-label="Estimate list">
			{#each data.estimates as estimate}<a class="block border-b p-5 hover:bg-[var(--surface-subtle)]" class:admin-selected={selected?.id === estimate.id} aria-current={selected?.id === estimate.id ? 'page' : undefined} href={`?request=${encodeURIComponent(estimate.quoteRequestId)}`}><div class="flex flex-wrap justify-between gap-2"><strong>{estimate.customerName}</strong><span class="text-xs capitalize">{estimate.delivery?.status?.replaceAll('-', ' ') ?? estimate.status.replaceAll('-', ' ')}</span></div><p class="mt-2 text-sm text-[var(--text-muted)]">{estimate.siteName}</p><p class="mt-2 text-sm">{money(estimate.locksmithPricing.total)} · Revision {estimate.revisionNumber}</p></a>{:else}{#if !data.loadError}<p class="p-8 text-center text-sm text-[var(--text-muted)]">No field estimates yet. Technicians prepare server-priced drafts from the field workspace.</p>{/if}{/each}
		</section>
		{#if selected}
			<section class="rounded-xl border bg-[var(--surface)] p-6" aria-label="Estimate detail">
				<h2 class="text-xl font-bold">{selected.customerName}</h2><p class="mt-2 text-sm text-[var(--text-muted)]">{selected.siteName}</p><p class="mt-2 text-sm">{selected.serviceSummary}</p><p class="mt-4 text-3xl font-bold">{money(selected.locksmithPricing.total)}</p><p class="mt-2 text-sm text-[var(--text-muted)]">Discount: {money(selected.locksmithPricing.discountAmount)} · Tax: {money(selected.locksmithPricing.taxAmount)}</p>
				<p class="mt-3 text-xs text-[var(--text-muted)]">Revision {selected.revisionNumber} · {selected.status} · pricing policy {selected.locksmithPricing.policyVersion}</p>
				<ul class="mt-5 divide-y">{#each selected.locksmithPricing.lines ?? [] as line}<li class="flex justify-between gap-4 py-3 text-sm"><div><strong>{line.name}</strong><p class="mt-1 text-[var(--text-muted)]">{line.openingName} · {line.quantity} × {money(line.unitPrice)}</p></div><span>{money(line.total)}</span></li>{/each}</ul>
				{#if selected.locksmithPricing.laborHours}<p class="mt-3 text-sm">Labor: {selected.locksmithPricing.laborHours} hours × {money(selected.locksmithPricing.laborRatePerHour ?? 0)}</p>{/if}
				{#each selected.locksmithPricing.openings ?? [] as opening}<details class="mt-3 rounded border p-3"><summary class="cursor-pointer text-sm font-semibold">{opening.name} · {opening.service}</summary><dl class="mt-3 space-y-2">{#each Object.entries(opening.measurements ?? {}) as [key, measurement]}{#if measurement.value}<div class="flex flex-wrap justify-between gap-2 text-xs"><dt>{measurementFields.find(([name]) => name === key)?.[1] ?? key}</dt><dd>{measurement.value} in · {measurement.certainty}</dd></div>{/if}{/each}</dl><p class="mt-2 whitespace-pre-wrap text-sm">{opening.handing}</p><p class="mt-2 whitespace-pre-wrap text-sm">{opening.notes}</p><p class="mt-2 whitespace-pre-wrap text-sm">{opening.commercialNotes}</p></details>{/each}
				{#if selected.locksmithPricing.officeApprovedAtUtc}<p class="mt-5 text-sm text-[var(--positive-text)]">Office pricing approval recorded {new Date(selected.locksmithPricing.officeApprovedAtUtc).toLocaleString()}.</p>{:else if selected.locksmithPricing.approvalReasons.length}<ul class="mt-5 list-disc space-y-1 rounded bg-[var(--warning-soft)] p-4 pl-8 text-sm text-[var(--warning-text)]">{#each selected.locksmithPricing.approvalReasons as reason}<li>{reason}</li>{/each}</ul>{/if}
				{#if selected.status === 'draft' || selected.status === 'ready-to-send'}
					{#key selected.version}<form method="POST" action={`?/${selected.status === 'draft' ? 'approve' : 'issue'}&request=${selected.quoteRequestId}`} class="mt-5 space-y-4 border-t pt-5" use:enhance={() => { busy = true; return async ({ update }) => { await update(); busy = false; }; }}>
						<input type="hidden" name="requestId" value={selected.quoteRequestId} /><input type="hidden" name="version" value={selected.version} />
						<label class="flex items-start gap-3 text-sm"><input class="mt-1" type="checkbox" name="reviewed" value="yes" required />I reviewed this revision’s scope, configured prices, and pricing exceptions.</label>
						<button class="btn-primary" disabled={busy}>{busy ? 'Saving…' : selected.status === 'draft' ? 'Approve pricing and mark ready' : 'Issue customer review link'}</button>
						<p class="text-xs text-[var(--text-muted)]">Office pricing approval is separate from customer approval. Issuing creates a review link; it does not send email or text, sign for the customer, or book a job.</p>
					</form>{/key}
				{/if}
				<IssuedQuoteLink estimate={selected} />
				<a class="mt-5 block text-sm underline" href={`/carlzipf/admin/requests?request=${selected.quoteRequestId}`}>View source request and photos</a>
			</section>
		{:else}<div class="rounded-xl border border-dashed p-10 text-center text-sm text-[var(--text-muted)]">{data.selectedId ? 'This estimate is not available. Select another estimate.' : 'Select an estimate to review its scope and pricing.'}</div>{/if}
	</div>
</div>
