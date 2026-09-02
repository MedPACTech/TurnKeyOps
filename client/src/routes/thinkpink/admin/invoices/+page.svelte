<script lang="ts">
	import { CircleDollarSign, FileText } from 'lucide-svelte';
	import type { PageProps } from './$types';
	let { data }: PageProps = $props();
</script>
<svelte:head><title>Invoices · Think Pink</title></svelte:head>
<div class="mx-auto max-w-6xl space-y-6 pb-10">
	<header><p class="text-xs font-bold uppercase tracking-[.18em] text-[var(--accent-text)]">Revenue</p><h1 class="mt-2 text-3xl font-black">Invoices</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Think Pink’s live billing queue, isolated from BDR.</p></header>
	<section class="overflow-hidden rounded-xl bg-white shadow-[var(--shell-shadow)]">
	{#if data.error}<div class="border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900" role="alert">{data.error} <a href="/thinkpink/admin/invoices" class="font-bold underline">Retry</a></div>{/if}
	{#each data.invoices as invoice}
			<div class="grid gap-4 border-b border-[var(--shell-border)] p-5 last:border-0 md:grid-cols-[auto_1fr_auto] md:items-center">
				<div class="flex h-11 w-11 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-[var(--accent-text)]">{#if invoice.state === 'paid'}<CircleDollarSign class="h-5 w-5" />{:else}<FileText class="h-5 w-5" />{/if}</div>
				<div><h2 class="font-bold">{invoice.customerName}</h2><p class="mt-1 text-sm text-[var(--text-muted)]">{invoice.siteName} · {invoice.serviceSummary}</p><p class="mt-2 text-xs text-[var(--text-muted)]">{invoice.invoiceNumber} · ${invoice.balanceDue.toLocaleString()} due</p></div>
				<div class="flex items-center gap-3"><span class="rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-bold text-[var(--accent-text)]">{invoice.state}</span><a href={`/thinkpink/invoice/${encodeURIComponent(invoice.id)}`} class="text-sm font-bold text-[var(--accent-text)]">Open →</a></div>
			</div>
		{:else}<div class="p-12 text-center"><CircleDollarSign class="mx-auto h-8 w-8 text-[var(--text-muted)]" /><p class="mt-3 font-semibold">No invoices yet.</p><p class="mt-1 text-sm text-[var(--text-muted)]">Durable tenant invoices will appear here when created.</p></div>{/each}
	</section>
</div>
