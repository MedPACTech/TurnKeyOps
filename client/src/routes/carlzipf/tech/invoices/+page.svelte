<script lang="ts">
	import type { PageProps } from './$types';
	let { data }: PageProps = $props();
	let query = $state('');
	const invoices = $derived(data.invoices.filter((invoice) =>
		[invoice.invoiceNumber, invoice.customerName, invoice.siteName, invoice.serviceSummary]
			.join(' ').toLowerCase().includes(query.toLowerCase().trim())
	));
	const money = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
</script>

<svelte:head><title>Field invoices · Carl Zipf</title></svelte:head>
<main class="mx-auto max-w-3xl space-y-5 px-4 py-7 pb-20 text-slate-900">
	<header class="space-y-2">
		<a href="/carlzipf/tech" class="text-sm font-semibold text-emerald-900 underline">← Field workspace</a>
		<p class="text-sm font-semibold text-emerald-900">Carl Zipf Lock Shop</p>
		<h1 class="text-3xl font-bold">Invoices in the field</h1>
		<p class="text-sm text-slate-600">Open a sent invoice with the customer to review the work and record their completion acknowledgement.</p>
	</header>
	<label class="block"><span class="sr-only">Search invoices</span><input class="w-full rounded-xl border bg-white p-3" placeholder="Search customer, site, or invoice" bind:value={query} /></label>
	<section aria-label="Invoices" class="overflow-hidden rounded-xl border bg-white">
		{#each invoices as invoice}
			<a href={`/carlzipf/tech/invoices/${encodeURIComponent(invoice.id)}`} class="block border-b p-5 last:border-b-0 hover:bg-slate-50">
				<div class="flex items-start justify-between gap-3"><div><h2 class="font-semibold">{invoice.customerName || invoice.invoiceNumber}</h2><p class="mt-1 text-sm text-slate-600">{invoice.invoiceNumber} · {invoice.siteName || invoice.serviceSummary || 'Service work'}</p></div><span class="text-right text-sm font-semibold">{money(invoice.total)}</span></div>
				<p class="mt-3 text-xs font-semibold uppercase tracking-wide text-slate-500">{invoice.completionSignature ? 'Completion acknowledged' : invoice.status.toLowerCase() === 'draft' ? 'Draft' : 'Awaiting completion acknowledgement'}</p>
			</a>
		{:else}
			<p class="p-8 text-center text-sm text-slate-600">{data.invoices.length ? 'No invoices match your search.' : 'No invoices are available yet. Approved quotes appear here after the office creates an invoice.'}</p>
		{/each}
	</section>
</main>
