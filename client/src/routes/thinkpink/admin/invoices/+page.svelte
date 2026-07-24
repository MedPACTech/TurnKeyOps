<script lang="ts">
	import { CircleDollarSign, FileText } from 'lucide-svelte';
	import type { PageProps } from './$types';
	let { data }: PageProps = $props();
</script>
<svelte:head><title>Invoices · Think Pink</title></svelte:head>
<div class="mx-auto max-w-6xl space-y-6 pb-10">
	<header><p class="text-xs font-bold uppercase tracking-[.18em] text-[var(--accent-text)]">Revenue</p><h1 class="mt-2 text-3xl font-black">Invoices</h1><p class="mt-2 text-sm text-[var(--text-muted)]">Think Pink’s live billing queue, isolated from BDR.</p></header>
	<section class="overflow-hidden rounded-xl bg-white shadow-[var(--shell-shadow)]">
		{#each data.invoiceCandidates as invoice}
			<div class="grid gap-4 border-b border-[var(--shell-border)] p-5 last:border-0 md:grid-cols-[auto_1fr_auto] md:items-center">
				<div class="flex h-11 w-11 items-center justify-center rounded-lg bg-[var(--accent-soft)] text-[var(--accent-text)]">{#if invoice.status === 'Ready to invoice'}<CircleDollarSign class="h-5 w-5" />{:else}<FileText class="h-5 w-5" />{/if}</div>
				<div><h2 class="font-bold">{invoice.customer}</h2><p class="mt-1 text-sm text-[var(--text-muted)]">{invoice.site} · {invoice.service}</p><p class="mt-2 text-xs text-[var(--text-muted)]">{invoice.nextAction}</p></div>
				<div class="flex items-center gap-3"><span class="rounded-full bg-[var(--accent-soft)] px-3 py-1 text-xs font-bold text-[var(--accent-text)]">{invoice.status}</span><a href={`/thinkpink/admin/estimates?request=${encodeURIComponent(invoice.id)}`} class="text-sm font-bold text-[var(--accent-text)]">Open →</a></div>
			</div>
		{:else}<div class="p-12 text-center"><CircleDollarSign class="mx-auto h-8 w-8 text-[var(--text-muted)]" /><p class="mt-3 font-semibold">No estimates are ready for billing yet.</p><p class="mt-1 text-sm text-[var(--text-muted)]">Approved estimates will appear here automatically.</p></div>{/each}
	</section>
</div>
